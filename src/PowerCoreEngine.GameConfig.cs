using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // WINDOWS GAME SERVICE / GAMECONFIGSTORE INTEGRATION
    // ---------------------------------------------------------------------------------------------
    private void LoadGameConfigStore() {
        RefreshGameConfigStore(true);
    }

    private void RefreshGameConfigStore(bool force) {
        DateTime now = DateTime.UtcNow;
        if (!force && (now - _lastGameStoreScan).TotalSeconds < 30.0) {
            return;
        }
        _lastGameStoreScan = now;

        try {
            ScanGameConfigStoreChildren();
            ScanGameConfigStoreParents();
            ScanGameBarRegistry();
        } catch { }
    }

    private void ScanGameConfigStoreChildren() {
        try {
            using (RegistryKey childrenKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children")) {
                if (childrenKey == null) return;
                string[] subKeyNames = childrenKey.GetSubKeyNames();
                for (int i = 0; i < subKeyNames.Length; i++) {
                    using (RegistryKey subKey = childrenKey.OpenSubKey(subKeyNames[i])) {
                        if (subKey == null) continue;
                        object flagsObj = subKey.GetValue("Flags");
                        int flags = 0;
                        if (flagsObj is int) flags = (int)flagsObj;
                        else if (flagsObj != null) int.TryParse(flagsObj.ToString(), out flags);
                        if (flags <= 0 || ((flags & 1) == 0 && flags != 17 && flags != 33)) continue;

                        object pathObj = subKey.GetValue("MatchedExeFullPath");
                        if (pathObj != null) RegisterGamePath(pathObj.ToString());
                        object titleObj = subKey.GetValue("Title");
                        if (titleObj != null) RegisterGameExeName(titleObj.ToString());
                    }
                }
            }
        } catch { }
    }

    private void ScanGameConfigStoreParents() {
        try {
            using (RegistryKey parentsKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Parents")) {
                if (parentsKey == null) return;
                string[] subKeyNames = parentsKey.GetSubKeyNames();
                for (int i = 0; i < subKeyNames.Length; i++) {
                    using (RegistryKey subKey = parentsKey.OpenSubKey(subKeyNames[i])) {
                        if (subKey == null) continue;
                        object flagsObj = subKey.GetValue("Flags");
                        int flags = 0;
                        if (flagsObj is int) flags = (int)flagsObj;
                        else if (flagsObj != null) int.TryParse(flagsObj.ToString(), out flags);
                        if (flags <= 0 || ((flags & 1) == 0 && flags != 17 && flags != 33)) continue;

                        object pathObj = subKey.GetValue("MatchedExeFullPath");
                        if (pathObj != null) RegisterGamePath(pathObj.ToString());
                        object titleObj = subKey.GetValue("Title");
                        if (titleObj != null) RegisterGameExeName(titleObj.ToString());
                    }
                }
            }
        } catch { }
    }

    private void ScanGameBarRegistry() {
        try {
            using (RegistryKey gbKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar")) {
                if (gbKey == null) return;
                string[] valNames = gbKey.GetValueNames();
                for (int i = 0; i < valNames.Length; i++) {
                    string vName = valNames[i];
                    if (vName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                        RegisterGameExeName(vName);
                    }
                }
            }
        } catch { }
    }

    private void RegisterGamePath(string rawPath) {
        if (string.IsNullOrEmpty(rawPath)) return;
        string fullPath = rawPath.Trim();
        if (string.IsNullOrEmpty(fullPath)) return;
        try {
            fullPath = Environment.ExpandEnvironmentVariables(fullPath);
        } catch { }

        lock (_syncLock) {
            _knownGamePaths.Add(fullPath);
            try {
                string exeName = Path.GetFileName(fullPath);
                if (!string.IsNullOrEmpty(exeName)) {
                    _knownGameExes.Add(exeName);
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(exeName);
                    if (!string.IsNullOrEmpty(nameWithoutExt)) {
                        _knownGameExes.Add(nameWithoutExt);
                    }
                }
            } catch { }
        }
    }

    private void RegisterGameExeName(string rawName) {
        if (string.IsNullOrEmpty(rawName)) return;
        string name = rawName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        lock (_syncLock) {
            _knownGameExes.Add(name);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrEmpty(nameWithoutExt)) {
                _knownGameExes.Add(nameWithoutExt);
            }
        }
    }

    private bool IsRegisteredInGameConfigStore(string fullPath, string exeName) {
        // Fast dynamic cache check
        lock (_syncLock) {
            if (!string.IsNullOrEmpty(exeName)) {
                if (_knownGameExes.Contains(exeName)) return true;
                string noExt = Path.GetFileNameWithoutExtension(exeName);
                if (!string.IsNullOrEmpty(noExt) && _knownGameExes.Contains(noExt)) return true;
            }
            if (!string.IsNullOrEmpty(fullPath)) {
                if (_knownGamePaths.Contains(fullPath)) return true;
            }
        }

        // Live check against GameConfigStore registry if not found in cache
        if (string.IsNullOrEmpty(fullPath) && string.IsNullOrEmpty(exeName)) return false;

        try {
            using (RegistryKey childrenKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children")) {
                if (childrenKey != null) {
                    string[] subKeyNames = childrenKey.GetSubKeyNames();
                    for (int i = 0; i < subKeyNames.Length; i++) {
                        using (RegistryKey subKey = childrenKey.OpenSubKey(subKeyNames[i])) {
                            if (subKey == null) continue;
                            object flagsObj = subKey.GetValue("Flags");
                            int flags = 0;
                            if (flagsObj is int) flags = (int)flagsObj;
                            else if (flagsObj != null) int.TryParse(flagsObj.ToString(), out flags);
                            if (flags <= 0 || ((flags & 1) == 0 && flags != 17 && flags != 33)) continue;

                            object pathObj = subKey.GetValue("MatchedExeFullPath");
                            if (pathObj != null) {
                                string regPath = pathObj.ToString().Trim();
                                if (!string.IsNullOrEmpty(regPath)) {
                                    if (!string.IsNullOrEmpty(fullPath) && string.Equals(fullPath, regPath, StringComparison.OrdinalIgnoreCase)) {
                                        RegisterGamePath(regPath);
                                        return true;
                                    }
                                    string regExe = Path.GetFileName(regPath);
                                    if (!string.IsNullOrEmpty(regExe) && !string.IsNullOrEmpty(exeName) && string.Equals(exeName, regExe, StringComparison.OrdinalIgnoreCase)) {
                                        RegisterGamePath(regPath);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch { }

        return false;
    }
}
