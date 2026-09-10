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
            using (RegistryKey childrenKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children")) {
                if (childrenKey != null) {
                    string[] subKeyNames = childrenKey.GetSubKeyNames();
                    for (int i = 0; i < subKeyNames.Length; i++) {
                        string subKeyName = subKeyNames[i];
                        using (RegistryKey subKey = childrenKey.OpenSubKey(subKeyName)) {
                            if (subKey != null) {
                                object flagsObj = subKey.GetValue("Flags");
                                int flags = 0;
                                if (flagsObj is int) {
                                    flags = (int)flagsObj;
                                } else if (flagsObj != null) {
                                    int.TryParse(flagsObj.ToString(), out flags);
                                }

                                if ((flags & 1) != 0 || flags == 17) {
                                    object pathObj = subKey.GetValue("MatchedExeFullPath");
                                    if (pathObj != null) {
                                        string fullPath = pathObj.ToString().Trim();
                                        if (!string.IsNullOrEmpty(fullPath)) {
                                            fullPath = Environment.ExpandEnvironmentVariables(fullPath);
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
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch { }
    }

    // ---------------------------------------------------------------------------------------------

}
