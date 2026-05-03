# GTA FSD Cybertruck Autopilot Package

This folder contains the current GTA V ScriptHookVDotNet scripts and configs for the Franklin Cybertruck replacement and waypoint autopilot.

## Contents

- `CybertruckWaypointAutopilot.3.cs`
- `CybertruckWaypointAutopilot.ini`
- `FranklinCybertruckPersonalVehicle.3.cs`
- `FranklinCybertruckPersonalVehicle.ini`
- `GTA5_StateStreamer.3.cs`

Runtime logs and temporary boost snapshot files are intentionally not included.

## Requirements

- GTA V Story Mode installed at `D:\Steam\GTAV`.
- BattleEye disabled before launching modded Story Mode.
- ScriptHookV installed in the GTA V root:
  - `D:\Steam\GTAV\ScriptHookV.dll`
  - `D:\Steam\GTAV\dinput8.dll`
- ScriptHookVDotNet installed in the GTA V root:
  - `D:\Steam\GTAV\ScriptHookVDotNet3.dll`
- OpenIV installed with:
  - ASI Loader
  - OpenIV.ASI
- A `scripts` folder at:
  - `D:\Steam\GTAV\scripts`

## Install The Scripts

Copy these files from this folder into `D:\Steam\GTAV\scripts`:

```text
CybertruckWaypointAutopilot.3.cs
CybertruckWaypointAutopilot.ini
FranklinCybertruckPersonalVehicle.3.cs
FranklinCybertruckPersonalVehicle.ini
GTA5_StateStreamer.3.cs
```

Do not copy `guide.md` into the GTA scripts folder unless you want it there for reference.

## Install The Cybertruck DLC

The Cybertruck add-on DLC must exist here:

```text
D:\Steam\GTAV\mods\update\x64\dlcpacks\CyberTruckV\dlc.rpf
```

Open OpenIV:

1. Select GTA V for Windows.
2. Point OpenIV at `D:\Steam\GTAV`.
3. Install OpenIV.ASI and ASI Loader from `Tools > ASI Manager`.
4. Enable Edit Mode.
5. Open:

```text
D:\Steam\GTAV\mods\update\update.rpf\common\data\dlclist.xml
```

6. Add this line before the closing `</Paths>` tag:

```xml
<Item>dlcpacks:/CyberTruckV/</Item>
```

7. Save the file inside OpenIV.

## Current Behavior

`FranklinCybertruckPersonalVehicle.3.cs` watches for Franklin's default `buffalo2` personal vehicle with plate `FC1988` and replaces it with `CyberTruckV`.

Current personal vehicle config:

```ini
ReplacementModel=CyberTruckV
MatchPlateText=FC1988
PlateText=PARZIVAL
PrimaryColor=4
SecondaryColor=4
PearlescentColor=4
WindowTint=0
DamageMultiplier=4
```

`CybertruckWaypointAutopilot.3.cs` adds waypoint autopilot for Franklin while driving `CyberTruckV`.

Autopilot modes:

```text
Off -> Chill -> MadMax -> Off
```

Controls:

```text
F9     Toggle Franklin Cybertruck swapper
F10    Cycle autopilot modes
Insert Reload ScriptHookVDotNet scripts
```

## Autopilot Notes

Chill mode is regular, non-aggressive driving:

```ini
Chill.SpeedMetersPerSecond=22
Chill.DriverAbility=0.75
Chill.DriverAggression=0.05
Chill.ApplyAutopilotHandlingBoost=false
```

MadMax mode is aggressive and temporarily boosts handling:

```ini
MadMax.SpeedMetersPerSecond=35
MadMax.DriverAbility=1.0
MadMax.DriverAggression=1.0
MadMax.ApplyAutopilotHandlingBoost=true
MadMax.BoostEnginePowerMultiplier=50
MadMax.BoostEngineTorqueMultiplier=50
MadMax.BoostBrakeForce=8
MadMax.BoostSteeringLock=55
```

MadMax boost recovery:

- The script saves a temporary restore snapshot at `D:\Steam\GTAV\scripts\CybertruckWaypointAutopilot.active-boost.ini`.
- That file is created only while MadMax boost is active.
- On normal stop/cancel/reload, the script restores handling and deletes the snapshot.
- Do not include that snapshot in backups unless debugging a stuck boost.

## Path Overlay

