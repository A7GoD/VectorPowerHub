using System;
using System.IO;
using System.Text;

public partial class PowerCoreEngine {
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VectorPowerHub");

    public static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    private int _platformPowerCeilingW = 215;
    public int PlatformPowerCeilingW { get { return _platformPowerCeilingW; } set { _platformPowerCeilingW = value; SaveUserSettings(); } }

    private bool _autoPowerCeiling = false;
    public bool AutoPowerCeiling { get { return _autoPowerCeiling; } set { _autoPowerCeiling = value; SaveUserSettings(); } }

    public void LoadUserSettings() {
        lock (_syncLock) {
            try {
                if (!File.Exists(SettingsFilePath)) return;
                string text = File.ReadAllText(SettingsFilePath);
                if (string.IsNullOrEmpty(text)) return;

                string gaming = ExtractJsonString(text, "SelectedGamingProfile");
                if (!string.IsNullOrEmpty(gaming)) _selectedGamingProfile = gaming.Trim().ToLowerInvariant();

                string desktop = ExtractJsonString(text, "SelectedDesktopProfile");
                if (!string.IsNullOrEmpty(desktop)) _selectedDesktopProfile = desktop.Trim().ToLowerInvariant();

                bool autoSwitch;
                if (ExtractJsonBool(text, "AutoProfileSwitching", out autoSwitch)) _autoProfileSwitching = autoSwitch;

                int val;
                if (ExtractJsonInt(text, "CustomPCoreMhz", out val)) _customPCoreMhz = val;
                if (ExtractJsonInt(text, "CustomECoreMhz", out val)) _customECoreMhz = val;
                if (ExtractJsonInt(text, "CustomBoostMode", out val)) _customBoostMode = val;
                if (ExtractJsonInt(text, "CustomEpp", out val)) _customEpp = val;
                if (ExtractJsonInt(text, "CustomGpuClockMhz", out val)) _customGpuClockMhz = val;
                if (ExtractJsonInt(text, "PlatformPowerCeilingW", out val) && val > 0) _platformPowerCeilingW = val;
                bool autoCeiling;
                if (ExtractJsonBool(text, "AutoPowerCeiling", out autoCeiling)) _autoPowerCeiling = autoCeiling;
            } catch { }
        }
    }

    public void SaveUserSettings() {
        bool startMin; int lastTab;
        ReadGuiSettingsState(out startMin, out lastTab);
        SaveUserSettingsWithGui(startMin, lastTab);
    }

    private static void ReadGuiSettingsState(out bool startMinimized, out int lastActiveTab) {
        startMinimized = false; lastActiveTab = 0;
        try {
            if (File.Exists(SettingsFilePath)) {
                string text = File.ReadAllText(SettingsFilePath);
                bool bVal;
                if (ExtractJsonBool(text, "StartMinimized", out bVal)) startMinimized = bVal;
                int iVal;
                if (ExtractJsonInt(text, "LastActiveTab", out iVal)) lastActiveTab = iVal;
            }
        } catch { }
    }

    public static bool TryLoadGuiSettings(out bool startMinimized, out int lastActiveTab) {
        startMinimized = false; lastActiveTab = 0;
        try {
            if (!File.Exists(SettingsFilePath)) return false;
            string text = File.ReadAllText(SettingsFilePath);
            if (string.IsNullOrEmpty(text)) return false;
            bool bVal;
            if (ExtractJsonBool(text, "StartMinimized", out bVal)) startMinimized = bVal;
            int iVal;
            if (ExtractJsonInt(text, "LastActiveTab", out iVal)) lastActiveTab = iVal;
            return true;
        } catch { return false; }
    }

    public static void SaveGuiSettingsOnly(bool startMinimized, int lastActiveTab) {
        try {
            PowerCoreEngine engine = PowerCoreEngine.Instance;
            if (engine != null) {
                engine.SaveUserSettingsWithGui(startMinimized, lastActiveTab);
                return;
            }
        } catch { }

        try {
            if (!Directory.Exists(SettingsDirectory)) Directory.CreateDirectory(SettingsDirectory);
            string json = string.Format(
                "{{\n  \"SelectedGamingProfile\": \"snappy\",\n  \"SelectedDesktopProfile\": \"desktop\",\n  \"AutoProfileSwitching\": true,\n  \"CustomPCoreMhz\": 4900,\n  \"CustomECoreMhz\": 2800,\n  \"CustomBoostMode\": 4,\n  \"CustomEpp\": 25,\n  \"CustomGpuClockMhz\": 0,\n  \"PlatformPowerCeilingW\": 215,\n  \"AutoPowerCeiling\": false,\n  \"StartMinimized\": {0},\n  \"LastActiveTab\": {1}\n}}\n",
                startMinimized ? "true" : "false", lastActiveTab);
            File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
        } catch { }
    }

    public void SaveUserSettingsWithGui(bool startMinimized, int lastActiveTab) {
        lock (_syncLock) {
            try {
                if (!Directory.Exists(SettingsDirectory)) Directory.CreateDirectory(SettingsDirectory);
                string json = string.Format(
                    "{{\n  \"SelectedGamingProfile\": \"{0}\",\n  \"SelectedDesktopProfile\": \"{1}\",\n  \"AutoProfileSwitching\": {2},\n  \"CustomPCoreMhz\": {3},\n  \"CustomECoreMhz\": {4},\n  \"CustomBoostMode\": {5},\n  \"CustomEpp\": {6},\n  \"CustomGpuClockMhz\": {7},\n  \"PlatformPowerCeilingW\": {8},\n  \"AutoPowerCeiling\": {9},\n  \"StartMinimized\": {10},\n  \"LastActiveTab\": {11}\n}}\n",
                    EscapeJson(_selectedGamingProfile), EscapeJson(_selectedDesktopProfile),
                    _autoProfileSwitching ? "true" : "false",
                    _customPCoreMhz, _customECoreMhz, _customBoostMode, _customEpp, _customGpuClockMhz,
                    _platformPowerCeilingW,
                    _autoPowerCeiling ? "true" : "false",
                    startMinimized ? "true" : "false", lastActiveTab);
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            } catch { }
        }
    }

    private static string EscapeJson(string s) {
        return (s == null) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static string ExtractJsonString(string json, string key) {
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon < 0) return null;
        int qStart = json.IndexOf('"', colon + 1);
        if (qStart < 0) return null;
        int qEnd = json.IndexOf('"', qStart + 1);
        if (qEnd < 0) return null;
        return json.Substring(qStart + 1, qEnd - qStart - 1);
    }

    public static bool ExtractJsonInt(string json, string key, out int value) {
        value = 0;
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon < 0) return false;
        int pos = colon + 1;
        while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n')) pos++;
        int end = pos;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
        return (end > pos) ? int.TryParse(json.Substring(pos, end - pos), out value) : false;
    }

    public static bool ExtractJsonBool(string json, string key, out bool value) {
        value = false;
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon < 0) return false;
        string tail = json.Substring(colon + 1).TrimStart();
        if (tail.StartsWith("true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
        if (tail.StartsWith("false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
        return false;
    }
}
