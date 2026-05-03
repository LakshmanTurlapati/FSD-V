using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

public sealed class FranklinCybertruckPersonalVehicle : Script
{
    private const string CONFIG_FILE = "FranklinCybertruckPersonalVehicle.ini";
    private const string LOG_FILE = "FranklinCybertruckPersonalVehicle.log";
    private const int DEFAULT_SCAN_INTERVAL_MS = 750;
    private const int MODEL_LOAD_TIMEOUT_MS = 3000;
    private const int SWAP_COOLDOWN_MS = 10000;

    private bool _enabled = true;
    private int _targetModel = HashKey("buffalo2");
    private int _replacementModel = HashKey("CyberTruckV");
    private int _franklinModel = HashKey("player_one");
    private float _scanRadius = 80f;
    private bool _skipInMissions = true;
    private Keys _toggleKey = Keys.F9;
    private string _matchPlateText = "FC1988";
    private string _plateText = "FC1988";
    private bool _forceDefaultColors = true;
    private int _primaryColor = 4;
    private int _secondaryColor = 4;
    private int _pearlescentColor = 4;
    private int _wheelColor = 156;
    private bool _forceWindowTint = true;
    private int _windowTint = 0;
    private bool _applyPerformanceTuning;
    private float _enginePowerMultiplier = 0f;
    private float _engineTorqueMultiplier = 1f;
    private float _brakeForce = 1.3f;
    private float _handBrakeForce = 0.5f;
    private float _damageMultiplier = 4f;
    private int _bedCoverModType = -1;
    private int _bedCoverModIndex = -1;
    private readonly List<int> _enableExtras = new List<int>();
    private readonly List<int> _disableExtras = new List<int>();
    private int _lastTunedVehicleHandle;
    private int _nextScanAt;
    private int _lastSwapAt = -SWAP_COOLDOWN_MS;
    private Vector3 _lastSwapPosition = Vector3.Zero;
    private bool _warnedModelMissing;
    private StreamWriter _log;

    public FranklinCybertruckPersonalVehicle()
    {
        OpenLog();
        LoadConfig();

        Interval = 100;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += OnAborted;

        Log("Loaded. Target=" + _targetModel + " Replacement=" + _replacementModel + " Enabled=" + _enabled);
        ShowStatus("Franklin Cybertruck swapper loaded");
    }

    private void OnTick(object sender, EventArgs e)
    {
        int now = Game.GameTime;
        if (now < _nextScanAt) return;
        _nextScanAt = now + DEFAULT_SCAN_INTERVAL_MS;

        if (!_enabled) return;
        if (_skipInMissions && Function.Call<bool>(Hash.GET_MISSION_FLAG)) return;

        Ped player = Game.Player.Character;
        if (player == null || !player.Exists()) return;
        if (Function.Call<int>(Hash.GET_ENTITY_MODEL, player.Handle) != _franklinModel) return;

        ApplyCurrentCybertruckTuning(player);

        Vehicle target = FindFranklinBuffalo(player);
        if (target == null) return;

        if (now - _lastSwapAt < SWAP_COOLDOWN_MS && target.Position.DistanceTo(_lastSwapPosition) < 12f)
        {
            return;
        }

        ReplaceVehicle(player, target);
    }

    private Vehicle FindFranklinBuffalo(Ped player)
    {
        Vehicle best = null;
        float bestDistance = _scanRadius;
        int currentVehicleHandle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);

        foreach (Vehicle vehicle in World.GetAllVehicles())
        {
            if (vehicle == null || !vehicle.Exists()) continue;
            if (Function.Call<int>(Hash.GET_ENTITY_MODEL, vehicle.Handle) != _targetModel) continue;

            float distance = vehicle.Position.DistanceTo(player.Position);
            if (distance > bestDistance) continue;
            if (vehicle.Handle != currentVehicleHandle && !PlateMatches(vehicle)) continue;

            best = vehicle;
            bestDistance = distance;
        }