The autopilot path overlay is enabled only while autopilot is active.

Current path behavior:

```ini
PredictedPathLengthMode=Exponential
PredictedPathExponentialPower=1.7
PredictedPathExponentialReferenceSpeed=35
PredictedPathMinDistance=0
PredictedPathMaxDistance=20
PredictedPathFollowTerrain=true
PredictedPathAlpha=255
PredictedPathFillAlpha=255
```

The overlay:

- Scales length exponentially with speed.
- Disappears when stopped.
- Caps at `20m`.
- Follows terrain.
- Avoids most tunnel/flyover snapping.
- Flips behind the vehicle while reversing.
- Shows darker moving chevrons only when the path length shrinks.

## How To Use In Game

1. Launch GTA V Story Mode with BattleEye disabled.
2. Switch to Franklin.
3. Make sure the Cybertruck replacement appears for Franklin's personal car.
4. Enter the Cybertruck as driver.
5. Set a waypoint on the map.
6. Press `F10` once for Chill autopilot.
7. Press `F10` again for MadMax autopilot.
8. Press `F10` again to turn autopilot off.

If scripts were changed while GTA is open, press `Insert` to reload ScriptHookVDotNet scripts.

## Troubleshooting

If F10 does nothing:

- Confirm you are Franklin.
- Confirm you are driving `CyberTruckV`.
- Confirm a waypoint is set.
- Check `D:\Steam\GTAV\ScriptHookVDotNet.log`.
- Check `D:\Steam\GTAV\scripts\CybertruckWaypointAutopilot.log`.

If the Cybertruck does not replace Franklin's car:

- Confirm `dlclist.xml` contains `dlcpacks:/CyberTruckV/`.
- Confirm OpenIV.ASI is installed.
- Confirm the DLC exists at `mods\update\x64\dlcpacks\CyberTruckV\dlc.rpf`.
- Confirm `FranklinCybertruckPersonalVehicle.3.cs` and `.ini` are in `D:\Steam\GTAV\scripts`.
- Press `F9` to make sure the swapper is enabled.

If MadMax handling stays boosted:

- Press `Insert` to reload scripts.
- The startup recovery should restore any active boost snapshot.
- If still stuck, exit and restart Story Mode after ensuring this latest script is installed.

## Rollback

To remove the setup:

1. Remove this line from `dlclist.xml` using OpenIV:

```xml
<Item>dlcpacks:/CyberTruckV/</Item>
```

2. Delete:

```text
D:\Steam\GTAV\mods\update\x64\dlcpacks\CyberTruckV
D:\Steam\GTAV\scripts\CybertruckWaypointAutopilot.3.cs
D:\Steam\GTAV\scripts\CybertruckWaypointAutopilot.ini
D:\Steam\GTAV\scripts\FranklinCybertruckPersonalVehicle.3.cs
D:\Steam\GTAV\scripts\FranklinCybertruckPersonalVehicle.ini
```

3. Optionally delete:

```text
D:\Steam\GTAV\scripts\GTA5_StateStreamer.3.cs
```

## For Another Codex To Recreate This

Use these exact paths:

```text
GTA root:     D:\Steam\GTAV
Scripts:      D:\Steam\GTAV\scripts
Cybertruck:   D:\Steam\GTAV\mods\update\x64\dlcpacks\CyberTruckV\dlc.rpf
dlclist.xml:  D:\Steam\GTAV\mods\update\update.rpf\common\data\dlclist.xml
```

Copy the scripts/configs from this folder into `D:\Steam\GTAV\scripts`.

Ensure `dlclist.xml` includes:

```xml
<Item>dlcpacks:/CyberTruckV/</Item>
```

Compile-check the autopilot script with:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:library /out:'D:\Steam\GTAV\scripts\__codex_compilecheck.dll' /r:'D:\Steam\GTAV\ScriptHookVDotNet3.dll' /r:'System.Windows.Forms.dll' 'D:\Steam\GTAV\scripts\CybertruckWaypointAutopilot.3.cs'
```

Then remove temp compile artifacts:

```powershell
Remove-Item 'D:\Steam\GTAV\scripts\__codex_compilecheck.dll','D:\Steam\GTAV\scripts\__codex_compilecheck.pdb' -ErrorAction SilentlyContinue
```

Do not delete or overwrite unrelated user mods. Do not copy logs as source files.
