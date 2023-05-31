
# aura-scheduler
Uses the Asus AURA SDK to turn LEDs on/off on a schedule

[![.NET](https://github.com/theyo/aura-scheduler/actions/workflows/build.yml/badge.svg)](https://github.com/theyo/aura-scheduler/actions/workflows/build.yml)


# Requirements
The Asus AURA SDK `Interop.AuraServiceLib.dll` needs to be present on the system. Normally this dll is installed with ASUS AURA (through ARMOURY CRATE).

# Instructions
1. Run `AURAScheduler.Setup.msi` to install, the application will be added to startup and begin running
1. AURA Scheduler will minimize to the system tray.
    
    a. To open the application, double-click the icon in teh system tray or right-click on the icon and choose "Show"
    a. To exit the application, right-click on the icon in the system tray and choose "Exit"

# Settings File
You may backup or set your preferred configuration manually in the `LightOptions` section of the `LightSetting.json` file located in `%LOCALAPPDATA%\TheYo\AURA Scheduler`. The configuration can be changed on-the-fly while the app is running.

`LightMode`: `On`, `Off`, or `Schedule`

`Schedule`:

  `LightsOn` Time for the lights to turn on (use Asus AURA configuration)

  `LightsOff` Time for the lights to turn off (sets the LEDs to Black)

Note that times need to be in the format `HH:mm:ss` with the hours portion in 24 hour time. Example: `09:00:00` would be 9:00 in the morning and `14:30:00` would be 2:30 in the afternoon.

Default Settings:
```
"LightOptions": {
    "LightMode": "Schedule",
    "Schedule": {
      "LightsOn": "07:30:00",
      "LightsOff": "21:30:00"
    }
  }
```

# To test the AURA Scheduler project
1. Run the `AuraScheduler.UI` project
1. Set the configuration in the UI and verify the configuration saved in the `LightOptions` section of the `appsettings.json` file. The configuration can be changed on-the-fly while the app is running

# To test the Setup project
Prerequisites:
1. Ensure the Windows Sandbox feature is installed on your maching (press Start and search for `Turn Windows features on or off`)
1. Edit `InstallerSandbox.wsb`

    a. Update the `HostFolder` node with the correct path to the output of the Setup project on your machine

Test the installer
1. Build the Setup project
1. Run `InstallerSandbox.wsb`, Windows Sandbox will start and a few windows will open:

    a. File Explorer open to c:\Program Files
    a. File Explorer open to c:\ProgramData
    a. File Explorer open to the folder with the output from the Setup project
    a. Add/Remove Programs
    a. Services

1. Run the `AURAScheduler.Setup.msi` and verify that the installation succeeded

    a. The application will start, and an error message will be logged due to the worker being unable to start because the `Interop.AuraServiceLib.dll` is not available in the sandbox

1. Uninstall AURA Scheduler and verify that everything is cleaned up properly