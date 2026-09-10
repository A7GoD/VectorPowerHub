using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {

    // ---------------------------------------------------------------------------------------------
    // SINGLETON PATTERN
    // ---------------------------------------------------------------------------------------------
    private static readonly PowerCoreEngine _instance = new PowerCoreEngine();
    public static PowerCoreEngine Instance {
        get { return _instance; }
    }

    // ---------------------------------------------------------------------------------------------
    // PUBLIC EVENTS & PROPERTIES
    // ---------------------------------------------------------------------------------------------
    public event EventHandler<TelemetrySnapshot> OnTelemetryUpdated;

    private TelemetrySnapshot _currentSnapshot;
    public TelemetrySnapshot CurrentSnapshot {
        get {
            lock (_syncLock) {
                return _currentSnapshot;
            }
        }
    }

    private string _activeProfile;
    public string ActiveProfile {
        get {
            lock (_syncLock) {
                return _activeProfile;
            }
        }
    }

    private string _selectedGamingProfile = "snappy";
    public string SelectedGamingProfile {
        get {
            lock (_syncLock) {
                return _selectedGamingProfile;
            }
        }
        set {
            lock (_syncLock) {
                if (!string.IsNullOrEmpty(value)) {
                    _selectedGamingProfile = value.Trim().ToLowerInvariant();
                    if (_isGameMode || !_autoProfileSwitching) {
                        ApplyProfileInternal(_selectedGamingProfile);
                    }
                }
            }
        }
    }

    private volatile bool _autoProfileSwitching = true;
    public bool AutoProfileSwitching {
        get { return _autoProfileSwitching; }
        set {
            lock (_syncLock) {
                _autoProfileSwitching = value;
                if (_autoProfileSwitching) {
                    if (_isGameMode) {
                        ApplyProfileInternal(_selectedGamingProfile);
                    } else {
                        ApplyProfileInternal("desktop");
                    }
                }
            }
        }
    }

    private string _selectedDesktopProfile = "desktop";
    public string SelectedDesktopProfile {
        get {
            lock (_syncLock) {
                return _selectedDesktopProfile;
            }
        }
        set {
            lock (_syncLock) {
                if (!string.IsNullOrEmpty(value)) {
                    _selectedDesktopProfile = value.Trim().ToLowerInvariant();
                    if (!_isGameMode && _autoProfileSwitching) {
                        ApplyProfileInternal(_selectedDesktopProfile);
                    }
                }
            }
        }
    }

    public void SetGamingProfile(string profileId) {
        SelectedGamingProfile = profileId;
    }

    public void SetDesktopProfile(string profileId) {
        SelectedDesktopProfile = profileId;
    }

    public void SetSelectedDesktopProfile(string profileId) {
        SelectedDesktopProfile = profileId;
    }

    public void SetAutoProfileSwitching(bool enabled) {
        AutoProfileSwitching = enabled;
    }

    private volatile bool _isRunning;
    public bool IsRunning {
        get { return _isRunning; }
    }

    private volatile bool _isEtwActive;
    public bool IsEtwActive {
        get { return _isEtwActive; }
    }

    private volatile bool _isBenchmarking;
    public bool IsBenchmarking {
        get { return _isBenchmarking; }
    }

    private volatile bool _benchmarkCancelRequested;

    // Custom Profile Configurable Parameters
    private int _customPCoreMhz;
    private int _customECoreMhz;
    private int _customBoostMode;
    private int _customEpp;
    private int _customGpuClockMhz;

    private static string FpsFilePath {
        get {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "game_fps.txt");
        }
    }

    private static string PcoreCapFilePath {
        get {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "game_pcore_cap.txt");
        }
    }

    // ---------------------------------------------------------------------------------------------

}
