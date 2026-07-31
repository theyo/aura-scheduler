# Update Check Worker Specification

## Goal

Collapse update scheduling, GitHub release evaluation, and update state publication into one application-facing hosted service named `UpdateCheckWorker`. Keep all WinUI, tray, dialog, installer, and Windows-notification presentation on the UI thread.

## Responsibilities

### `UpdateCheckWorker`

- Derives from `BackgroundService`.
- Performs one automatic check when the host starts.
- Rechecks every 24 hours while the host remains active.
- Reads `LightOptions.CheckForUpdates` before every automatic check.
- Logs when automatic checking is disabled.
- Serializes automatic and manual checks so only one GitHub request can run at once.
- Exposes an asynchronous manual-check method that runs regardless of the automatic-check setting.
- Exposes installer download/launch and release-browser operations to the UI coordinator.
- Publishes immutable state through a UI-neutral event or observable contract:
  - check started;
  - no update available;
  - update available with `ReleaseInfo`;
  - installer downloading with `ReleaseInfo`;
  - installer starting with `ReleaseInfo`;
  - check failed with an exception;
  - automatic check skipped because disabled.
- Never references WinUI, `MainWindow`, `TrayIcon`, `ContentDialog`, `DispatcherQueue`, or Windows app-notification APIs.
- Uses host cancellation and does not own a second application-level cancellation source.
- Publishes `Failed` with `MissingInstallerException` when a newer stable release has no `AURAScheduler.Setup.exe` asset.
- Publishes `Skipped` with `UpdateCheckKind.Manual` when a manual request contends with an active check, without making a second release-client call.

### Injected adapters and seams

- `IUpdateReleaseClient` owns GitHub HTTP and returns the latest release candidate.
- `IUpdateInstaller` owns installer download/launch and opening the release URL.
- `IUpdateVersionProvider` supplies the current application version.
- `IUpdateSchedule` supplies the next automatic-check wait; production waits 24 hours and tests can advance a fake schedule immediately.
- `IOptionsMonitor<LightOptions>` supplies the live `CheckForUpdates` setting.
- The adapters are internal and injected; application code resolves only `UpdateCheckWorker` for checking, downloading, and opening releases.

### `App.xaml.cs`

- Subscribes to background-service state events before the host starts.
- Marshals state handling onto the WinUI dispatcher.
- Stores the currently available `ReleaseInfo`.
- Updates the dashboard banner and tray icon from state changes.
- Changes the dashboard banner and tray tooltip to downloading and installing states while an update is in progress.
- Shows Windows notifications when checks are enabled.
- Handles dialogs, browser navigation, download/install, and notification activation.
- Invokes `UpdateCheckWorker.CheckNowAsync` from the tray command.
- Invokes `UpdateCheckWorker.OpenRelease` and `UpdateCheckWorker.DownloadAndLaunchInstallerAsync` from the release dialog.
- Does not own a periodic timer, update-check semaphore, or update-check cancellation source.
- Unsubscribes during shutdown.

## Manual Check Behavior

- Manual checks run even when automatic checks are disabled.
- Manual no-update and failure states show their existing dialogs.
- Automatic no-update and failure states are logged but do not interrupt the user.
- A successful manual or automatic check may create a Windows notification when `CheckForUpdates` is enabled.
- Downloading publishes `Downloading` before network transfer begins.
- Installer launch publishes `Installing`, requests elevation through the Windows shell, and must return a process before the app exits.
- Download or launch failure publishes `Failed`, restores the available-update presentation, shows an error, and leaves the app running.

## Threading

- Background-service events may be raised from worker threads.
- Subscribers must not block the background service.
- `App.xaml.cs` must dispatch all UI and tray mutations to the UI dispatcher.
- Event payloads must be immutable.

## Lifecycle

- The hosted service starts and stops with the existing generic host.
- Host shutdown cancels the periodic wait and any in-flight automatic request.
- App shutdown continues to unregister Windows notifications and dispose the tray icon.

## Acceptance Criteria

- `App.xaml.cs` contains no `PeriodicTimer`, automatic-check loop, or update-check semaphore.
- Automatic checking still occurs at startup and every 24 hours.
- Disabling update checks prevents automatic requests but not manual checks.
- Existing dashboard, tray badge, release dialog, installer, browser, and Windows-notification behavior remains intact.
- Download/install progress is visible in both the dashboard banner and tray tooltip.
- The app exits only after Windows successfully creates the installer process.
- Concurrent manual and automatic requests do not produce duplicate GitHub calls; the contending manual request publishes `Skipped`.
- UI project builds with zero warnings and errors.
- Existing worker tests pass.
