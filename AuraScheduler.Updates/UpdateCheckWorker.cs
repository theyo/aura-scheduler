using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

using AuraScheduler.Worker;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraScheduler.UI.Infrastructure;

internal sealed record ReleaseInfo(string Version, string Name, string Notes, string Url, string InstallerUrl);

internal sealed record UpdateReleaseAsset(string Name, string DownloadUrl);

internal sealed record UpdateRelease(string TagName, string Name, string Notes, string Url, bool Draft, bool Prerelease, IReadOnlyList<UpdateReleaseAsset> Assets);

internal interface IUpdateReleaseClient
{
    Task<UpdateRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken);
}

internal interface IUpdateInstaller
{
    Task DownloadAndLaunchInstallerAsync(ReleaseInfo release, Action installerLaunching, CancellationToken cancellationToken);

    void OpenRelease(ReleaseInfo release);
}

internal interface IUpdateVersionProvider
{
    string CurrentVersion { get; }
}

internal interface IUpdateSchedule
{
    Task WaitForNextCheckAsync(CancellationToken cancellationToken);
}

internal sealed class MissingInstallerException(string version)
    : InvalidOperationException($"Release {version} does not contain the expected AURAScheduler.Setup.exe installer.");

internal sealed class UpdateCheckWorker : BackgroundService
{
    internal const string ExpectedInstallerName = "AURAScheduler.Setup.exe";
    internal static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(24);

    private readonly IUpdateReleaseClient _releaseClient;
    private readonly IUpdateInstaller _installer;
    private readonly IUpdateVersionProvider _versionProvider;
    private readonly IUpdateSchedule _schedule;
    private readonly IOptionsMonitor<LightOptions> _optionsMonitor;
    private readonly ILogger<UpdateCheckWorker> _logger;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private CancellationToken _hostStoppingToken;

    public UpdateCheckWorker(IUpdateReleaseClient releaseClient, IUpdateInstaller installer, IUpdateVersionProvider versionProvider, IUpdateSchedule schedule, IOptionsMonitor<LightOptions> optionsMonitor, ILogger<UpdateCheckWorker> logger)
    {
        _releaseClient = releaseClient;
        _installer = installer;
        _versionProvider = versionProvider;
        _schedule = schedule;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Raised on the worker thread after a check changes state. Handlers must return promptly.
    /// </summary>
    internal event Action<UpdateCheckState>? StateChanged;

    /// <summary>
    /// Runs a user-requested check even when automatic checks are disabled.
    /// </summary>
    internal async Task CheckNowAsync(CancellationToken cancellationToken = default)
    {
        var hostStoppingToken = _hostStoppingToken;
        if (!hostStoppingToken.CanBeCanceled || !cancellationToken.CanBeCanceled)
        {
            var effectiveCancellationToken = hostStoppingToken.CanBeCanceled
                ? hostStoppingToken
                : cancellationToken;
            await ExecuteCheckAsync(UpdateCheckKind.Manual, effectiveCancellationToken).ConfigureAwait(false);
            return;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            hostStoppingToken,
            cancellationToken);
        await ExecuteCheckAsync(UpdateCheckKind.Manual, linkedCancellation.Token).ConfigureAwait(false);
    }

    internal async Task<bool> DownloadAndLaunchInstallerAsync(ReleaseInfo release, CancellationToken cancellationToken = default)
    {
        Publish(UpdateCheckStatus.Downloading, UpdateCheckKind.Manual, release);
        try
        {
            await _installer.DownloadAndLaunchInstallerAsync(
                release,
                () => Publish(UpdateCheckStatus.Installing, UpdateCheckKind.Manual, release),
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The update installer for version {Version} could not be started.", release.Version);
            Publish(UpdateCheckStatus.Failed, UpdateCheckKind.Manual, release, ex);
            return false;
        }
    }

    internal void OpenRelease(ReleaseInfo release) => _installer.OpenRelease(release);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _hostStoppingToken = stoppingToken;

        await ExecuteCheckAsync(UpdateCheckKind.Automatic, stoppingToken).ConfigureAwait(false);

        while (true)
        {
            await _schedule.WaitForNextCheckAsync(stoppingToken).ConfigureAwait(false);
            await ExecuteCheckAsync(UpdateCheckKind.Automatic, stoppingToken).ConfigureAwait(false);
        }
    }

    public override void Dispose()
    {
        _checkGate.Dispose();
        base.Dispose();
    }

    private async Task ExecuteCheckAsync(UpdateCheckKind kind, CancellationToken cancellationToken)
    {
        var entered = false;
        try
        {
            if (kind == UpdateCheckKind.Manual)
            {
                entered = await _checkGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
                if (!entered)
                {
                    _logger.LogInformation(
                        "A manual update check was skipped because another check is already in progress.");
                    Publish(UpdateCheckStatus.Skipped, kind);
                    return;
                }
            }
            else
            {
                await _checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
            }

            Publish(UpdateCheckStatus.Started, kind);

            if (kind == UpdateCheckKind.Automatic && !_optionsMonitor.CurrentValue.CheckForUpdates)
            {
                _logger.LogInformation("Automatic update check skipped because it is disabled in Settings.");
                Publish(UpdateCheckStatus.Skipped, kind);
                return;
            }

            var release = await CheckAsync(cancellationToken).ConfigureAwait(false);
            if (release is null)
            {
                _logger.LogInformation("No update is available after a {CheckKind} check.", kind);
                Publish(UpdateCheckStatus.NoUpdateAvailable, kind);
                return;
            }

            _logger.LogInformation("Update {Version} is available after a {CheckKind} check.", release.Version, kind);

            Publish(UpdateCheckStatus.UpdateAvailable, kind, release);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("The {CheckKind} update check was canceled.", kind);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The {CheckKind} update check failed.", kind);
            Publish(UpdateCheckStatus.Failed, kind, error: ex);
        }
        finally
        {
            if (entered)
                _checkGate.Release();
        }
    }

    private async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken)
    {
        var currentVersion = NormalizeVersion(_versionProvider.CurrentVersion);

        _logger.LogInformation(
            "Checking GitHub releases for updates. Current version: {CurrentVersion}",
            _versionProvider.CurrentVersion);

        var release = await _releaseClient.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (release is null || release.Draft || release.Prerelease)
        {
            _logger.LogInformation("No eligible stable GitHub release was returned.");
            return null;
        }

        var versionText = NormalizeVersion(release.TagName);
        if (!Version.TryParse(versionText, out var latest) || !Version.TryParse(currentVersion, out var current))
        {
            _logger.LogWarning(
                "Could not compare current version {CurrentVersion} with release tag {ReleaseTag}.",
                _versionProvider.CurrentVersion,
                release.TagName);

            return null;
        }

        if (latest <= current)
        {
            _logger.LogInformation("No update is available. Latest release: {LatestVersion}", versionText);
            return null;
        }

        var installer = release.Assets.FirstOrDefault(
            asset => asset.Name.Equals(ExpectedInstallerName, StringComparison.OrdinalIgnoreCase));

        if (installer is null)
            throw new MissingInstallerException(versionText);

        _logger.LogInformation("Update available: version {LatestVersion}.", versionText);

        return new ReleaseInfo(versionText, release.Name, release.Notes, release.Url, installer.DownloadUrl);
    }

