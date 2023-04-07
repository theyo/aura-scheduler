# aura-scheduler
Uses the Asus AURA SDK to turn LEDs on/off on a schedule

# Requirements
The Asus AURA SDK `Interop.AuraServiceLib.dll` needs to be present on the system. Normally this dll is installed with ASUS AURA (through ARMOURY CRATE).

# Instructions
Set your preferred configuration in the `LightOptions` section of the `appsettings.json` file. The configuration can be changed on-the-fly while the app is running.

`LightMode`: `On`, `Off`, or `Schedule`

`Schedule`:

  `LightsOn` Time for the lights to turn on (use Asus AURA configuration)

  `LightsOff` Time for the lights to turn off (sets the LEDs to Black)

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