        return best;
    }

    private bool PlateMatches(Vehicle vehicle)
    {
        if (string.IsNullOrWhiteSpace(_matchPlateText)) return true;

        string actual = Function.Call<string>(Hash.GET_VEHICLE_NUMBER_PLATE_TEXT, vehicle.Handle);
        return NormalizePlate(actual) == NormalizePlate(_matchPlateText);
    }

    private void ReplaceVehicle(Ped player, Vehicle oldVehicle)
    {
        if (!EnsureReplacementModelLoaded()) return;

        bool playerWasInside = Function.Call<bool>(Hash.IS_PED_IN_VEHICLE, player.Handle, oldVehicle.Handle, false);
        Vector3 position = oldVehicle.Position;
        float heading = oldVehicle.Heading;
        float speed = oldVehicle.Speed;

        int newHandle = Function.Call<int>(
            Hash.CREATE_VEHICLE,
            _replacementModel,
            position.X,
            position.Y,
            position.Z,
            heading,
            false,
            false,
            false);

        if (newHandle == 0)
        {
            Log("CREATE_VEHICLE returned 0 for CyberTruckV");
            return;
        }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, newHandle, true, true);
        Function.Call(Hash.SET_VEHICLE_NUMBER_PLATE_TEXT, newHandle, _plateText);
        Function.Call(Hash.SET_VEHICLE_ON_GROUND_PROPERLY, newHandle);
        Function.Call(Hash.SET_VEHICLE_FIXED, newHandle);
        Function.Call(Hash.SET_VEHICLE_ENGINE_ON, newHandle, true, true, false);
        Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, newHandle, speed);
        Vehicle newVehicle = GetVehicleByHandle(newHandle);
        ApplyVehicleAppearance(newHandle, newVehicle);

        if (playerWasInside)
        {
            Function.Call(Hash.SET_PED_INTO_VEHICLE, player.Handle, newHandle, -1);
        }

        Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, oldVehicle.Handle, true, true);
        oldVehicle.Delete();

        _lastSwapAt = Game.GameTime;
        _lastSwapPosition = position;
        Log("Replaced Franklin buffalo2 at " + FormatPosition(position));
        ShowStatus("Franklin's Buffalo replaced with Cybertruck");
    }

    private void ApplyCurrentCybertruckTuning(Ped player)
    {
        int currentVehicleHandle = Function.Call<int>(Hash.GET_VEHICLE_PED_IS_IN, player.Handle, false);
        if (currentVehicleHandle == 0 || currentVehicleHandle == _lastTunedVehicleHandle) return;
        if (Function.Call<int>(Hash.GET_ENTITY_MODEL, currentVehicleHandle) != _replacementModel) return;

        Vehicle vehicle = GetVehicleByHandle(currentVehicleHandle);
        ApplyVehicleAppearance(currentVehicleHandle, vehicle);
    }

    private void ApplyVehicleAppearance(int vehicleHandle, Vehicle vehicle)
    {
        if (_forceDefaultColors)
        {
            Function.Call(Hash.SET_VEHICLE_COLOURS, vehicleHandle, _primaryColor, _secondaryColor);
            Function.Call(Hash.SET_VEHICLE_EXTRA_COLOURS, vehicleHandle, _pearlescentColor, _wheelColor);
        }

        if (_forceWindowTint)
        {
            Function.Call(Hash.SET_VEHICLE_WINDOW_TINT, vehicleHandle, _windowTint);
        }

        if (vehicle != null && vehicle.Exists())
        {
            ApplyVehiclePerformance(vehicle);
        }
        else
        {
            Function.Call(Hash.SET_VEHICLE_CHEAT_POWER_INCREASE, vehicleHandle, _enginePowerMultiplier);
        }

        Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicleHandle, 0);

        if (_bedCoverModType >= 0 && _bedCoverModIndex >= 0)
        {
            int modCount = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, vehicleHandle, _bedCoverModType);
            if (modCount > _bedCoverModIndex)
            {
                Function.Call(Hash.SET_VEHICLE_MOD, vehicleHandle, _bedCoverModType, _bedCoverModIndex, false);
                Log("Applied bed-cover candidate mod type " + _bedCoverModType + " index " + _bedCoverModIndex + " of " + modCount + ".");
            }
            else
            {
                Log("Bed-cover candidate mod type " + _bedCoverModType + " has " + modCount + " option(s); no mod applied.");
            }
        }

        ApplyExtras(vehicleHandle, _enableExtras, false, "enabled");
        ApplyExtras(vehicleHandle, _disableExtras, true, "disabled");
        _lastTunedVehicleHandle = vehicleHandle;
    }

    private void ApplyVehiclePerformance(Vehicle vehicle)
    {
        if (_applyPerformanceTuning)
        {
            vehicle.EnginePowerMultiplier = _enginePowerMultiplier;
            vehicle.EngineTorqueMultiplier = _engineTorqueMultiplier;
        }

        vehicle.DirtLevel = 0f;

        if (vehicle.HandlingData != null)
        {
            if (_applyPerformanceTuning)
            {
                vehicle.HandlingData.BrakeForce = _brakeForce;
                vehicle.HandlingData.HandBrakeForce = _handBrakeForce;
            }

            vehicle.HandlingData.CollisionDamageMultiplier = _damageMultiplier;
            vehicle.HandlingData.DeformationDamageMultiplier = _damageMultiplier;
            vehicle.HandlingData.EngineDamageMultiplier = _damageMultiplier;
            vehicle.HandlingData.WeaponDamageMultiplier = _damageMultiplier;
        }

        Log("Applied Cybertruck appearance/damage: silver, no tint, performanceTuning=" + _applyPerformanceTuning +
            ", damage=" + _damageMultiplier.ToString("0.##", CultureInfo.InvariantCulture) + ".");
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

    private void ApplyExtras(int vehicleHandle, List<int> extras, bool disable, string action)
    {
        for (int i = 0; i < extras.Count; i++)
        {
            int extra = extras[i];
            if (!Function.Call<bool>(Hash.DOES_EXTRA_EXIST, vehicleHandle, extra)) continue;

            Function.Call(Hash.SET_VEHICLE_EXTRA, vehicleHandle, extra, disable);
            Log("Extra " + extra + " " + action + ".");
        }
    }

    private bool EnsureReplacementModelLoaded()
    {
        if (!Function.Call<bool>(Hash.IS_MODEL_IN_CDIMAGE, _replacementModel) ||
            !Function.Call<bool>(Hash.IS_MODEL_A_VEHICLE, _replacementModel))
        {
            if (!_warnedModelMissing)
            {
                _warnedModelMissing = true;
                Log("CyberTruckV model is not available. Confirm dlcpacks:/CyberTruckV/ is in mods/update/update.rpf/common/data/dlclist.xml and OpenIV.ASI is installed.");
                ShowStatus("CyberTruckV model is not available; check dlclist.xml");
            }
            return false;
        }

        if (Function.Call<bool>(Hash.HAS_MODEL_LOADED, _replacementModel)) return true;

        int start = Game.GameTime;
        Function.Call(Hash.REQUEST_MODEL, _replacementModel);

        while (!Function.Call<bool>(Hash.HAS_MODEL_LOADED, _replacementModel) &&
               Game.GameTime - start < MODEL_LOAD_TIMEOUT_MS)
        {
            Script.Yield();
        }

        bool loaded = Function.Call<bool>(Hash.HAS_MODEL_LOADED, _replacementModel);
        if (!loaded)
        {
            Log("Timed out loading CyberTruckV model.");
            ShowStatus("CyberTruckV model load timed out");
        }
        return loaded;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != _toggleKey) return;

        _enabled = !_enabled;
        string state = _enabled ? "enabled" : "disabled";
        Log("Toggled " + state);
        ShowStatus("Franklin Cybertruck swapper " + state);
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
            else if (key.Equals("TargetModel", StringComparison.OrdinalIgnoreCase))
            {
                _targetModel = HashKey(value);
            }
            else if (key.Equals("ReplacementModel", StringComparison.OrdinalIgnoreCase))
            {
                _replacementModel = HashKey(value);
            }
            else if (key.Equals("ScanRadius", StringComparison.OrdinalIgnoreCase))
            {
                _scanRadius = ParseFloat(value, _scanRadius);
            }
            else if (key.Equals("SkipInMissions", StringComparison.OrdinalIgnoreCase))
            {
                _skipInMissions = ParseBool(value, _skipInMissions);
            }
            else if (key.Equals("ToggleKey", StringComparison.OrdinalIgnoreCase))
            {
                Keys parsed;
                if (Enum.TryParse(value, true, out parsed)) _toggleKey = parsed;
            }
            else if (key.Equals("PlateText", StringComparison.OrdinalIgnoreCase))
            {
                _plateText = value;
            }
            else if (key.Equals("MatchPlateText", StringComparison.OrdinalIgnoreCase))
            {
                _matchPlateText = value;
            }
            else if (key.Equals("ForceDefaultColors", StringComparison.OrdinalIgnoreCase))
            {
                _forceDefaultColors = ParseBool(value, _forceDefaultColors);
            }
            else if (key.Equals("PrimaryColor", StringComparison.OrdinalIgnoreCase))
            {
                _primaryColor = ParseInt(value, _primaryColor);
            }
            else if (key.Equals("SecondaryColor", StringComparison.OrdinalIgnoreCase))
            {
                _secondaryColor = ParseInt(value, _secondaryColor);
            }
            else if (key.Equals("PearlescentColor", StringComparison.OrdinalIgnoreCase))
            {
                _pearlescentColor = ParseInt(value, _pearlescentColor);
            }
            else if (key.Equals("WheelColor", StringComparison.OrdinalIgnoreCase))
            {
                _wheelColor = ParseInt(value, _wheelColor);
            }
            else if (key.Equals("ForceWindowTint", StringComparison.OrdinalIgnoreCase))
            {
                _forceWindowTint = ParseBool(value, _forceWindowTint);
            }
            else if (key.Equals("WindowTint", StringComparison.OrdinalIgnoreCase))
            {
                _windowTint = ParseInt(value, _windowTint);
            }
            else if (key.Equals("ApplyPerformanceTuning", StringComparison.OrdinalIgnoreCase))
            {
                _applyPerformanceTuning = ParseBool(value, _applyPerformanceTuning);
            }
            else if (key.Equals("EnginePowerMultiplier", StringComparison.OrdinalIgnoreCase))
            {
                _enginePowerMultiplier = ParseFloat(value, _enginePowerMultiplier);
            }
            else if (key.Equals("EngineTorqueMultiplier", StringComparison.OrdinalIgnoreCase))
            {
                _engineTorqueMultiplier = ParseFloat(value, _engineTorqueMultiplier);
            }
            else if (key.Equals("BrakeForce", StringComparison.OrdinalIgnoreCase))
            {
                _brakeForce = ParseFloat(value, _brakeForce);
            }
            else if (key.Equals("HandBrakeForce", StringComparison.OrdinalIgnoreCase))
            {
                _handBrakeForce = ParseFloat(value, _handBrakeForce);
            }
            else if (key.Equals("DamageMultiplier", StringComparison.OrdinalIgnoreCase))
            {
                _damageMultiplier = ParseFloat(value, _damageMultiplier);
            }
            else if (key.Equals("BedCoverModType", StringComparison.OrdinalIgnoreCase))
            {
                _bedCoverModType = ParseInt(value, _bedCoverModType);
            }
            else if (key.Equals("BedCoverModIndex", StringComparison.OrdinalIgnoreCase))
            {
                _bedCoverModIndex = ParseInt(value, _bedCoverModIndex);
            }
            else if (key.Equals("EnableExtras", StringComparison.OrdinalIgnoreCase))
            {
                _enableExtras.Clear();
                _enableExtras.AddRange(ParseIntList(value));
            }
            else if (key.Equals("DisableExtras", StringComparison.OrdinalIgnoreCase))
            {
                _disableExtras.Clear();
                _disableExtras.AddRange(ParseIntList(value));
            }
        }
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

    private static List<int> ParseIntList(string value)
    {
        List<int> result = new List<int>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        string[] parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            int parsed;
            if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static string NormalizePlate(string value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
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
        Log("Aborted.");
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
