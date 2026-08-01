namespace AuraScheduler.UI.Infrastructure;

/// <summary>
/// Identifies whether an update check was initiated by the background schedule or by the user.
/// </summary>
internal enum UpdateCheckKind
{
    Automatic,
    Manual,
}

/// <summary>
/// Describes the outcome or lifecycle point of an update check.
/// </summary>
internal enum UpdateCheckStatus
{
    Started,
    NoUpdateAvailable,
    UpdateAvailable,
    Failed,
    Skipped,
}

/// <summary>
/// Immutable, UI-neutral update-check event payload.
/// </summary>
internal sealed record UpdateCheckState(UpdateCheckStatus Status, UpdateCheckKind Kind, DateTimeOffset OccurredAt, ReleaseInfo? Release = null, Exception? Error = null);
