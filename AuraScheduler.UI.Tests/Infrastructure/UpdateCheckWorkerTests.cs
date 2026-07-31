namespace AuraScheduler.UI.Tests.Infrastructure;

[TestClass]
public sealed class UpdateCheckWorkerTests
{
    [TestMethod]
    public async Task CheckAsync_WhenStableReleaseIsNewer_PublishesUpdateAvailableWithNormalizedVersion()
    {
        var client = new RecordingReleaseClient(CreateRelease("v1.1.0"));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        var update = sink.Single(UpdateCheckStatus.UpdateAvailable, UpdateCheckKind.Manual);
        Assert.AreEqual("1.1.0", update.Release!.Version);
        Assert.AreEqual("AURA Scheduler 1.1.0", update.Release.Name);
        Assert.AreEqual("Release notes", update.Release.Notes);
        Assert.AreEqual("https://github.com/theYo/aura-scheduler/releases/tag/v1.1.0", update.Release.Url);
        Assert.AreEqual("https://downloads.example/setup.exe", update.Release.InstallerUrl);
        Assert.IsNull(update.Error);
    }

    [TestMethod]
    [DataRow("1.1.0", "v1.1.0")]
    [DataRow("1.2.0", "v1.1.0")]
    [DataRow("2.0.0", "V1.9.9")]
    public async Task CheckAsync_WhenReleaseVersionIsEqualOrOlder_PublishesNoUpdateAvailable(string currentVersion, string releaseVersion)
    {
        var client = new RecordingReleaseClient(CreateRelease(releaseVersion));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion);
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        var state = sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.IsNull(state.Release);
        Assert.AreEqual(1, client.CallCount);
        Assert.DoesNotContain(UpdateCheckStatus.UpdateAvailable, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task CheckAsync_WhenCurrentVersionContainsBuildMetadata_ComparesCoreVersionOnly()
    {
        var client = new RecordingReleaseClient(CreateRelease("1.0.0"));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0+ci.123");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.DoesNotContain(UpdateCheckStatus.UpdateAvailable, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    [DataRow("not-a-version", "1.1.0")]
    [DataRow("1.0.0", "release-candidate")]
    public async Task CheckAsync_WhenCurrentOrReleaseVersionIsMalformed_PublishesNoUpdateAvailableAndLogsWarning(string currentVersion, string releaseVersion)
    {
        var logger = new RecordingLoggerProvider();
        var client = new RecordingReleaseClient(CreateRelease(releaseVersion));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion, logger);
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Warning));
        Assert.DoesNotContain(UpdateCheckStatus.Failed, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task CheckAsync_WhenLatestReleaseIsDraft_PublishesNoUpdateAvailable()
    {
        var client = new RecordingReleaseClient(CreateRelease("1.1.0", draft: true));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.DoesNotContain(UpdateCheckStatus.UpdateAvailable, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task CheckAsync_WhenLatestReleaseIsPrerelease_PublishesNoUpdateAvailable()
    {
        var client = new RecordingReleaseClient(CreateRelease("1.1.0", prerelease: true));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.DoesNotContain(UpdateCheckStatus.UpdateAvailable, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task CheckAsync_WhenNewerStableReleaseHasNoExpectedInstaller_PublishesFailedWithMissingInstallerException()
    {
        var logger = new RecordingLoggerProvider();
        var client = new RecordingReleaseClient(CreateRelease("1.1.0", includeInstaller: false));
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", logger);
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        var failed = sink.Single(UpdateCheckStatus.Failed, UpdateCheckKind.Manual);
        Assert.IsNotNull(failed.Error);
        Assert.AreEqual(typeof(MissingInstallerException), failed.Error.GetType());
        Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Warning));
        Assert.DoesNotContain(UpdateCheckStatus.UpdateAvailable, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task CheckAsync_WhenInstallerAssetNameUsesDifferentCase_SelectsExpectedInstaller()
    {
        var release = CreateRelease("1.1.0", installerName: "aurascheduler.setup.exe");
        var client = new RecordingReleaseClient(release);
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        var update = sink.Single(UpdateCheckStatus.UpdateAvailable, UpdateCheckKind.Manual);
        Assert.AreEqual("https://downloads.example/setup.exe", update.Release!.InstallerUrl);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ExecuteAutomaticCheck_WhenCheckForUpdatesIsEnabled_CallsReleaseClientAndPublishesStartedThenOutcome()
    {
        var client = new RecordingReleaseClient(null);
        var sink = new RecordingStateSink();
        var schedule = new ManualSchedule();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", checkForUpdates: true, schedule: schedule);
        worker.StateChanged += sink.Add;

        var startTask = worker.StartAsync(CancellationToken.None);
        await client.WaitForCallAsync(1);
        await sink.WaitForStateAsync(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Automatic);

        var states = sink.Snapshot();
        Assert.AreEqual(UpdateCheckStatus.Started, states[0].Status);
        Assert.AreEqual(UpdateCheckStatus.NoUpdateAvailable, states[1].Status);
        Assert.AreEqual(UpdateCheckKind.Automatic, states[0].Kind);
        Assert.AreEqual(1, client.CallCount);

        await StopWorkerAsync(worker, startTask);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ExecuteAutomaticCheck_WhenCheckForUpdatesIsDisabled_SkipsClientAndPublishesStartedThenSkipped()
    {
        var client = new RecordingReleaseClient(null);
        var sink = new RecordingStateSink();
        var schedule = new ManualSchedule();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", checkForUpdates: false, schedule: schedule);
        worker.StateChanged += sink.Add;

        var startTask = worker.StartAsync(CancellationToken.None);
        await sink.WaitForStateAsync(UpdateCheckStatus.Skipped, UpdateCheckKind.Automatic);

        var states = sink.Snapshot();
        Assert.AreEqual(UpdateCheckStatus.Started, states[0].Status);
        Assert.AreEqual(UpdateCheckStatus.Skipped, states[1].Status);
        Assert.AreEqual(0, client.CallCount);

        await StopWorkerAsync(worker, startTask);
    }

    [TestMethod]
    public async Task CheckNowAsync_WhenCheckForUpdatesIsDisabled_StillChecksAndPublishesManualOutcome()
    {
        var client = new RecordingReleaseClient(null);
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", checkForUpdates: false);
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        sink.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.AreEqual(1, client.CallCount);
        Assert.DoesNotContain(UpdateCheckStatus.Skipped, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    public async Task ExecuteCheck_WhenReleaseClientThrows_PublishesFailedWithOriginalException()
    {
        var expected = new InvalidOperationException("GitHub unavailable");
        var client = new RecordingReleaseClient(null)
        {
            Handler = _ => Task.FromException<UpdateRelease?>(expected),
        };
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;

        await worker.CheckNowAsync();

        var failed = sink.Single(UpdateCheckStatus.Failed, UpdateCheckKind.Manual);
        Assert.AreSame(expected, failed.Error);
        Assert.AreEqual(1, client.CallCount);
    }

    [TestMethod]
    public async Task ExecuteCheck_WhenStateHandlerThrows_ContinuesPublishingAndLogsHandlerFailure()
    {
        var logger = new RecordingLoggerProvider();
        var client = new RecordingReleaseClient(null);
        var observed = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", logger);
        worker.StateChanged += _ => throw new InvalidOperationException("subscriber failure");
        worker.StateChanged += observed.Add;

        await worker.CheckNowAsync();

        observed.Single(UpdateCheckStatus.Started, UpdateCheckKind.Manual);
        observed.Single(UpdateCheckStatus.NoUpdateAvailable, UpdateCheckKind.Manual);
        Assert.IsTrue(logger.Entries.Any(entry => entry.Level == LogLevel.Error));
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CheckNowAsync_WhenAutomaticCheckIsInProgress_PublishesSkippedAndKeepsClientConcurrencyAtOne()
    {
        var releaseFirstCheck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new RecordingReleaseClient(null)
        {
            Handler = async cancellationToken =>
            {
                await releaseFirstCheck.Task.WaitAsync(cancellationToken);
                return null;
            },
        };
        var sink = new RecordingStateSink();
        var schedule = new ManualSchedule();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", checkForUpdates: true, schedule: schedule);
        worker.StateChanged += sink.Add;

        var startTask = worker.StartAsync(CancellationToken.None);
        await client.WaitForCallAsync(1);

        await worker.CheckNowAsync();
        sink.Single(UpdateCheckStatus.Skipped, UpdateCheckKind.Manual);
        Assert.AreEqual(1, client.CallCount);
        Assert.AreEqual(1, client.MaximumActiveCalls);

        releaseFirstCheck.TrySetResult(true);
        await client.WaitForCompletedCallAsync(1);
        await StopWorkerAsync(worker, startTask);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CheckNowAsync_WhenCallerTokenIsCanceled_CancelsReleaseRequestAndDoesNotPublishFailure()
    {
        var client = new RecordingReleaseClient(null);
        client.Handler = async cancellationToken =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    client.CancellationObserved.TrySetResult(true);
                    throw;
                }

                return null;
            };
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(client, currentVersion: "1.0.0");
        worker.StateChanged += sink.Add;
        using var cancellation = new CancellationTokenSource();

        var checkTask = worker.CheckNowAsync(cancellation.Token);
        await client.WaitForCallAsync(1);
        cancellation.Cancel();
        await checkTask;

        await client.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(UpdateCheckStatus.Failed, sink.Snapshot().Select(s => s.Status));
        Assert.Contains(UpdateCheckStatus.Started, sink.Snapshot().Select(s => s.Status));
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ExecuteAsync_WhenStarted_RunsOneAutomaticCheckBeforeWaiting()
    {
        var client = new RecordingReleaseClient(null);
        var schedule = new ManualSchedule();
        schedule.IsFirstCallCompleted = () => client.CompletedCallCount >= 1;
        using var worker = CreateWorker(client, currentVersion: "1.0.0", schedule: schedule);
        var startTask = worker.StartAsync(CancellationToken.None);

        await client.WaitForCompletedCallAsync(1);
        await schedule.WaitForWaitAsync(1);

        Assert.IsTrue(schedule.FirstWaitObservedAfterFirstCallCompleted);
        Assert.AreEqual(1, client.CallCount);

        await StopWorkerAsync(worker, startTask);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ExecuteAsync_WhenSchedulerAdvancesOneInterval_RunsExactlyOneAdditionalAutomaticCheck()
    {
        var client = new RecordingReleaseClient(null);
        var schedule = new ManualSchedule();
        using var worker = CreateWorker(client, currentVersion: "1.0.0", schedule: schedule);
        var startTask = worker.StartAsync(CancellationToken.None);

        await client.WaitForCompletedCallAsync(1);
        await schedule.WaitForWaitAsync(1);
        schedule.Advance();
        await client.WaitForCallAsync(2);
        await client.WaitForCompletedCallAsync(2);

        Assert.AreEqual(2, client.CallCount);
        Assert.AreEqual(2, schedule.WaitCount);

        await StopWorkerAsync(worker, startTask);
    }

    [TestMethod]
    public async Task DownloadAndLaunchInstallerAsync_WhenDownloadBegins_PublishesDownloadingBeforeInstallerWork()
    {
        var installer = new RecordingInstaller();
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(new RecordingReleaseClient(null), currentVersion: "1.0.0", installer: installer);
        worker.StateChanged += sink.Add;
        var release = CreateReleaseInfo();

        installer.Handler = (_, _, _) =>
        {
            Assert.AreEqual(UpdateCheckStatus.Downloading, sink.Snapshot().Last().Status);
            installer.WorkStarted.TrySetResult(true);
            return Task.CompletedTask;
        };

        var result = await worker.DownloadAndLaunchInstallerAsync(release);

        Assert.IsTrue(result);
        await installer.WorkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var downloading = sink.Single(UpdateCheckStatus.Downloading, UpdateCheckKind.Manual);
        Assert.AreSame(release, downloading.Release);
        Assert.IsNull(downloading.Error);
    }

    [TestMethod]
    public async Task DownloadAndLaunchInstallerAsync_WhenInstallerLaunchBegins_PublishesInstallingThroughCallback()
    {
        var installer = new RecordingInstaller
        {
            Handler = (_, installerLaunching, _) =>
            {
                installerLaunching();
                return Task.CompletedTask;
            },
        };
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(new RecordingReleaseClient(null), currentVersion: "1.0.0", installer: installer);
        worker.StateChanged += sink.Add;
        var release = CreateReleaseInfo();

        var result = await worker.DownloadAndLaunchInstallerAsync(release);

        Assert.IsTrue(result);
        var states = sink.Snapshot();
        Assert.AreEqual(UpdateCheckStatus.Downloading, states[0].Status);
        Assert.AreEqual(UpdateCheckStatus.Installing, states[1].Status);
        Assert.AreSame(release, states[1].Release);
        Assert.IsNull(states[1].Error);
        Assert.AreSame(release, installer.Release);
        Assert.IsNotNull(installer.InstallerLaunching);
    }

    [TestMethod]
    public async Task DownloadAndLaunchInstallerAsync_WhenInstallerFails_ReturnsFalseAndPublishesFailedWithoutSuccessfulCompletion()
    {
        var expected = new InvalidOperationException("Installer launch failed.");
        var installer = new RecordingInstaller
        {
            Handler = (_, _, _) => Task.FromException(expected),
        };
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(new RecordingReleaseClient(null), currentVersion: "1.0.0", installer: installer);
        worker.StateChanged += sink.Add;
        var release = CreateReleaseInfo();

        var result = await worker.DownloadAndLaunchInstallerAsync(release);

        Assert.IsFalse(result);
        var states = sink.Snapshot();
        Assert.AreEqual(UpdateCheckStatus.Downloading, states[0].Status);
        Assert.AreEqual(UpdateCheckStatus.Failed, states[^1].Status);
        Assert.AreSame(release, states[^1].Release);
        Assert.AreSame(expected, states[^1].Error);
        Assert.DoesNotContain(UpdateCheckStatus.Installing, states.Select(state => state.Status));
    }

    [TestMethod]
    public async Task DownloadAndLaunchInstallerAsync_WhenInstallerSucceeds_ReturnsTrueAndUsesInjectedInstallerSeam()
    {
        var installer = new RecordingInstaller();
        var sink = new RecordingStateSink();
        using var worker = CreateWorker(new RecordingReleaseClient(null), currentVersion: "1.0.0", installer: installer);
        worker.StateChanged += sink.Add;
        var release = CreateReleaseInfo();

        var result = await worker.DownloadAndLaunchInstallerAsync(release);

        Assert.IsTrue(result);
        Assert.AreSame(release, installer.Release);
        Assert.IsNotNull(installer.InstallerLaunching);
        Assert.AreEqual(1, installer.CallCount);
        Assert.AreEqual(UpdateCheckStatus.Downloading, sink.Snapshot()[0].Status);
    }

    private static UpdateCheckWorker CreateWorker(RecordingReleaseClient client, string currentVersion, RecordingLoggerProvider? logger = null, bool checkForUpdates = true, ManualSchedule? schedule = null, IUpdateInstaller? installer = null)
    {
        logger ??= new RecordingLoggerProvider();
        return new UpdateCheckWorker(
            client,
            installer ?? new NoOpInstaller(),
            new FixedVersionProvider(currentVersion),
            schedule ?? new ManualSchedule(),
            new MutableOptionsMonitor(new LightOptions { CheckForUpdates = checkForUpdates }),
            logger.CreateLogger<UpdateCheckWorker>());
    }

    private static async Task StopWorkerAsync(UpdateCheckWorker worker, Task startTask)
    {
        await worker.StopAsync(CancellationToken.None);
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // BackgroundService exits through the canceled scheduler token.
        }
    }

    private static UpdateRelease CreateRelease(string tagName, bool draft = false, bool prerelease = false, bool includeInstaller = true, string installerName = UpdateCheckWorker.ExpectedInstallerName) =>
        new(
            tagName,
            "AURA Scheduler 1.1.0",
            "Release notes",
            $"https://github.com/theYo/aura-scheduler/releases/tag/{tagName}",
            draft,
            prerelease,
            includeInstaller
                ? [new UpdateReleaseAsset(installerName, "https://downloads.example/setup.exe")]
                : []);

    private static ReleaseInfo CreateReleaseInfo() =>
        new(
            "1.1.0",
            "AURA Scheduler 1.1.0",
            "Release notes",
            "https://github.com/theYo/aura-scheduler/releases/tag/v1.1.0",
            "https://downloads.example/setup.exe");

    private sealed class NoOpInstaller : IUpdateInstaller
    {
        public Task DownloadAndLaunchInstallerAsync(ReleaseInfo release, Action installerLaunching, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void OpenRelease(ReleaseInfo release)
        {
        }
    }

    private sealed class RecordingInstaller : IUpdateInstaller
    {
        public Func<ReleaseInfo, Action, CancellationToken, Task> Handler { get; set; } =
            (_, _, _) => Task.CompletedTask;

        public TaskCompletionSource<bool> WorkStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ReleaseInfo? Release { get; private set; }

        public Action? InstallerLaunching { get; private set; }

        public int CallCount { get; private set; }

        public Task DownloadAndLaunchInstallerAsync(ReleaseInfo release, Action installerLaunching, CancellationToken cancellationToken)
        {
            Release = release;
            InstallerLaunching = installerLaunching;
            CallCount++;
            return Handler(release, installerLaunching, cancellationToken);
        }

        public void OpenRelease(ReleaseInfo release)
        {
        }
    }

    private sealed class FixedVersionProvider(string version) : IUpdateVersionProvider
    {
        public string CurrentVersion { get; } = version;
    }

    private sealed class MutableOptionsMonitor(LightOptions options) : IOptionsMonitor<LightOptions>
    {
        public LightOptions CurrentValue { get; set; } = options;

        public LightOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<LightOptions, string?> listener) => NoOpDisposable.Instance;
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class RecordingReleaseClient(UpdateRelease? result) : IUpdateReleaseClient
    {
        private readonly TaskCompletionSource<bool>[] _started = CreateSignals();
        private readonly TaskCompletionSource<bool>[] _completed = CreateSignals();
        private int _callCount;
        private int _activeCalls;
        private int _maximumActiveCalls;
        private int _completedCallCount;

        public Func<CancellationToken, Task<UpdateRelease?>> Handler { get; set; } =
            _ => Task.FromResult(result);

        public TaskCompletionSource<bool> CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaximumActiveCalls => Volatile.Read(ref _maximumActiveCalls);

        public int CompletedCallCount => Volatile.Read(ref _completedCallCount);

        public Task<UpdateRelease?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            var callNumber = Interlocked.Increment(ref _callCount);
            var active = Interlocked.Increment(ref _activeCalls);
            UpdateMaximum(active);
            _started[callNumber - 1].TrySetResult(true);
            return RunAsync(callNumber, cancellationToken);
        }

        public Task WaitForCallAsync(int callNumber) =>
            _started[callNumber - 1].Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitForCompletedCallAsync(int callNumber) =>
            _completed[callNumber - 1].Task.WaitAsync(TimeSpan.FromSeconds(5));

        private async Task<UpdateRelease?> RunAsync(int callNumber, CancellationToken cancellationToken)
        {
            try
            {
                return await Handler(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
                Interlocked.Increment(ref _completedCallCount);
                _completed[callNumber - 1].TrySetResult(true);
            }
        }

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximumActiveCalls);
                if (active <= current || Interlocked.CompareExchange(ref _maximumActiveCalls, active, current) == current)
                    return;

            }
        }

        private static TaskCompletionSource<bool>[] CreateSignals() =>
            Enumerable.Range(0, 8)
                .Select(_ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
                .ToArray();
    }

    private sealed class ManualSchedule : IUpdateSchedule
    {
        private readonly object _gate = new();
        private readonly List<TaskCompletionSource<bool>> _waits = [];

        public int WaitCount
        {
            get
            {
                lock (_gate)
                    return _waits.Count;
            }
        }

        public bool FirstWaitObservedAfterFirstCallCompleted { get; set; }

        public Func<bool>? IsFirstCallCompleted { get; set; }

        public TaskCompletionSource<bool> WaitStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForNextCheckAsync(CancellationToken cancellationToken)
        {
            var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _waits.Add(wait);
                if (_waits.Count == 1)
                {
                    FirstWaitObservedAfterFirstCallCompleted = IsFirstCallCompleted?.Invoke() == true;
                    WaitStarted.TrySetResult(true);
                }
            }

            return AwaitWaitAsync(wait, cancellationToken);
        }

        public Task WaitForWaitAsync(int waitNumber) =>
            WaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Advance()
        {
            TaskCompletionSource<bool>? wait;
            lock (_gate)
            {
                wait = _waits.Count > 0 && _waits[^1] is { Task.IsCompleted: false }
                    ? _waits[^1]
                    : null;
            }

            wait?.TrySetResult(true);
        }

        private static async Task AwaitWaitAsync(TaskCompletionSource<bool> wait, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => wait.TrySetCanceled(cancellationToken));
            await wait.Task.ConfigureAwait(false);
        }
    }

    private sealed class RecordingStateSink
    {
        private readonly object _gate = new();
        private readonly List<UpdateCheckState> _states = [];
        private readonly Dictionary<(UpdateCheckStatus, UpdateCheckKind), TaskCompletionSource<UpdateCheckState>> _waiters = [];

        public void Add(UpdateCheckState state)
        {
            TaskCompletionSource<UpdateCheckState>? waiter = null;
            lock (_gate)
            {
                _states.Add(state);
                if (_waiters.TryGetValue((state.Status, state.Kind), out waiter))
                    _waiters.Remove((state.Status, state.Kind));
            }

            waiter?.TrySetResult(state);
        }

        public UpdateCheckState Single(UpdateCheckStatus status, UpdateCheckKind kind)
        {
            var matching = Snapshot().Where(state => state.Status == status && state.Kind == kind).ToArray();
            Assert.HasCount(1, matching);
            return matching[0];
        }

        public UpdateCheckState[] Snapshot()
        {
            lock (_gate)
                return _states.ToArray();
        }

        public Task<UpdateCheckState> WaitForStateAsync(UpdateCheckStatus status, UpdateCheckKind kind)
        {
            lock (_gate)
            {
                var existing = _states.FirstOrDefault(state => state.Status == status && state.Kind == kind);
                if (existing is not null)
                    return Task.FromResult(existing);

                var waiter = new TaskCompletionSource<UpdateCheckState>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters[(status, kind)] = waiter;
                return waiter.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        public ILogger<T> CreateLogger<T>() => new RecordingLogger<T>(Entries);
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger(ICollection<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class RecordingLogger<T>(ICollection<LogEntry> entries) : ILogger<T>
    {
        private readonly RecordingLogger _inner = new(entries);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
