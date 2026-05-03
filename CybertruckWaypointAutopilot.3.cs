using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public sealed class CybertruckWaypointAutopilot : Script
{
    private const string CONFIG_FILE = "CybertruckWaypointAutopilot.ini";
    private const string LOG_FILE = "CybertruckWaypointAutopilot.log";
    private const string ACTIVE_BOOST_FILE = "CybertruckWaypointAutopilot.active-boost.ini";
    private const int CONTROL_GROUP = 0;
    private const int INPUT_VEH_MOVE_LR = 59;
    private const int INPUT_VEH_MOVE_UD = 60;
    private const int INPUT_VEH_ACCELERATE = 71;
    private const int INPUT_VEH_BRAKE = 72;
    private const int INPUT_VEH_HANDBRAKE = 76;
    private const int WAYPOINT_BLIP_ID = 8;
    private const int MONITOR_INTERVAL_MS = 250;
    private const int DRIVING_STYLE_NORMAL = 786603;
    private const int DRIVING_STYLE_RUSHED = 1074528293;
    private const int RAYCAST_INTERSECT_MAP = 1;
    private const int RAYCAST_INTERSECT_OBJECTS = 16;
    private const int RAYCAST_SURFACE_FLAGS = RAYCAST_INTERSECT_MAP | RAYCAST_INTERSECT_OBJECTS;
    private const int RAYCAST_DEFAULT_OPTIONS = 7;

    private bool _enabled = true;
    private Keys _toggleKey = Keys.F10;
    private string _requiredCharacter = "Franklin";
    private int _franklinModel = HashKey("player_one");
    private int _requiredVehicleModel = HashKey("CyberTruckV");
    private readonly AutopilotProfile _chillProfile = new AutopilotProfile("Chill", 22f, 10f, 100f, 10f, DRIVING_STYLE_NORMAL, 0.75f, 0.05f, false, 1f, 1f, 1.3f, 0.5f, 35f, 2.6f, 2.4f, 22f, 0.6f, 1f, 2.2f, 1f);
    private readonly AutopilotProfile _madMaxProfile = new AutopilotProfile("MadMax", 35f, 16f, 140f, 10f, DRIVING_STYLE_RUSHED, 1f, 1f, true, 50f, 50f, 8f, 3f, 55f, 4.2f, 3.8f, 30f, 0.15f, 0.35f, 3.2f, 1.35f);
    private bool _blockVehicleControls = true;
    private bool _showStatus = true;
    private bool _predictedPathEnabled = true;
    private bool _predictedPathAutopilotOnly = true;
    private int _predictedPathSegments = 18;
    private string _predictedPathLengthMode = "Exponential";
    private float _predictedPathLookaheadSeconds = 2.8f;
    private float _predictedPathExponentialPower = 1.7f;
    private float _predictedPathExponentialReferenceSpeed = 35f;
    private float _predictedPathMinDistance = 0f;
    private float _predictedPathMaxDistance = 20f;
    private float _predictedPathHalfWidth = 0.95f;
    private float _predictedPathHeightOffset = 0.18f;
    private int _predictedPathRed = 18;
    private int _predictedPathGreen = 162;
    private int _predictedPathBlue = 230;
    private int _predictedPathAlpha = 255;
    private bool _predictedPathFillEnabled = true;
    private int _predictedPathFillAlpha = 255;
    private bool _predictedPathFollowTerrain = true;
    private float _predictedPathSurfaceProbeAbove = 2.2f;
    private float _predictedPathSurfaceProbeBelow = 8f;
    private float _predictedPathMaxSnapUp = 2.5f;
    private float _predictedPathMaxSnapDown = 6f;
    private float _predictedPathDirectionSpeedThreshold = 0.35f;
    private bool _predictedPathBrakeChevronsEnabled = true;
    private float _predictedPathBrakeDistanceDropThreshold = 0.08f;
    private float _predictedPathBrakeChevronSpacing = 3f;
    private float _predictedPathBrakeChevronLength = 1.25f;
    private float _predictedPathBrakeChevronWidth = 0.72f;
    private float _predictedPathBrakeChevronSpeed = 6f;
    private int _predictedPathBrakeChevronRed = 9;
    private int _predictedPathBrakeChevronGreen = 81;
    private int _predictedPathBrakeChevronBlue = 115;
    private int _predictedPathBrakeChevronAlpha = 245;
    private float _predictedPathBrakeChevronWeightMultiplier = 2.7f;
    private float _predictedPathBrakeChevronStrokeOffset = 0.035f;
    private int _predictedPathBrakeHoldMilliseconds = 450;

    private bool _active;
    private bool _slowTaskIssued;
    private int _activeVehicleHandle;
    private int _nextMonitorAt;
    private Vector3 _destination;
    private AutopilotMode _activeMode = AutopilotMode.Off;
    private AutopilotProfile _activeProfile;
    private readonly HandlingSnapshot _handlingSnapshot = new HandlingSnapshot();
    private float _lastPredictedPathDistance = -1f;
    private int _predictedPathBrakingUntil;
    private StreamWriter _log;

    private enum AutopilotMode
    {
        Off,
        Chill,
        MadMax
    }

    private sealed class AutopilotProfile
    {
        public readonly string Name;
        public float speedMetersPerSecond;
        public float slowSpeedMetersPerSecond;
        public float slowdownDistance;
        public float arrivalRadius;
        public int drivingStyle;
        public float driverAbility;
        public float driverAggression;
        public bool applyHandlingBoost;
        public float boostEnginePowerMultiplier;
        public float boostEngineTorqueMultiplier;
        public float boostBrakeForce;
        public float boostHandBrakeForce;
        public float boostSteeringLock;
        public float boostTractionCurveMax;
        public float boostTractionCurveMin;
        public float boostTractionCurveLateral;
        public float boostLowSpeedTractionLossMultiplier;
        public float boostTractionLossMultiplier;
        public float boostSuspensionForce;
        public float boostTopSpeedMultiplier;

        public AutopilotProfile(
            string name,
            float speedMetersPerSecond,
            float slowSpeedMetersPerSecond,
            float slowdownDistance,
            float arrivalRadius,
            int drivingStyle,
            float driverAbility,
            float driverAggression,
            bool applyHandlingBoost,
            float boostEnginePowerMultiplier,
            float boostEngineTorqueMultiplier,
            float boostBrakeForce,
            float boostHandBrakeForce,
            float boostSteeringLock,
            float boostTractionCurveMax,
            float boostTractionCurveMin,
            float boostTractionCurveLateral,
            float boostLowSpeedTractionLossMultiplier,
            float boostTractionLossMultiplier,
            float boostSuspensionForce,
            float boostTopSpeedMultiplier)
        {
            Name = name;
            this.speedMetersPerSecond = speedMetersPerSecond;
            this.slowSpeedMetersPerSecond = slowSpeedMetersPerSecond;
            this.slowdownDistance = slowdownDistance;
            this.arrivalRadius = arrivalRadius;
            this.drivingStyle = drivingStyle;
            this.driverAbility = driverAbility;
            this.driverAggression = driverAggression;
            this.applyHandlingBoost = applyHandlingBoost;
            this.boostEnginePowerMultiplier = boostEnginePowerMultiplier;
            this.boostEngineTorqueMultiplier = boostEngineTorqueMultiplier;
            this.boostBrakeForce = boostBrakeForce;
            this.boostHandBrakeForce = boostHandBrakeForce;
            this.boostSteeringLock = boostSteeringLock;
            this.boostTractionCurveMax = boostTractionCurveMax;
            this.boostTractionCurveMin = boostTractionCurveMin;
            this.boostTractionCurveLateral = boostTractionCurveLateral;
            this.boostLowSpeedTractionLossMultiplier = boostLowSpeedTractionLossMultiplier;
            this.boostTractionLossMultiplier = boostTractionLossMultiplier;
            this.boostSuspensionForce = boostSuspensionForce;
            this.boostTopSpeedMultiplier = boostTopSpeedMultiplier;
        }
    }

    private sealed class HandlingSnapshot
    {
        public bool valid;
        public int vehicleHandle;
        public int modelHash;
        public string plateText = string.Empty;
        public bool hasPosition;
        public Vector3 position;
        public float initialDriveForce;
        public float initialDriveMaxFlatVelocity;
        public float brakeForce;
        public float handBrakeForce;
        public float steeringLock;
        public float tractionCurveMax;
        public float tractionCurveMin;
        public float tractionCurveLateral;
        public float lowSpeedTractionLossMultiplier;
        public float tractionLossMultiplier;
        public float suspensionForce;

        public void Clear()
        {
            valid = false;
            vehicleHandle = 0;
            modelHash = 0;
            plateText = string.Empty;
            hasPosition = false;
            position = Vector3.Zero;
        }
    }

    public CybertruckWaypointAutopilot()
    {
        OpenLog();
        LoadConfig();
        RestorePendingAutopilotHandlingBoost();
        RepairBoostedCybertrucksWithoutSnapshot();

        Interval = 0;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;

        Log("Loaded. Enabled=" + _enabled + " ToggleKey=" + _toggleKey + " VehicleModel=" + _requiredVehicleModel);
        ShowStatus("Cybertruck waypoint autopilot loaded");
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != _toggleKey) return;

        if (_active)
        {
            if (_activeMode == AutopilotMode.Chill)
            {
                SwitchAutopilotProfile(_madMaxProfile, AutopilotMode.MadMax);
                return;
            }

            StopAutopilot("Cybertruck autopilot cancelled", true);
            return;
        }

        StartAutopilot(_chillProfile, AutopilotMode.Chill);
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!_active) return;

        if (_blockVehicleControls)
        {
            DisableDrivingControls();
        }

        DrawPredictedPathIfNeeded();

        int now = Game.GameTime;
        if (now < _nextMonitorAt) return;
        _nextMonitorAt = now + MONITOR_INTERVAL_MS;

        Ped player = Game.Player.Character;
        if (player == null || !player.Exists())
        {
            StopAutopilot("Autopilot stopped: no player", false);
            return;
        }

        int currentVehicle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
        if (currentVehicle == 0 || currentVehicle != _activeVehicleHandle)
        {
            StopAutopilot("Autopilot stopped: left Cybertruck", true);
            return;
        }

        if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, currentVehicle, -1) != player.Handle)
        {
            StopAutopilot("Autopilot stopped: not driving", true);
            return;
        }

        if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE))
        {
            StopAutopilot("Autopilot stopped: waypoint removed", true);
            return;
        }

        AutopilotProfile profile = CurrentProfile();
        float distance = Dist2D(player.Position, _destination);
        if (distance <= profile.arrivalRadius)
        {
            StopAutopilot("Autopilot arrived", true);
            return;
        }

        if (!_slowTaskIssued && distance <= profile.slowdownDistance)
        {
            IssueDriveTask(player, currentVehicle, profile.slowSpeedMetersPerSecond, profile);
            _slowTaskIssued = true;
            Log("Slowdown task issued. Profile=" + profile.Name + " distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }

    private void StartAutopilot(AutopilotProfile profile, AutopilotMode mode)
    {
        if (!_enabled)
        {
            ShowStatus("Cybertruck autopilot disabled");
            return;
        }

        Ped player = Game.Player.Character;
        if (player == null || !player.Exists())
        {
            ShowStatus("Cybertruck autopilot: player not found");
            return;
        }

        if (!CharacterAllowed(player))
        {
            ShowStatus("Cybertruck autopilot works only for Franklin");
            return;
        }

        int vehicle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
        if (vehicle == 0)
        {
            ShowStatus("Cybertruck autopilot: enter the Cybertruck first");
            return;
        }

        if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle, -1) != player.Handle)
        {
            ShowStatus("Cybertruck autopilot: driver seat required");
            return;
        }

        if (Function.Call<int>(Hash.GET_ENTITY_MODEL, vehicle) != _requiredVehicleModel)
        {
            ShowStatus("Cybertruck autopilot works only in CyberTruckV");
            return;
        }

        Vector3 waypoint;
        if (!TryGetWaypoint(out waypoint))
        {
            ShowStatus("Cybertruck autopilot: set a waypoint first");
            return;
        }

        _active = true;
        _slowTaskIssued = false;
        _activeVehicleHandle = vehicle;
        _destination = waypoint;
        _nextMonitorAt = 0;
        _activeMode = mode;
        _activeProfile = profile;
        ResetPredictedPathBrakeState();

        ApplyAutopilotHandlingBoost(vehicle, profile);
        IssueDriveTask(player, vehicle, profile.speedMetersPerSecond, profile);
        ShowStatus("Cybertruck autopilot: " + profile.Name);
        Log("Started. Profile=" + profile.Name + " Destination=" + FormatPosition(_destination) + " speed=" + profile.speedMetersPerSecond.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void SwitchAutopilotProfile(AutopilotProfile profile, AutopilotMode mode)
    {
        Ped player = Game.Player.Character;
        if (player == null || !player.Exists())
        {
            StopAutopilot("Autopilot stopped: no player", false);
            return;
        }

        int vehicle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
        if (vehicle == 0 || vehicle != _activeVehicleHandle)
        {
            StopAutopilot("Autopilot stopped: left Cybertruck", true);
            return;
        }

        if (Function.Call<int>(Hash.GET_PED_IN_VEHICLE_SEAT, vehicle, -1) != player.Handle)
        {
            StopAutopilot("Autopilot stopped: not driving", true);
            return;
        }

        Vector3 waypoint;
        if (!TryGetWaypoint(out waypoint))
        {
            StopAutopilot("Autopilot stopped: waypoint removed", true);
            return;
        }

        _destination = waypoint;
        RestoreAutopilotHandling();
        _activeMode = mode;
        _activeProfile = profile;
        _slowTaskIssued = false;
        _nextMonitorAt = 0;
        ResetPredictedPathBrakeState();

        ApplyAutopilotHandlingBoost(vehicle, profile);
        IssueDriveTask(player, vehicle, profile.speedMetersPerSecond, profile);
        ShowStatus("Cybertruck autopilot: " + profile.Name);
        Log("Switched. Profile=" + profile.Name + " Destination=" + FormatPosition(_destination) + " speed=" + profile.speedMetersPerSecond.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void IssueDriveTask(Ped driver, int vehicleHandle, float speed, AutopilotProfile profile)
    {
        Function.Call(Hash.SET_DRIVER_ABILITY, driver.Handle, Clamp01(profile.driverAbility));
        Function.Call(Hash.SET_DRIVER_AGGRESSIVENESS, driver.Handle, Clamp01(profile.driverAggression));
        Function.Call(
            Hash.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE,
            driver.Handle,
            vehicleHandle,
            _destination.X,
            _destination.Y,
            _destination.Z,
            speed,
            profile.drivingStyle,
            profile.arrivalRadius);
    }

    private void StopAutopilot(string message, bool notify)
    {
        RestoreAutopilotHandling();

        Ped player = Game.Player.Character;
        if (player != null && player.Exists())
        {
            Function.Call(Hash.CLEAR_PED_TASKS, player.Handle);
        }

        _active = false;
        _slowTaskIssued = false;
        _activeVehicleHandle = 0;
        _nextMonitorAt = 0;
        _activeMode = AutopilotMode.Off;
        _activeProfile = null;
        ResetPredictedPathBrakeState();

        if (notify) ShowStatus(message);
        Log(message);
    }

    private AutopilotProfile CurrentProfile()
    {
        return _activeProfile ?? _chillProfile;
    }

    private void ApplyAutopilotHandlingBoost(int vehicleHandle, AutopilotProfile profile)
    {
        if (!profile.applyHandlingBoost) return;

        RestorePendingAutopilotHandlingBoost();

        Vehicle vehicle = GetVehicleByHandle(vehicleHandle);
        if (vehicle == null || !vehicle.Exists())
        {
            Log("Handling boost skipped: vehicle not found.");
            return;
        }

        try
        {
            _handlingSnapshot.Clear();
            CaptureCurrentHandlingSnapshot(vehicle, vehicleHandle, _handlingSnapshot);
            if (LooksLikeBoostedHandling(vehicle, profile) && TryCaptureCleanModelHandlingSnapshot(vehicle, _handlingSnapshot))
            {
                Log("Captured clean Cybertruck baseline because current handling already looked boosted.");
            }

            if (!SaveActiveBoostSnapshot(_handlingSnapshot))
            {
                _handlingSnapshot.Clear();
                Log("Handling boost aborted: could not persist restore snapshot.");
                return;
            }

            if (vehicle.HandlingData != null)
            {
                vehicle.HandlingData.InitialDriveForce = Math.Max(vehicle.HandlingData.InitialDriveForce, 0.75f);
                vehicle.HandlingData.InitialDriveMaxFlatVelocity = Math.Max(vehicle.HandlingData.InitialDriveMaxFlatVelocity, 280f);
                vehicle.HandlingData.BrakeForce = profile.boostBrakeForce;
                vehicle.HandlingData.HandBrakeForce = profile.boostHandBrakeForce;
                vehicle.HandlingData.SteeringLock = profile.boostSteeringLock;
                vehicle.HandlingData.TractionCurveMax = profile.boostTractionCurveMax;
                vehicle.HandlingData.TractionCurveMin = profile.boostTractionCurveMin;
                vehicle.HandlingData.TractionCurveLateral = profile.boostTractionCurveLateral;
                vehicle.HandlingData.LowSpeedTractionLossMultiplier = profile.boostLowSpeedTractionLossMultiplier;
                vehicle.HandlingData.TractionLossMultiplier = profile.boostTractionLossMultiplier;
                vehicle.HandlingData.SuspensionForce = profile.boostSuspensionForce;
            }

            Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicleHandle, profile.boostEnginePowerMultiplier);
            vehicle.EngineTorqueMultiplier = profile.boostEngineTorqueMultiplier;
            Function.Call(Hash.MODIFY_VEHICLE_TOP_SPEED, vehicleHandle, profile.boostTopSpeedMultiplier);
            Log("Applied autopilot handling boost. Profile=" + profile.Name);
        }
        catch (Exception ex)
        {
            RestoreAutopilotHandling();
            Log("Handling boost failed: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    private void RestoreAutopilotHandling()
    {
        if (_handlingSnapshot.valid)
        {
            RestoreAutopilotHandlingFromSnapshot(_handlingSnapshot, "memory");
            return;
        }

        RestorePendingAutopilotHandlingBoost();
    }

    private bool RestorePendingAutopilotHandlingBoost()
    {
        HandlingSnapshot pending;
        if (!TryLoadActiveBoostSnapshot(out pending)) return false;
        return RestoreAutopilotHandlingFromSnapshot(pending, "persistent snapshot");
    }

    private void RepairBoostedCybertrucksWithoutSnapshot()
    {
        if (File.Exists(GetActiveBoostSnapshotPath())) return;

        int repaired = 0;
        foreach (Vehicle vehicle in World.GetAllVehicles())
        {
            if (vehicle == null || !vehicle.Exists()) continue;
            if (Function.Call<int>(Hash.GET_ENTITY_MODEL, vehicle.Handle) != _requiredVehicleModel) continue;
            if (!LooksLikeBoostedHandling(vehicle, _madMaxProfile)) continue;

            HandlingSnapshot cleanSnapshot = new HandlingSnapshot();
            CaptureCurrentHandlingSnapshot(vehicle, vehicle.Handle, cleanSnapshot);
            if (!TryCaptureCleanModelHandlingSnapshot(vehicle, cleanSnapshot))
            {
                Log("Startup boosted-handling repair skipped: clean baseline unavailable.");
                continue;
            }

            if (RestoreAutopilotHandlingFromSnapshot(cleanSnapshot, "clean Cybertruck baseline"))
            {
                repaired++;
            }
        }

        if (repaired > 0)
        {
            Log("Startup repaired boosted Cybertruck handling count=" + repaired.ToString(CultureInfo.InvariantCulture));
        }
    }

    private bool RestoreAutopilotHandlingFromSnapshot(HandlingSnapshot snapshot, string source)
    {
        if (!snapshot.valid) return false;

        Vehicle vehicle = ResolveSnapshotVehicle(snapshot);
        if (vehicle == null || !vehicle.Exists())
        {
            _handlingSnapshot.Clear();
            Log("Handling restore skipped: vehicle not found for " + source + ".");
            return false;
        }

        try
        {
            Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicle.Handle, 1f);
            vehicle.EngineTorqueMultiplier = 1f;
            Function.Call(Hash.MODIFY_VEHICLE_TOP_SPEED, vehicle.Handle, -1f);

            if (vehicle.HandlingData != null)
            {
                vehicle.HandlingData.InitialDriveForce = snapshot.initialDriveForce;
                vehicle.HandlingData.InitialDriveMaxFlatVelocity = snapshot.initialDriveMaxFlatVelocity;
                vehicle.HandlingData.BrakeForce = snapshot.brakeForce;
                vehicle.HandlingData.HandBrakeForce = snapshot.handBrakeForce;
                vehicle.HandlingData.SteeringLock = snapshot.steeringLock;
                vehicle.HandlingData.TractionCurveMax = snapshot.tractionCurveMax;
                vehicle.HandlingData.TractionCurveMin = snapshot.tractionCurveMin;
                vehicle.HandlingData.TractionCurveLateral = snapshot.tractionCurveLateral;
                vehicle.HandlingData.LowSpeedTractionLossMultiplier = snapshot.lowSpeedTractionLossMultiplier;
                vehicle.HandlingData.TractionLossMultiplier = snapshot.tractionLossMultiplier;
                vehicle.HandlingData.SuspensionForce = snapshot.suspensionForce;
            }

            DeleteActiveBoostSnapshot();
            Log("Restored autopilot handling boost from " + source + ".");
            return true;
        }
        catch (Exception ex)
        {
            Log("Handling restore failed: " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
        finally
        {
            _handlingSnapshot.Clear();
        }
    }

    private void CaptureCurrentHandlingSnapshot(Vehicle vehicle, int vehicleHandle, HandlingSnapshot snapshot)
    {
        snapshot.Clear();
        snapshot.valid = true;
        snapshot.vehicleHandle = vehicleHandle;
        snapshot.modelHash = Function.Call<int>(Hash.GET_ENTITY_MODEL, vehicleHandle);
        snapshot.plateText = GetVehiclePlateText(vehicle);
        snapshot.hasPosition = true;
        snapshot.position = vehicle.Position;
        CopyHandlingFields(vehicle, snapshot);
    }

    private void CopyHandlingFields(Vehicle vehicle, HandlingSnapshot snapshot)
    {
        if (vehicle == null || !vehicle.Exists() || vehicle.HandlingData == null) return;

        snapshot.initialDriveForce = vehicle.HandlingData.InitialDriveForce;
        snapshot.initialDriveMaxFlatVelocity = vehicle.HandlingData.InitialDriveMaxFlatVelocity;
        snapshot.brakeForce = vehicle.HandlingData.BrakeForce;
        snapshot.handBrakeForce = vehicle.HandlingData.HandBrakeForce;
        snapshot.steeringLock = vehicle.HandlingData.SteeringLock;
        snapshot.tractionCurveMax = vehicle.HandlingData.TractionCurveMax;
        snapshot.tractionCurveMin = vehicle.HandlingData.TractionCurveMin;
        snapshot.tractionCurveLateral = vehicle.HandlingData.TractionCurveLateral;
        snapshot.lowSpeedTractionLossMultiplier = vehicle.HandlingData.LowSpeedTractionLossMultiplier;
        snapshot.tractionLossMultiplier = vehicle.HandlingData.TractionLossMultiplier;
        snapshot.suspensionForce = vehicle.HandlingData.SuspensionForce;
    }

    private bool LooksLikeBoostedHandling(Vehicle vehicle, AutopilotProfile profile)
    {
        if (vehicle == null || !vehicle.Exists() || vehicle.HandlingData == null) return false;

        return vehicle.HandlingData.BrakeForce >= profile.boostBrakeForce * 0.95f ||
               vehicle.HandlingData.HandBrakeForce >= profile.boostHandBrakeForce * 0.95f ||
               vehicle.HandlingData.SteeringLock >= profile.boostSteeringLock * 0.95f ||
               vehicle.HandlingData.TractionCurveMax >= profile.boostTractionCurveMax * 0.95f ||
               vehicle.HandlingData.InitialDriveMaxFlatVelocity >= 250f;
    }

    private bool TryCaptureCleanModelHandlingSnapshot(Vehicle targetVehicle, HandlingSnapshot snapshot)
    {
        Vehicle sample = null;
        try
        {
            Model model = new Model(_requiredVehicleModel);
            model.Request(1000);
            if (!model.IsLoaded) return false;

            Vector3 samplePosition = targetVehicle.Position + new Vector3(0f, 0f, -180f);
            sample = World.CreateVehicle(model, samplePosition, targetVehicle.Heading);
            if (sample == null || !sample.Exists() || sample.HandlingData == null) return false;

            CopyHandlingFields(sample, snapshot);
            return true;
        }
        catch (Exception ex)
        {
            Log("Clean handling baseline capture failed: " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
        finally
        {
            try
            {
                if (sample != null && sample.Exists()) sample.Delete();
            }
            catch { }
        }
    }

    private bool SaveActiveBoostSnapshot(HandlingSnapshot snapshot)
    {
        if (!snapshot.valid) return false;

        try
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Version=1");
            builder.AppendLine("VehicleHandle=" + snapshot.vehicleHandle.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("ModelHash=" + snapshot.modelHash.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("PlateText=" + (snapshot.plateText ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty));
            builder.AppendLine("HasPosition=" + snapshot.hasPosition.ToString(CultureInfo.InvariantCulture));
            AppendSnapshotFloat(builder, "PositionX", snapshot.position.X);
            AppendSnapshotFloat(builder, "PositionY", snapshot.position.Y);
            AppendSnapshotFloat(builder, "PositionZ", snapshot.position.Z);
            AppendSnapshotFloat(builder, "InitialDriveForce", snapshot.initialDriveForce);
            AppendSnapshotFloat(builder, "InitialDriveMaxFlatVelocity", snapshot.initialDriveMaxFlatVelocity);
            AppendSnapshotFloat(builder, "BrakeForce", snapshot.brakeForce);
            AppendSnapshotFloat(builder, "HandBrakeForce", snapshot.handBrakeForce);
            AppendSnapshotFloat(builder, "SteeringLock", snapshot.steeringLock);
            AppendSnapshotFloat(builder, "TractionCurveMax", snapshot.tractionCurveMax);
            AppendSnapshotFloat(builder, "TractionCurveMin", snapshot.tractionCurveMin);
            AppendSnapshotFloat(builder, "TractionCurveLateral", snapshot.tractionCurveLateral);
            AppendSnapshotFloat(builder, "LowSpeedTractionLossMultiplier", snapshot.lowSpeedTractionLossMultiplier);
            AppendSnapshotFloat(builder, "TractionLossMultiplier", snapshot.tractionLossMultiplier);
            AppendSnapshotFloat(builder, "SuspensionForce", snapshot.suspensionForce);
            File.WriteAllText(GetActiveBoostSnapshotPath(), builder.ToString(), Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Log("Failed to save active boost snapshot: " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
    }

    private bool TryLoadActiveBoostSnapshot(out HandlingSnapshot snapshot)
    {
        snapshot = new HandlingSnapshot();
        string path = GetActiveBoostSnapshotPath();
        if (!File.Exists(path)) return false;

        try
        {
            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int equals = line.IndexOf('=');
                if (equals <= 0) continue;

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();

                if (key.Equals("VehicleHandle", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.vehicleHandle = ParseInt(value, snapshot.vehicleHandle);
                }
                else if (key.Equals("ModelHash", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.modelHash = ParseInt(value, snapshot.modelHash);
                }
                else if (key.Equals("PlateText", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.plateText = value;
                }
                else if (key.Equals("HasPosition", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.hasPosition = ParseBool(value, snapshot.hasPosition);
                }
                else if (key.Equals("PositionX", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.position.X = ParseFloat(value, snapshot.position.X);
                }
                else if (key.Equals("PositionY", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.position.Y = ParseFloat(value, snapshot.position.Y);
                }
                else if (key.Equals("PositionZ", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.position.Z = ParseFloat(value, snapshot.position.Z);
                }
                else if (key.Equals("InitialDriveForce", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.initialDriveForce = ParseFloat(value, snapshot.initialDriveForce);
                }
                else if (key.Equals("InitialDriveMaxFlatVelocity", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.initialDriveMaxFlatVelocity = ParseFloat(value, snapshot.initialDriveMaxFlatVelocity);
                }
                else if (key.Equals("BrakeForce", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.brakeForce = ParseFloat(value, snapshot.brakeForce);
                }
                else if (key.Equals("HandBrakeForce", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.handBrakeForce = ParseFloat(value, snapshot.handBrakeForce);
                }
                else if (key.Equals("SteeringLock", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.steeringLock = ParseFloat(value, snapshot.steeringLock);
                }
                else if (key.Equals("TractionCurveMax", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.tractionCurveMax = ParseFloat(value, snapshot.tractionCurveMax);
                }
                else if (key.Equals("TractionCurveMin", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.tractionCurveMin = ParseFloat(value, snapshot.tractionCurveMin);
                }
                else if (key.Equals("TractionCurveLateral", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.tractionCurveLateral = ParseFloat(value, snapshot.tractionCurveLateral);
                }
                else if (key.Equals("LowSpeedTractionLossMultiplier", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.lowSpeedTractionLossMultiplier = ParseFloat(value, snapshot.lowSpeedTractionLossMultiplier);
                }
                else if (key.Equals("TractionLossMultiplier", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.tractionLossMultiplier = ParseFloat(value, snapshot.tractionLossMultiplier);
                }
                else if (key.Equals("SuspensionForce", StringComparison.OrdinalIgnoreCase))
                {
                    snapshot.suspensionForce = ParseFloat(value, snapshot.suspensionForce);
                }
            }

            snapshot.valid = true;
            if (snapshot.modelHash == 0) snapshot.modelHash = _requiredVehicleModel;
            return true;
        }
        catch (Exception ex)
        {
            Log("Failed to load active boost snapshot: " + ex.GetType().Name + " " + ex.Message);
            return false;
        }
    }

    private Vehicle ResolveSnapshotVehicle(HandlingSnapshot snapshot)
    {
        Vehicle vehicle = GetVehicleByHandle(snapshot.vehicleHandle);
        if (SnapshotVehicleMatches(vehicle, snapshot, false)) return vehicle;

        Ped player = Game.Player.Character;
        if (player != null && player.Exists())
        {
            int currentVehicle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
            vehicle = GetVehicleByHandle(currentVehicle);
            if (SnapshotVehicleMatches(vehicle, snapshot, false)) return vehicle;
        }

        Vehicle firstModelMatch = null;
        Vehicle nearestModelMatch = null;
        float nearestDistance = float.MaxValue;

        foreach (Vehicle candidate in World.GetAllVehicles())
        {
            if (!SnapshotVehicleMatches(candidate, snapshot, false)) continue;
            if (firstModelMatch == null) firstModelMatch = candidate;
            if (SnapshotVehicleMatches(candidate, snapshot, true)) return candidate;

            if (snapshot.hasPosition)
            {
                float distance = DistanceSquared(candidate.Position, snapshot.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestModelMatch = candidate;
                }
            }
        }

        return nearestModelMatch ?? firstModelMatch;
    }

    private bool SnapshotVehicleMatches(Vehicle vehicle, HandlingSnapshot snapshot, bool requirePlate)
    {
        if (vehicle == null || !vehicle.Exists()) return false;
        int expectedModel = snapshot.modelHash != 0 ? snapshot.modelHash : _requiredVehicleModel;
        if (Function.Call<int>(Hash.GET_ENTITY_MODEL, vehicle.Handle) != expectedModel) return false;

        if (requirePlate && !string.IsNullOrWhiteSpace(snapshot.plateText))
        {
            return NormalizePlate(GetVehiclePlateText(vehicle)) == NormalizePlate(snapshot.plateText);
        }

        return true;
    }

    private string GetVehiclePlateText(Vehicle vehicle)
    {
        if (vehicle == null || !vehicle.Exists()) return string.Empty;

        try
        {
            return Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void DeleteActiveBoostSnapshot()
    {
        try
        {
            string path = GetActiveBoostSnapshotPath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Log("Failed to delete active boost snapshot: " + ex.GetType().Name + " " + ex.Message);
        }
    }

    private string GetActiveBoostSnapshotPath()
    {
        return Path.Combine(GetScriptsDirectory(), ACTIVE_BOOST_FILE);
    }

    private Vehicle GetVehicleByHandle(int handle)
    {
        foreach (Vehicle vehicle in World.GetAllVehicles())
        {
            if (vehicle != null && vehicle.Exists() && vehicle.Handle == handle)
            {
                return vehicle;
            }
        }

        return null;
    }

    private bool CharacterAllowed(Ped player)
    {
        if (string.IsNullOrWhiteSpace(_requiredCharacter)) return true;
        if (_requiredCharacter.Equals("Any", StringComparison.OrdinalIgnoreCase)) return true;
        if (_requiredCharacter.Equals("Franklin", StringComparison.OrdinalIgnoreCase))
        {
            return Function.Call<int>(Hash.GET_ENTITY_MODEL, player.Handle) == _franklinModel;
        }

        return true;
    }

    private bool TryGetWaypoint(out Vector3 waypoint)
    {
        waypoint = Vector3.Zero;
        if (!Function.Call<bool>(Hash.IS_WAYPOINT_ACTIVE)) return false;

        int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, WAYPOINT_BLIP_ID);
        if (blip == 0) return false;

        waypoint = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
        waypoint.Z = ResolveWaypointZ(waypoint);
        return true;
    }

    private float ResolveWaypointZ(Vector3 waypoint)
    {
        try
        {
            var groundZ = new OutputArgument();
            bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, waypoint.X, waypoint.Y, 1000f, groundZ, false);
            if (found) return groundZ.GetResult<float>();
        }
        catch { }

        Ped player = Game.Player.Character;
        return player != null && player.Exists() ? player.Position.Z : waypoint.Z;
    }

    private void DisableDrivingControls()
    {
        Function.Call(Hash.DISABLE_CONTROL_ACTION, CONTROL_GROUP, INPUT_VEH_MOVE_LR, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, CONTROL_GROUP, INPUT_VEH_MOVE_UD, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, CONTROL_GROUP, INPUT_VEH_ACCELERATE, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, CONTROL_GROUP, INPUT_VEH_BRAKE, true);
        Function.Call(Hash.DISABLE_CONTROL_ACTION, CONTROL_GROUP, INPUT_VEH_HANDBRAKE, true);
    }

    private void DrawPredictedPathIfNeeded()
    {
        if (!_predictedPathEnabled) return;
        if (_predictedPathAutopilotOnly && !_active) return;

        Vehicle vehicle = GetVehicleByHandle(_activeVehicleHandle);
        if (vehicle == null || !vehicle.Exists()) return;

        Vector3 facingForward = Function.Call<Vector3>(Hash.GET_ENTITY_FORWARD_VECTOR, vehicle.Handle);
        facingForward.Z = 0f;
        if (facingForward.Length() < 0.001f)
        {
            return;
        }
        facingForward.Normalize();

        Vector3 velocity = Function.Call<Vector3>(Hash.GET_ENTITY_VELOCITY, vehicle.Handle);
        velocity.Z = 0f;
        float signedSpeed = Dot(velocity, facingForward);
        Vector3 pathForward = signedSpeed < -Math.Max(0.01f, _predictedPathDirectionSpeedThreshold)
            ? new Vector3(-facingForward.X, -facingForward.Y, 0f)
            : facingForward;

        Vector3 right = Vector3.Cross(Vector3.WorldUp, pathForward);
        if (right.Length() < 0.001f)
        {
            right = new Vector3(-1f, 0f, 0f);
        }
        else
        {
            right.Normalize();
        }

        float speed = Function.Call<float>(Hash.GET_ENTITY_SPEED, vehicle.Handle);
        float distance = CalculatePredictedPathDistance(speed);
        if (distance < 0.05f)
        {
            ResetPredictedPathBrakeState();
            return;
        }

        bool braking = UpdatePredictedPathBraking(distance);
        int segments = Math.Max(3, _predictedPathSegments);
        float yawRate = GetYawRate(vehicle.Handle);
        float curvature = Math.Abs(speed) > 0.5f ? Clamp(yawRate / Math.Max(speed, 0.5f), -0.12f, 0.12f) : 0f;

        Vector3 origin = vehicle.Position + pathForward * 2.4f;
        float fallbackDrawZ = vehicle.Position.Z + _predictedPathHeightOffset;
        Vector3 prevLeft;
        Vector3 prevRight;
        BuildPathEdges(origin, right, vehicle.Handle, fallbackDrawZ, out prevLeft, out prevRight);
        fallbackDrawZ = AverageZ(prevLeft, prevRight);

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / (float)segments;
            float s = distance * t;
            Vector3 center = GetPredictedPathRawCenter(origin, pathForward, right, curvature, s);
            Vector3 left;
            Vector3 railRight;
            BuildPathEdges(center, right, vehicle.Handle, fallbackDrawZ, out left, out railRight);

            if (_predictedPathFillEnabled)
            {
                DrawFilledPathSegment(prevLeft, prevRight, left, railRight);
            }

            DrawLine(prevLeft, left);
            DrawLine(prevRight, railRight);

            prevLeft = left;
            prevRight = railRight;
            fallbackDrawZ = AverageZ(prevLeft, prevRight);
        }

        if (braking)
        {
            DrawBrakingChevrons(vehicle.Handle, origin, pathForward, right, curvature, distance, fallbackDrawZ);
        }
    }

    private Vector3 GetPredictedPathRawCenter(Vector3 origin, Vector3 pathForward, Vector3 right, float curvature, float s)
    {
        float lateral = Clamp(0.5f * curvature * s * s, -12f, 12f);
        return origin + pathForward * s + right * lateral;
    }

    private float CalculatePredictedPathDistance(float speed)
    {
        float minDistance = Math.Max(0f, _predictedPathMinDistance);
        float maxDistance = Math.Max(minDistance, _predictedPathMaxDistance);

        if (_predictedPathLengthMode.Equals("Linear", StringComparison.OrdinalIgnoreCase))
        {
            return Clamp(speed * _predictedPathLookaheadSeconds, minDistance, maxDistance);
        }

        float referenceSpeed = Math.Max(0.1f, _predictedPathExponentialReferenceSpeed);
        float power = Math.Max(0.1f, _predictedPathExponentialPower);
        float speedRatio = Clamp(speed / referenceSpeed, 0f, 1f);
        float scaled = (float)Math.Pow(speedRatio, power);
        return Clamp(minDistance + (maxDistance - minDistance) * scaled, minDistance, maxDistance);
    }

    private bool UpdatePredictedPathBraking(float distance)
    {
        int now = Game.GameTime;
        if (_lastPredictedPathDistance < 0f)
        {
            _lastPredictedPathDistance = distance;
            return false;
        }

        float distanceDrop = _lastPredictedPathDistance - distance;
        if (distanceDrop >= Math.Max(0.001f, _predictedPathBrakeDistanceDropThreshold))
        {
            _predictedPathBrakingUntil = now + Math.Max(0, _predictedPathBrakeHoldMilliseconds);
        }

        _lastPredictedPathDistance = distance;
        return _predictedPathBrakeChevronsEnabled && now <= _predictedPathBrakingUntil;
    }

    private void ResetPredictedPathBrakeState()
    {
        _lastPredictedPathDistance = -1f;
        _predictedPathBrakingUntil = 0;
    }

    private void DrawBrakingChevrons(int vehicleHandle, Vector3 origin, Vector3 pathForward, Vector3 right, float curvature, float distance, float fallbackDrawZ)
    {
        if (!_predictedPathBrakeChevronsEnabled) return;

        float spacing = Math.Max(1.2f, _predictedPathBrakeChevronSpacing);
        float length = Math.Min(Math.Max(0.4f, _predictedPathBrakeChevronLength), spacing * 0.8f);
        float halfWidth = Math.Min(_predictedPathHalfWidth * 0.9f, Math.Max(0.15f, _predictedPathBrakeChevronWidth));
        float animationSpeed = Math.Max(0.1f, _predictedPathBrakeChevronSpeed);
        float phase = PositiveModulo((Game.GameTime / 1000f) * animationSpeed, spacing);

        for (float s = distance - phase; s > length; s -= spacing)
        {
            float apexS = Math.Max(0f, s - length * 0.5f);
            float tailS = Math.Min(distance, s + length * 0.5f);
            Vector3 rawApex = GetPredictedPathRawCenter(origin, pathForward, right, curvature, apexS);
            Vector3 rawTail = GetPredictedPathRawCenter(origin, pathForward, right, curvature, tailS);
            Vector3 apexNormal;
            Vector3 tailNormal;
            Vector3 apex = ProjectPathCenterToSurface(rawApex, vehicleHandle, fallbackDrawZ, out apexNormal);
            Vector3 tail = ProjectPathCenterToSurface(rawTail, vehicleHandle, apex.Z, out tailNormal);
            Vector3 tailRight = GetSurfaceTangent(right, tailNormal);
            Vector3 leftTail = tail - tailRight * halfWidth;
            Vector3 rightTail = tail + tailRight * halfWidth;

            DrawBrakeChevronLine(leftTail, apex);
            DrawBrakeChevronLine(rightTail, apex);
        }
    }

    private void BuildPathEdges(Vector3 rawCenter, Vector3 horizontalRight, int ignoredEntity, float fallbackDrawZ, out Vector3 left, out Vector3 railRight)
    {
        Vector3 normal = Vector3.WorldUp;
        Vector3 center;

        if (_predictedPathFollowTerrain)
        {
            center = ProjectPathCenterToSurface(rawCenter, ignoredEntity, fallbackDrawZ, out normal);
        }
        else
        {
            center = rawCenter + new Vector3(0f, 0f, _predictedPathHeightOffset);
        }

        Vector3 surfaceRight = GetSurfaceTangent(horizontalRight, normal);
        left = center - surfaceRight * _predictedPathHalfWidth;
        railRight = center + surfaceRight * _predictedPathHalfWidth;
    }

    private Vector3 ProjectPathCenterToSurface(Vector3 rawCenter, int ignoredEntity, float fallbackDrawZ, out Vector3 normal)
    {
        Vector3 surface;
        if (TryRaycastSurface(rawCenter, ignoredEntity, fallbackDrawZ, out surface, out normal) || TryGetGroundSurface(rawCenter, fallbackDrawZ, out surface, out normal))
        {
            normal = NormalizeSurfaceNormal(normal);
            return surface + normal * _predictedPathHeightOffset;
        }

        normal = Vector3.WorldUp;
        return new Vector3(rawCenter.X, rawCenter.Y, fallbackDrawZ);
    }

    private bool TryRaycastSurface(Vector3 rawCenter, int ignoredEntity, float expectedDrawZ, out Vector3 surface, out Vector3 normal)
    {
        surface = Vector3.Zero;
        normal = Vector3.WorldUp;

        try
        {
            float probeAbove = Math.Max(1f, _predictedPathSurfaceProbeAbove);
            float probeBelow = Math.Max(1f, _predictedPathSurfaceProbeBelow);
            Vector3 start = new Vector3(rawCenter.X, rawCenter.Y, expectedDrawZ + probeAbove);
            Vector3 end = new Vector3(rawCenter.X, rawCenter.Y, expectedDrawZ - probeBelow);
            int ray = Function.Call<int>(
                Hash.START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE,
                start.X, start.Y, start.Z,
                end.X, end.Y, end.Z,
                RAYCAST_SURFACE_FLAGS,
                ignoredEntity,
                RAYCAST_DEFAULT_OPTIONS);

            if (ray == 0) return false;

            var hit = new OutputArgument();
            var endCoords = new OutputArgument();
            var surfaceNormal = new OutputArgument();
            var entityHit = new OutputArgument();
            Function.Call<int>(Hash.GET_SHAPE_TEST_RESULT, ray, hit, endCoords, surfaceNormal, entityHit);

            if (!hit.GetResult<bool>()) return false;

            surface = endCoords.GetResult<Vector3>();
            normal = surfaceNormal.GetResult<Vector3>();
            return IsSurfaceNearExpected(surface.Z, expectedDrawZ);
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetGroundSurface(Vector3 rawCenter, float expectedDrawZ, out Vector3 surface, out Vector3 normal)
    {
        surface = Vector3.Zero;
        normal = Vector3.WorldUp;

        try
        {
            var groundZ = new OutputArgument();
            var groundNormal = new OutputArgument();
            bool found = Function.Call<bool>(
                Hash.GET_GROUND_Z_AND_NORMAL_FOR_3D_COORD,
                rawCenter.X,
                rawCenter.Y,
                expectedDrawZ + Math.Max(1f, _predictedPathSurfaceProbeAbove),
                groundZ,
                groundNormal);

            if (found)
            {
                surface = new Vector3(rawCenter.X, rawCenter.Y, groundZ.GetResult<float>());
                normal = groundNormal.GetResult<Vector3>();
                return IsSurfaceNearExpected(surface.Z, expectedDrawZ);
            }
        }
        catch { }

        try
        {
            var groundZ = new OutputArgument();
            bool found = Function.Call<bool>(
                Hash.GET_GROUND_Z_FOR_3D_COORD,
                rawCenter.X,
                rawCenter.Y,
                expectedDrawZ + Math.Max(1f, _predictedPathSurfaceProbeAbove),
                groundZ,
                false);

            if (found)
            {
                surface = new Vector3(rawCenter.X, rawCenter.Y, groundZ.GetResult<float>());
                normal = Vector3.WorldUp;
                return IsSurfaceNearExpected(surface.Z, expectedDrawZ);
            }
        }
        catch { }

        return false;
    }

    private bool IsSurfaceNearExpected(float surfaceZ, float expectedDrawZ)
    {
        float maxSnapUp = Math.Max(0.1f, _predictedPathMaxSnapUp);
        float maxSnapDown = Math.Max(0.1f, _predictedPathMaxSnapDown);
        float delta = surfaceZ - expectedDrawZ;
        return delta <= maxSnapUp && delta >= -maxSnapDown;
    }

    private static Vector3 NormalizeSurfaceNormal(Vector3 normal)
    {
        if (normal.Length() < 0.001f) return Vector3.WorldUp;
        normal.Normalize();
        if (normal.Z < 0f)
        {
            normal = new Vector3(-normal.X, -normal.Y, -normal.Z);
        }

        return normal;
    }

    private static Vector3 GetSurfaceTangent(Vector3 horizontalRight, Vector3 normal)
    {
        normal = NormalizeSurfaceNormal(normal);
        Vector3 tangent = horizontalRight - normal * Dot(horizontalRight, normal);
        if (tangent.Length() < 0.001f)
        {
            tangent = horizontalRight;
        }

        if (tangent.Length() < 0.001f)
        {
            tangent = new Vector3(-1f, 0f, 0f);
        }
        else
        {
            tangent.Normalize();
        }

        return tangent;
    }

    private void DrawFilledPathSegment(Vector3 prevLeft, Vector3 prevRight, Vector3 left, Vector3 right)
    {
        DrawPoly(prevLeft, left, prevRight);
        DrawPoly(prevRight, left, right);
    }

    private float GetYawRate(int vehicleHandle)
    {
        try
        {
            Vector3 rotationVelocity = Function.Call<Vector3>(Hash.GET_ENTITY_ROTATION_VELOCITY, vehicleHandle);
            return rotationVelocity.Z;
        }
        catch
        {
            return 0f;
        }
    }

    private void DrawLine(Vector3 from, Vector3 to)
    {
        Function.Call(
            Hash.DRAW_LINE,
            from.X, from.Y, from.Z,
            to.X, to.Y, to.Z,
            ClampByte(_predictedPathRed),
            ClampByte(_predictedPathGreen),
            ClampByte(_predictedPathBlue),
            ClampByte(_predictedPathAlpha));
    }

    private void DrawBrakeChevronLine(Vector3 from, Vector3 to)
    {
        DrawBrakeChevronLineOnce(from, to);

        float strokeOffset = Math.Max(0f, _predictedPathBrakeChevronStrokeOffset);
        float weightMultiplier = Math.Max(1f, _predictedPathBrakeChevronWeightMultiplier);
        int extraPairs = Math.Min(3, (int)Math.Ceiling((weightMultiplier - 1f) * 1.25f));
        if (strokeOffset <= 0f || extraPairs <= 0) return;

        Vector3 line = to - from;
        if (line.Length() < 0.001f) return;

        Vector3 offsetDirection = Vector3.Cross(Vector3.WorldUp, line);
        if (offsetDirection.Length() < 0.001f) return;
        offsetDirection.Normalize();

        for (int i = 1; i <= extraPairs; i++)
        {
            Vector3 offset = offsetDirection * (strokeOffset * i);
            DrawBrakeChevronLineOnce(from + offset, to + offset);
            DrawBrakeChevronLineOnce(from - offset, to - offset);
        }
    }

    private void DrawBrakeChevronLineOnce(Vector3 from, Vector3 to)
    {
        Function.Call(
            Hash.DRAW_LINE,
            from.X, from.Y, from.Z,
            to.X, to.Y, to.Z,
            ClampByte(_predictedPathBrakeChevronRed),
            ClampByte(_predictedPathBrakeChevronGreen),
            ClampByte(_predictedPathBrakeChevronBlue),
            ClampByte(_predictedPathBrakeChevronAlpha));
    }

    private void DrawPoly(Vector3 first, Vector3 second, Vector3 third)
    {
        Function.Call(
            Hash.DRAW_POLY,
            first.X, first.Y, first.Z,
            second.X, second.Y, second.Z,
            third.X, third.Y, third.Z,
            ClampByte(_predictedPathRed),
            ClampByte(_predictedPathGreen),
            ClampByte(_predictedPathBlue),
            ClampByte(_predictedPathFillAlpha));
    }

    private void LoadConfig()
    {
        string path = Path.Combine(GetScriptsDirectory(), CONFIG_FILE);
        if (!File.Exists(path))
        {
            Log("Config not found; using defaults.");
            return;
        }

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

            int equals = line.IndexOf('=');
            if (equals <= 0) continue;

            string key = line.Substring(0, equals).Trim();
            string value = line.Substring(equals + 1).Trim();

            if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase))
            {
                _enabled = ParseBool(value, _enabled);
            }
            else if (key.Equals("ToggleKey", StringComparison.OrdinalIgnoreCase))
            {
                Keys parsed;
                if (Enum.TryParse(value, true, out parsed)) _toggleKey = parsed;
            }
            else if (key.Equals("RequiredCharacter", StringComparison.OrdinalIgnoreCase))
            {
                _requiredCharacter = value;
            }
            else if (key.Equals("RequiredVehicleModel", StringComparison.OrdinalIgnoreCase))
            {
                _requiredVehicleModel = HashKey(value);
            }
            else if (TryApplyProfileSetting(_chillProfile, key, value, "Chill."))
            {
            }
            else if (TryApplyProfileSetting(_madMaxProfile, key, value, "MadMax."))
            {
            }
            else if (ApplyProfileSetting(_madMaxProfile, key, value))
            {
            }
            else if (key.Equals("BlockVehicleControls", StringComparison.OrdinalIgnoreCase))
            {
                _blockVehicleControls = ParseBool(value, _blockVehicleControls);
            }
            else if (key.Equals("ShowStatus", StringComparison.OrdinalIgnoreCase))
            {
                _showStatus = ParseBool(value, _showStatus);
            }
            else if (key.Equals("PredictedPathEnabled", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathEnabled = ParseBool(value, _predictedPathEnabled);
            }
            else if (key.Equals("PredictedPathAutopilotOnly", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathAutopilotOnly = ParseBool(value, _predictedPathAutopilotOnly);
            }
            else if (key.Equals("PredictedPathSegments", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathSegments = ParseInt(value, _predictedPathSegments);
            }
            else if (key.Equals("PredictedPathLengthMode", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathLengthMode = value;
            }
            else if (key.Equals("PredictedPathLookaheadSeconds", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathLookaheadSeconds = ParseFloat(value, _predictedPathLookaheadSeconds);
            }
            else if (key.Equals("PredictedPathExponentialPower", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathExponentialPower = ParseFloat(value, _predictedPathExponentialPower);
            }
            else if (key.Equals("PredictedPathExponentialReferenceSpeed", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathExponentialReferenceSpeed = ParseFloat(value, _predictedPathExponentialReferenceSpeed);
            }
            else if (key.Equals("PredictedPathMinDistance", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathMinDistance = ParseFloat(value, _predictedPathMinDistance);
            }
            else if (key.Equals("PredictedPathMaxDistance", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathMaxDistance = ParseFloat(value, _predictedPathMaxDistance);
            }
            else if (key.Equals("PredictedPathHalfWidth", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathHalfWidth = ParseFloat(value, _predictedPathHalfWidth);
            }
            else if (key.Equals("PredictedPathHeightOffset", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathHeightOffset = ParseFloat(value, _predictedPathHeightOffset);
            }
            else if (key.Equals("PredictedPathRed", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathRed = ParseInt(value, _predictedPathRed);
            }
            else if (key.Equals("PredictedPathGreen", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathGreen = ParseInt(value, _predictedPathGreen);
            }
            else if (key.Equals("PredictedPathBlue", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBlue = ParseInt(value, _predictedPathBlue);
            }
            else if (key.Equals("PredictedPathAlpha", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathAlpha = ParseInt(value, _predictedPathAlpha);
            }
            else if (key.Equals("PredictedPathFillEnabled", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathFillEnabled = ParseBool(value, _predictedPathFillEnabled);
            }
            else if (key.Equals("PredictedPathFillAlpha", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathFillAlpha = ParseInt(value, _predictedPathFillAlpha);
            }
            else if (key.Equals("PredictedPathFollowTerrain", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathFollowTerrain = ParseBool(value, _predictedPathFollowTerrain);
            }
            else if (key.Equals("PredictedPathSurfaceProbeAbove", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathSurfaceProbeAbove = ParseFloat(value, _predictedPathSurfaceProbeAbove);
            }
            else if (key.Equals("PredictedPathSurfaceProbeBelow", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathSurfaceProbeBelow = ParseFloat(value, _predictedPathSurfaceProbeBelow);
            }
            else if (key.Equals("PredictedPathMaxSnapUp", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathMaxSnapUp = ParseFloat(value, _predictedPathMaxSnapUp);
            }
            else if (key.Equals("PredictedPathMaxSnapDown", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathMaxSnapDown = ParseFloat(value, _predictedPathMaxSnapDown);
            }
            else if (key.Equals("PredictedPathDirectionSpeedThreshold", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathDirectionSpeedThreshold = ParseFloat(value, _predictedPathDirectionSpeedThreshold);
            }
            else if (key.Equals("PredictedPathBrakeChevronsEnabled", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronsEnabled = ParseBool(value, _predictedPathBrakeChevronsEnabled);
            }
            else if (key.Equals("PredictedPathBrakeDistanceDropThreshold", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeDistanceDropThreshold = ParseFloat(value, _predictedPathBrakeDistanceDropThreshold);
            }
            else if (key.Equals("PredictedPathBrakeChevronSpacing", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronSpacing = ParseFloat(value, _predictedPathBrakeChevronSpacing);
            }
            else if (key.Equals("PredictedPathBrakeChevronLength", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronLength = ParseFloat(value, _predictedPathBrakeChevronLength);
            }
            else if (key.Equals("PredictedPathBrakeChevronWidth", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronWidth = ParseFloat(value, _predictedPathBrakeChevronWidth);
            }
            else if (key.Equals("PredictedPathBrakeChevronSpeed", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronSpeed = ParseFloat(value, _predictedPathBrakeChevronSpeed);
            }
            else if (key.Equals("PredictedPathBrakeChevronRed", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronRed = ParseInt(value, _predictedPathBrakeChevronRed);
            }
            else if (key.Equals("PredictedPathBrakeChevronGreen", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronGreen = ParseInt(value, _predictedPathBrakeChevronGreen);
            }
            else if (key.Equals("PredictedPathBrakeChevronBlue", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronBlue = ParseInt(value, _predictedPathBrakeChevronBlue);
            }
            else if (key.Equals("PredictedPathBrakeChevronAlpha", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronAlpha = ParseInt(value, _predictedPathBrakeChevronAlpha);
            }
            else if (key.Equals("PredictedPathBrakeChevronWeightMultiplier", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronWeightMultiplier = ParseFloat(value, _predictedPathBrakeChevronWeightMultiplier);
            }
            else if (key.Equals("PredictedPathBrakeChevronStrokeOffset", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeChevronStrokeOffset = ParseFloat(value, _predictedPathBrakeChevronStrokeOffset);
            }
            else if (key.Equals("PredictedPathBrakeHoldMilliseconds", StringComparison.OrdinalIgnoreCase))
            {
                _predictedPathBrakeHoldMilliseconds = ParseInt(value, _predictedPathBrakeHoldMilliseconds);
            }
        }
    }

    private bool TryApplyProfileSetting(AutopilotProfile profile, string key, string value, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return ApplyProfileSetting(profile, key.Substring(prefix.Length), value);
    }

    private bool ApplyProfileSetting(AutopilotProfile profile, string key, string value)
    {
        if (key.Equals("SpeedMetersPerSecond", StringComparison.OrdinalIgnoreCase))
        {
            profile.speedMetersPerSecond = ParseFloat(value, profile.speedMetersPerSecond);
        }
        else if (key.Equals("SlowSpeedMetersPerSecond", StringComparison.OrdinalIgnoreCase))
        {
            profile.slowSpeedMetersPerSecond = ParseFloat(value, profile.slowSpeedMetersPerSecond);
        }
        else if (key.Equals("SlowdownDistance", StringComparison.OrdinalIgnoreCase))
        {
            profile.slowdownDistance = ParseFloat(value, profile.slowdownDistance);
        }
        else if (key.Equals("ArrivalRadius", StringComparison.OrdinalIgnoreCase))
        {
            profile.arrivalRadius = ParseFloat(value, profile.arrivalRadius);
        }
        else if (key.Equals("DrivingStyle", StringComparison.OrdinalIgnoreCase))
        {
            profile.drivingStyle = ParseInt(value, profile.drivingStyle);
        }
        else if (key.Equals("DriverAbility", StringComparison.OrdinalIgnoreCase))
        {
            profile.driverAbility = ParseFloat(value, profile.driverAbility);
        }
        else if (key.Equals("DriverAggression", StringComparison.OrdinalIgnoreCase))
        {
            profile.driverAggression = ParseFloat(value, profile.driverAggression);
        }
        else if (key.Equals("ApplyAutopilotHandlingBoost", StringComparison.OrdinalIgnoreCase))
        {
            profile.applyHandlingBoost = ParseBool(value, profile.applyHandlingBoost);
        }
        else if (key.Equals("BoostEnginePowerMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostEnginePowerMultiplier = ParseFloat(value, profile.boostEnginePowerMultiplier);
        }
        else if (key.Equals("BoostEngineTorqueMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostEngineTorqueMultiplier = ParseFloat(value, profile.boostEngineTorqueMultiplier);
        }
        else if (key.Equals("BoostBrakeForce", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostBrakeForce = ParseFloat(value, profile.boostBrakeForce);
        }
        else if (key.Equals("BoostHandBrakeForce", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostHandBrakeForce = ParseFloat(value, profile.boostHandBrakeForce);
        }
        else if (key.Equals("BoostSteeringLock", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostSteeringLock = ParseFloat(value, profile.boostSteeringLock);
        }
        else if (key.Equals("BoostTractionCurveMax", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostTractionCurveMax = ParseFloat(value, profile.boostTractionCurveMax);
        }
        else if (key.Equals("BoostTractionCurveMin", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostTractionCurveMin = ParseFloat(value, profile.boostTractionCurveMin);
        }
        else if (key.Equals("BoostTractionCurveLateral", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostTractionCurveLateral = ParseFloat(value, profile.boostTractionCurveLateral);
        }
        else if (key.Equals("BoostLowSpeedTractionLossMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostLowSpeedTractionLossMultiplier = ParseFloat(value, profile.boostLowSpeedTractionLossMultiplier);
        }
        else if (key.Equals("BoostTractionLossMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostTractionLossMultiplier = ParseFloat(value, profile.boostTractionLossMultiplier);
        }
        else if (key.Equals("BoostSuspensionForce", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostSuspensionForce = ParseFloat(value, profile.boostSuspensionForce);
        }
        else if (key.Equals("BoostTopSpeedMultiplier", StringComparison.OrdinalIgnoreCase))
        {
            profile.boostTopSpeedMultiplier = ParseFloat(value, profile.boostTopSpeedMultiplier);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool ParseBool(string value, bool fallback)
    {
        bool parsed;
        return bool.TryParse(value, out parsed) ? parsed : fallback;
    }

    private static float ParseFloat(string value, float fallback)
    {
        float parsed;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
    }

    private static int ParseInt(string value, int fallback)
    {
        int parsed;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static int ClampByte(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return value;
    }

    private static float Dist2D(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static float DistanceSquared(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static float Dot(Vector3 a, Vector3 b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    private static float AverageZ(Vector3 a, Vector3 b)
    {
        return (a.Z + b.Z) * 0.5f;
    }

    private static float PositiveModulo(float value, float divisor)
    {
        if (divisor <= 0f) return 0f;
        float result = value % divisor;
        return result < 0f ? result + divisor : result;
    }

    private static void AppendSnapshotFloat(StringBuilder builder, string key, float value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static string NormalizePlate(string value)
    {
        return (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
    }

    private static int HashKey(string value)
    {
        uint hash = 0;
        string lower = (value ?? string.Empty).ToLowerInvariant();

        for (int i = 0; i < lower.Length; i++)
        {
            hash += lower[i];
            hash += hash << 10;
            hash ^= hash >> 6;
        }

        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;
        return unchecked((int)hash);
    }

    private static string GetScriptsDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string trimmed = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(Path.GetFileName(trimmed), "scripts", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : Path.Combine(baseDir, "scripts");
    }

    private static string FormatPosition(Vector3 position)
    {
        return position.X.ToString("0.0", CultureInfo.InvariantCulture) + "," +
               position.Y.ToString("0.0", CultureInfo.InvariantCulture) + "," +
               position.Z.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void OpenLog()
    {
        try
        {
            string path = Path.Combine(GetScriptsDirectory(), LOG_FILE);
            _log = new StreamWriter(path, true, Encoding.UTF8);
            _log.AutoFlush = true;
            Log("=== starting ===");
        }
        catch
        {
            _log = null;
        }
    }

    private void Log(string message)
    {
        try
        {
            if (_log != null)
            {
                _log.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + message);
            }
        }
        catch { }
    }

    private void ShowStatus(string message)
    {
        if (!_showStatus) return;

        try
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_PRINT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
            Function.Call(Hash.END_TEXT_COMMAND_PRINT, 2500, true);
        }
        catch { }
    }

    private void OnAborted(object sender, EventArgs e)
    {
        if (_active)
        {
            StopAutopilot("Autopilot aborted", false);
        }

        try
        {
            if (_log != null)
            {
                _log.Flush();
                _log.Dispose();
                _log = null;
            }
        }
        catch { }
    }
}
