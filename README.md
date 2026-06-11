
# aura-scheduler

Uses the Asus AURA SDK to turn LEDs on/off on a schedule

[![.NET](https://github.com/theyo/aura-scheduler/actions/workflows/build.yml/badge.svg)](https://github.com/theyo/aura-scheduler/actions/workflows/build.yml)

## Requirements

- Windows 11 (build 22000 or later)
- Asus ARMOURY CRATE (or ASUS AURA) must be installed — this registers the AURA COM service used to control the LEDs
- An internet connection during installation — `AURAScheduler.Setup.exe` always downloads Windows App Runtime 2.1 (~107 MB), and additionally downloads .NET 10 Desktop Runtime (~57 MB) if it isn't already installed

The installer is published as two files:

- `AURAScheduler.Setup.exe` — **recommended**. A small (~25 MB) Burn bootstrapper. It downloads and installs .NET 10 Desktop Runtime only if it isn't already present. Windows App Runtime 2.1 is always downloaded and (re-)run, even if already installed — there's no reliable way to detect it ahead of time, but its installer is a safe no-op when everything is already up to date. Either way, this requires an internet connection.
- `AURAScheduler.Setup.msi` — a small installer containing only AURA Scheduler itself. It does **not** install prerequisites; use it only on machines that already have .NET 10 Desktop Runtime and Windows App Runtime 2.1. If .NET 10 Desktop Runtime is missing, it stops with a message pointing you at `AURAScheduler.Setup.exe` instead (there's no equivalent check for Windows App Runtime 2.1 — see `Setup/Package.wxs` for why).

## Instructions

1. Run `AURAScheduler.Setup.exe` to install

    a. Requires an internet connection: it always downloads and installs Windows App Runtime 2.1 (~107 MB), and also downloads .NET 10 Desktop Runtime (~57 MB) if it isn't already present — this can take a few minutes

1. The application is added to startup and begins running
1. AURA Scheduler will minimize to the system tray.

    a. To open the application, double-click the icon in the system tray or right-click on the icon and choose "Show Window"
    a. To exit the application, right-click on the icon in the system tray and choose "Exit"

## Settings File

You may backup or set your preferred configuration manually in the `LightOptions` section of the `settings.json` file located in `%LOCALAPPDATA%\TheYo\AURA Scheduler`. The configuration can be changed on-the-fly while the app is running.

`LightMode`: `On`, `Off`, or `Schedule`

`Schedule`:

  `LightsOn` Time for the lights to turn on (use Asus AURA configuration)

  `LightsOff` Time for the lights to turn off (sets the LEDs to Black)

`CloseToTray`: `true` or `false` — controls whether closing the main window minimizes to the tray (`true`) or exits the application (`false`). Defaults to `true`.

`StartMinimized`: `true` or `false` — when `true`, the app starts silently in the system tray without showing the main window. Defaults to `false`.

Note that times need to be in the format `HH:mm:ss` with the hours portion in 24 hour time. Example: `09:00:00` would be 9:00 in the morning and `14:30:00` would be 2:30 in the afternoon.

Default Settings:

```json
{
    "LightOptions": {
        "LightMode": "Schedule",
        "Schedule": {
          "LightsOn": "07:30:00",
          "LightsOff": "21:30:00"
        },
        "CloseToTray": true,
        "StartMinimized": false
    }
}
```

## To test the AURA Scheduler project

1. Run the `AuraScheduler.UI` project
1. Set the configuration in the UI and verify the configuration was saved in the `LightOptions` section of `settings.json` in `%LOCALAPPDATA%\TheYo\AURA Scheduler`. The configuration can be changed on-the-fly while the app is running.

## To test the Setup project

Prerequisites:

1. Ensure the Windows Sandbox feature is installed on your machine (press Start and search for `Turn Windows features on or off`)
1. Ensure the sandbox has internet access — it's a clean machine, so `AURAScheduler.Setup.exe` will download and install both .NET 10 Desktop Runtime and Windows App Runtime 2.1 during this test (no internet access is needed on the host to *build* the installer anymore, since neither runtime is embedded)

Test the installer:

1. Build `Setup\Bundle\Bundle.wixproj` (this also builds `AURAScheduler.Setup.msi`, which it bundles)
1. Run `RunWindowsSandbox.cmd`; Windows Sandbox will start and open a few windows:

    a. File Explorer open to `C:\Program Files`
    a. File Explorer open to `C:\Users\WDAGUtilityAccount\AppData\Local`
    a. File Explorer open to `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup`
    a. File Explorer open to the folder with the Burn bundle output (`AURAScheduler.Setup.exe`)
    a. File Explorer open to the folder with the MSI output (`AURAScheduler.Setup.msi`)
    a. Add/Remove Programs
    a. Services

1. Run `AURAScheduler.Setup.exe` and verify that it installs .NET 10 Desktop Runtime, Windows App Runtime 2.1, and AURA Scheduler

    a. The application will start, but an error will be logged because the AURA COM service is not registered in the sandbox (ARMOURY CRATE is not installed there)

1. Uninstall AURA Scheduler and verify that everything is cleaned up properly (the two runtimes are left in place — they're installed as `Permanent`)
1. (Optional) To test the standalone-MSI guard rail, run `AURAScheduler.Setup.msi` directly in a fresh sandbox (before running the `.exe`) — it should stop with a message pointing you at `AURAScheduler.Setup.exe` instead of installing an app that won't run
