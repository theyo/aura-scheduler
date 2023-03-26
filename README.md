# aura-scheduler
Uses the Asus Aura SDK to turn LEDs on/off on a schedule

# Requirements
The ASUS Aura SDK `Interop.AuraServiceLib.dll` needs to be present on the system. Normally this dll is installed with ASUS Aura.

# Instructions
Set your preferred configuration in the `LightOptions` section of the `appsettings.json` file. The configuration can be changed on-the-fly while the app is running.

`LightsOn` Controls the lights if `ScheduleEnabled` is set to false

`ScheduleEnabled` Enables the schedule

`Schedule`:

  `LightsOn` Time for the lights to turn on (use Asus Aura configuration)

  `LightsOff` Time for the lights to turn off (sets the LEDs to Black)

Default Settings:
```
"LightOptions": {
    "LightsOn": true,
    "ScheduleEnabled": true,
    "Schedule": {
      "LightsOn": "07:30:00",
      "LightsOff": "21:30:00"
    }
  }
```
