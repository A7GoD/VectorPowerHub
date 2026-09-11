using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // PROCESS INSPECTION & GAME FILTERING
    // ---------------------------------------------------------------------------------------------
    private bool IsGameProcess(int pid, out string friendlyName) {
        return IsGameProcess(pid, false, out friendlyName);
    }

    private bool IsGameProcess(int pid, bool fallbackPermissive, out string friendlyName) {
        friendlyName = "";
        if (pid <= 4 || pid == _currentHubPid) return false;

        try {
            using (Process p = Process.GetProcessById(pid)) {
                string procName = p.ProcessName;
                if (EXCLUDE_NAMES.Contains(procName)) return false;

                string fullPath = GetProcessPath(pid);
                string lowerPath = (!string.IsNullOrEmpty(fullPath)) ? fullPath.ToLowerInvariant() : "";

                // Disregard system and Windows background directories
                if (!string.IsNullOrEmpty(lowerPath)) {
                    if (lowerPath.Contains(@"\windows\system32\") ||
                        lowerPath.Contains(@"\windows\syswow64\") ||
                        lowerPath.Contains(@"\windows\systemapps\") ||
                        lowerPath.Contains(@"\windows\microsoft.net\")) {
                        return false;
                    }
                }

                bool isGame = false;

                // 1. Instant check against Windows GameConfigStore cache
                lock (_syncLock) {
                    if (_knownGameExes.Contains(procName) || _knownGameExes.Contains(procName + ".exe")) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath) && (_knownGamePaths.Contains(fullPath) || _knownGamePaths.Contains(lowerPath))) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath)) {
                        string exeName = Path.GetFileName(fullPath);
                        if (!string.IsNullOrEmpty(exeName) && _knownGameExes.Contains(exeName)) {
                            isGame = true;
                        }
                    }
                }

                // 2. Check standard gaming root directories & launchers
                if (!isGame && !string.IsNullOrEmpty(lowerPath)) {
                    for (int i = 0; i < GAME_PATH_HINTS.Length; i++) {
                        if (lowerPath.Contains(GAME_PATH_HINTS[i])) {
                            isGame = true;
                            break;
                        }
                    }
                }

                // 3. Unreal Engine, Unity, and common game markers
                if (!isGame) {
                    if (procName.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
                        procName.EndsWith("-Win32-Shipping", StringComparison.OrdinalIgnoreCase) ||
                        procName.IndexOf("Discovery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        procName.IndexOf("TheFinals", StringComparison.OrdinalIgnoreCase) >= 0) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath)) {
                        try {
                            string dir = Path.GetDirectoryName(fullPath);
                            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "UnityPlayer.dll"))) {
                                isGame = true;
                            }
                        } catch { }
                    }
                }

                // 4. Hardware Fallback: If discrete GPU is heavily active, allow non-system process
                if (!isGame && fallbackPermissive) {
                    isGame = true;
                }

                if (!isGame) return false;

                // Extract friendly game title from Steam directory structure if present
                if (!string.IsNullOrEmpty(fullPath) && lowerPath.Contains(@"steamapps\common\")) {
                    int idx = lowerPath.IndexOf(@"steamapps\common\") + @"steamapps\common\".Length;
                    string sub = fullPath.Substring(idx);
                    int slash = sub.IndexOf('\\');
                    if (slash > 0) {
                        friendlyName = sub.Substring(0, slash);
                    }
                }

                if (string.IsNullOrEmpty(friendlyName)) {
                    try {
                        string desc = p.MainModule.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrEmpty(desc) && desc.Length > 2 && !desc.Equals(procName, StringComparison.OrdinalIgnoreCase)) {
                            friendlyName = desc;
                        }
                    } catch { }
                }

                if (string.IsNullOrEmpty(friendlyName)) {
                    try {
                        string winTitle = p.MainWindowTitle;
                        if (!string.IsNullOrEmpty(winTitle) && winTitle.Length > 2) {
                            friendlyName = winTitle.Trim();
                        }
                    } catch { }
                }

                if (string.IsNullOrEmpty(friendlyName)) {
                    friendlyName = procName;
                }

                return true;
            }
        } catch {
            return false;
        }
    }

    private static string GetProcessPath(int pid) {
        IntPtr h = Win32Native.OpenProcess(Win32Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return "";
        try {
            uint size = 1024;
            StringBuilder sb = new StringBuilder((int)size);
            if (Win32Native.QueryFullProcessImageNameW(h, 0, sb, ref size)) {
                return sb.ToString();
            }
        } finally {
            Win32Native.CloseHandle(h);
        }
        return "";
    }

    private int FindRunningGameCandidate(out string gameName) {
        gameName = "";
        try {
            Process[] procs = Process.GetProcesses();
            for (int i = 0; i < procs.Length; i++) {
                Process p = procs[i];
                int pid = p.Id;
                if (pid <= 4 || pid == _currentHubPid) continue;
                string pName = p.ProcessName.ToLowerInvariant();
                if (pName.Contains("crash") || pName.Contains("handler") || pName.Contains("helper")) continue;
                string fName;
                if (IsGameProcess(pid, false, out fName)) {
                    gameName = fName;
                    return pid;
                }
            }
            for (int i = 0; i < procs.Length; i++) {
                Process p = procs[i];
                int pid = p.Id;
                if (pid <= 4 || pid == _currentHubPid) continue;
                string pName = p.ProcessName.ToLowerInvariant();
                if (pName.Contains("crash") || pName.Contains("handler") || pName.Contains("helper")) continue;
                string fName;
                if (IsGameProcess(pid, true, out fName)) {
                    gameName = fName;
                    return pid;
                }
            }
        } catch { }
        return 0;
    }

}
