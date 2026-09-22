# Contributing to VectorPowerHub ⚡

Welcome! VectorPowerHub is a lightweight, low-overhead Windows power management and game telemetry hub engineered for hybrid Intel Raptor Lake / Arrow Lake HX architectures paired with NVIDIA GeForce RTX discrete GPUs.

This document outlines the architectural constraints, hardware safety directives, Intel power findings, and telemetry pipeline design required for contributing.

---

## 1. Architectural & Codebase Constraints

To ensure zero dependencies, instantaneous startup (<30ms), and long-term maintainability:

1. **File Size Rule**: **Every source file in `src/*.cs` MUST remain $\le$ 200 lines.**
   - If a file approaches or exceeds 200 lines, immediately split it into focused partial classes (e.g. `PowerCoreEngine.GameConfig.cs`, `VectorPowerHubForm.Layout.cs`).
   - Validate line counts using `rtk python tools/count_lines.py`.
2. **Language & Compiler Standards**:
   - **C# 5.0 / .NET Framework 4.0+ Syntax Only**:
     - ❌ NO C# 6+ string interpolation (`$"..."`). Use `string.Format(...)` or string concatenation.
     - ❌ NO expression-bodied members (`=>`).
     - ❌ NO null-propagating operators (`?.`).
     - ❌ NO pattern matching (`is int x`).
   - Target compiler is Microsoft .NET Framework 64-bit `csc.exe` (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
3. **Build & Toolchain**:
   - Build via GNU `make` (`rtk make`).
   - Zero NuGet dependencies or third-party DLLs. All Win32, ETW, PDH, and PowrProf APIs are directly P/Invoked.
4. **Knowledge Graph**:
   - Keep codebase graphify current after source modifications:
     ```bash
     graphify . --code-only
     graphify cluster-only .
     ```

---

## 2. Critical Safety Directives: Discrete GPU & D3cold

### 🚫 ZERO NVML / nvidia-smi Polling During Idle
- When no external display is physically driven by the NVIDIA GPU and no 3D game is rendering, the discrete GPU enters **PCIe D3cold Sleep (0.0W)**.
- Querying NVML (`nvmlDeviceGetUtilizationRates`, `nvmlDeviceGetPowerUsage`) or executing `nvidia-smi` while the GPU is in D3cold forces a PCIe bus wake-up, spikes power consumption by **15–25W**, and prevents the CPU package from entering C8/C10 deep sleep states.
- **Rules**:
  - `ReadGpuTelemetrySafe` and `EnsureNvmlInitialized` must ONLY execute when `_isGameMode`, `_isBenchmarking`, or an external NVIDIA-attached display (`CheckNvidiaDisplayAttached`) is active.
  - On idle desktop or system suspend, `ShutdownNvml()` must be called immediately.

---

## 3. Intel Raptor Lake / Arrow Lake HX Power & Scheduling Findings

Extensive silicon profiling on 24-core hybrid processors (8 P-cores + 16 E-cores) reveals the following physical behaviors:

### Core Parking: 0% DC / 10% AC Target
- **On Battery (DC)**: `CPMINCORES = 0%` allows inactive P-cores to enter deep core C-states ($C_6/C_7$), allowing the CPU to hit true battery idle targets without background jitter.
- **On Mains (AC)**: `CPMINCORES = 10%` ensures immediate microsecond thread wake-up without latency penalties for interactive bursts.
- Fast unpark/park latency intervals (`CPINCREASETIME = 1`, `CPDECREASETIME = 2`) ensure snappy ramping under multi-threaded load.

### E-Core Background Routing & Uncore Floor
- Routing non-critical background workloads to the 16 Efficient cores yields **>95% E-core residency** during office and browser workflows.
- Physical floor of the HX uncore / system agent is approximately **~5W**. Over-constraining core clocks below base frequency does not reduce power further and hurts task completion energy efficiency (race-to-sleep).

### Energy Performance Preference (EPP) Sweet Spots
- **80% (0x50)**: Power Saver / DC Idle (Speed Shift biases strongly toward energy efficiency).
- **65% (0x41)**: Cold & Quiet profile (optimal balance of thermals and framerate consistency).
- **30% (0x1E) / 25% (0x19)**: Snappy profile (eliminates 1% low frame stutter by prioritizing P-core clock boost).

### Windows Processor Performance Boost Modes
- **Mode 0 (Disabled)**: Hard caps frequency to nominal base clock (zero boost). Ideal for maximum battery runtime.
- **Mode 3 (Efficient Enabled)**: Frequency scales autonomously with sustained workload duration, filtering out transient spikes.
- **Mode 4 (Aggressive)**: Ramps instantly to maximum turbo bins on thread activity.

---

## 4. Dynamic Game Detection Architecture

VectorPowerHub uses a **Zero-Blacklist, Positive-Identification** architecture combining kernel ETW and Windows Xbox Game Bar integration.

```
                    ┌──────────────────────────────┐
                    │  Kernel DXGI ETW Event 42    │
                    │  (IDXGISwapChain::Present)   │
                    └──────────────┬───────────────┘
                                   │
                                   ▼
             ┌───────────────────────────────────────────┐
             │   Is PID Presenting at $\ge$ 10.0 FPS?     │
             └─────────────────────┬─────────────────────┘
                                   │ Yes
                                   ▼
        ┌─────────────────────────────────────────────────────┐
        │        Positive Identification Engine:              │
        │  1. Windows GameConfigStore / Xbox Game Bar?        │
        │  2. Recognized Gaming Library Directory Path?       │
        │  3. 3D Game Engine Signatures (Unreal/Unity)?       │
        │  4. Reject Electron/CEF Apps (app.asar / node.dll)? │
        └──────────────────────────┬──────────────────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
                 [Match]                      [No Match]
                    │                             │
                    ▼                             ▼
        ┌───────────────────────┐     ┌───────────────────────┐
        │  Engage Game Profile  │     │   Ignore Desktop App  │
        │  & NVML Safe Telemetry│     │   (Keep D3cold 0.0W)  │
        └───────────────────────┘     └───────────────────────┘
```

### Zero-Blacklist & Electron/CEF False Positive Elimination
- Rather than maintaining an endless blacklist of desktop applications, the engine requires positive game verification:
  1. **Xbox Game Bar / GameConfigStore Integration**: Scans `HKCU\System\GameConfigStore\Children`, `Parents`, and `HKCU\Software\Microsoft\GameBar` for verified game registrations. Provides real-time lookup via `IsRegisteredInGameConfigStore(fullPath, exeName)`.
  2. **Recognized Gaming Directories**: Validates known library paths (`steamapps\common`, `XboxGames`, `Epic Games`, `Riot Games`, `Ubisoft Game Launcher`, `GOG Galaxy\Games`, `Battle.net`, `Origin Games`, `WindowsApps\...\Games`).
  3. **3D Engine Signatures**: Recognizes `UnityPlayer.dll`, `*-Win64-Shipping.exe`, `Discovery.exe`, etc.
  4. **Electron / CEF Rejection**: Desktop web containers (e.g. VS Code, Codex, Discord, Slack, Spotify) containing `resources\app.asar`, `node.dll`, `ffmpeg.dll`+`d3dcompiler_47.dll`, or `icudtl.dat` are rejected unless explicitly registered in GameConfigStore.

---

## 5. Modern Standby (S0ix) & Timer Resolution Subsystems

### Kernel ETW Session Lifecycle
- During Modern Standby / Sleep transitions (`SystemEvents.PowerModeChanged`), the Desktop Activity Moderator (DAM) freezes user processes.
- If an active ETW kernel trace session (`Microsoft-Windows-DXGI`) remains open across standby transitions, it causes kernel queue deadlocks and Event 506 standby aborts.
- VectorPowerHub intercepts `PowerModes.Suspend` and synchronously issues `CloseTrace` and `ControlTraceW(..., EVENT_TRACE_CONTROL_STOP)` before system sleep, restoring the session 2.0s after resume.

### Timer Resolution Isolation
- Windows timer resolution requests (`GlobalTimerResolutionRequests` / `timeBeginPeriod(1)`) prevent CPU sub-states from entering deep package C-states.
- VectorPowerHub avoids artificial global timer resolution forcing, allowing the operating system to sleep at nominal 15.6ms ticks during idle.

---

## 6. Verification Checklist Before Pull Request

- [ ] `rtk python tools/count_lines.py` confirms all `src/*.cs` files are $\le$ 200 lines.
- [ ] Built cleanly with `rtk make`.
- [ ] Headless verification passed: `cmd /c "bin\VectorPowerHub.exe /test"`.
- [ ] No NVML / nvidia-smi calls added in idle or background loops.
- [ ] Graphify updated: `graphify . --code-only` and `graphify cluster-only .`.