    private static string NormalizeVersion(string version) =>
        version.Trim().TrimStart('v', 'V').Split('+', 2)[0];

    private void Publish(UpdateCheckStatus status, UpdateCheckKind kind, ReleaseInfo? release = null, Exception? error = null)
    {
        var state = new UpdateCheckState(status, kind, DateTimeOffset.UtcNow, release, error);
        var handlers = StateChanged;

        if (handlers is null)
            return;

        foreach (Action<UpdateCheckState> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An update-check state handler failed for {Status}.", status);
            }
        }
    }
}

internal sealed class GitHubReleaseClient : IUpdateReleaseClient
{
    private const string Repository = "theYo/aura-scheduler";
    private readonly HttpClient _httpClient = new();

    public GitHubReleaseClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AURA-Scheduler-Update-Checker");
    }

    public async Task<UpdateRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        var release = await _httpClient.GetFromJsonAsync<GitHubReleaseResponse>(
            $"https://api.github.com/repos/{Repository}/releases/latest",
            cancellationToken).ConfigureAwait(false);
        if (release is null)
            return null;

        return new UpdateRelease(
            release.TagName,
            release.Name,
            release.Body,
            release.HtmlUrl,
            release.Draft,
            release.Prerelease,
            release.Assets
                .Select(asset => new UpdateReleaseAsset(asset.Name, asset.BrowserDownloadUrl))
                .ToArray());
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("body")] public string Body { get; set; } = "";
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAssetResponse> Assets { get; set; } = [];
    }

    private sealed class GitHubAssetResponse
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}

internal sealed class UpdateInstaller : IUpdateInstaller
{
    private readonly HttpClient _httpClient = new();
    private readonly ILogger<UpdateInstaller> _logger;

    public UpdateInstaller(ILogger<UpdateInstaller> logger)
    {
        _logger = logger;
    }

    public async Task DownloadAndLaunchInstallerAsync(ReleaseInfo release, Action installerLaunching, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"AURAScheduler.Setup.{release.Version}.exe");
        _logger.LogInformation("Downloading update {Version}.", release.Version);
        using (var source = await _httpClient.GetStreamAsync(release.InstallerUrl, cancellationToken).ConfigureAwait(false))
        using (var destination = File.Create(path))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        installerLaunching();
        _logger.LogInformation("Starting the downloaded update installer {InstallerPath}.", path);
        using var process = Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(path)
        });
        if (process is null)
            throw new InvalidOperationException("Windows did not create a process for the update installer.");

        _logger.LogInformation("Update installer started with process ID {ProcessId}.", process.Id);
    }

    public void OpenRelease(ReleaseInfo release) =>
        Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true });
}

internal sealed class AssemblyVersionProvider : IUpdateVersionProvider
{
    public string CurrentVersion => typeof(UpdateCheckWorker).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
}

internal sealed class UpdateCheckSchedule : IUpdateSchedule
{
    public Task WaitForNextCheckAsync(CancellationToken cancellationToken) =>
        Task.Delay(UpdateCheckWorker.AutomaticCheckInterval, cancellationToken);
}
