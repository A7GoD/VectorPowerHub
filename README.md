# VectorPowerHub ⚡

> **High-Performance Telemetry & Dynamic Power Hub for the MSI Vector 16 HX**  
> Custom engineered for Intel Core Ultra 9 275HX (Arrow Lake-HX: 8 Lion Cove P-cores + 16 Skymont E-cores) and NVIDIA GeForce RTX 5070 Mobile.

---

## Overview

**VectorPowerHub** is a lightweight, zero-overhead Windows telemetry HUD, power profile manager, and automated tuning suite built specifically for hybrid laptop architectures. It delivers real-time DXGI kernel present monitoring, per-core CPU frequency/utilization tracking, and intelligent dGPU power state management without the bloat of manufacturer software.

---

## Key Features

### 1. 🎮 Real-Time In-Game FPS via ETW DXGI Hook
- **Zero In-Game Overhead**: Hooks into the Windows kernel ETW provider `Microsoft-Windows-DXGI` (`{CA11C036-0102-4A2D-A6AD-F03CFED5D3C9}`) Event 42 (`IDXGISwapChain::Present`).
- **GameConfigStore Integration**: Cross-references detected rendering binaries against Windows Game Service (`HKCU\System\GameConfigStore\Children`).
- **Foreground Verification & Hysteresis**: Validates active foreground window (`GetForegroundWindow`) with a 2.0-second sustained rendering hysteresis (>15 FPS) to completely eliminate false positives from browsers, video players, and background installers.

### 2. ⚡ Dynamic Profile Engine & Power Balancing
- **⚡ Snappy-Pacing (Default / Competitive)**: Unconstrained P-core and E-core clocks, aggressive Windows Boost Mode 4, EPP 30%. Maximizes competitive framerates and eliminates 1% low frame stutter.
- **🎯 Sweet-Spot Efficiency**: Clamped 4.9 GHz P-Core, 2.8 GHz E-Core, 58W target package draw. Eliminates thermal throttling and leaves full power headroom for the dGPU.
- **❄️ Cold & Quiet**: Clamped 2100 MHz GPU curve, EPP 20%, Boost Mode 3. Keeps thermals under 74°C with whisper-quiet fan curves.
- **🛠️ Custom Tuner**: Interactive on-demand drawer allowing direct granular control of P-core limits (3.0–5.4 GHz), E-core limits (2.0–3.8 GHz), Windows EPP (0–100%), Boost Mode, and GPU frequency caps.

### 3. ❄️ dGPU D3cold PCIe Link Protection
- Unlike standard hardware monitors that keep the discrete GPU awake with polling loops, VectorPowerHub monitors external display status (`QueryDisplayConfig`) and gates NVML queries.
- When on internal display in desktop mode, the RTX 5070 enters deep **PCIe D3cold Sleep (0.0W)**.
- When an external display (e.g. BenQ EX271Q) is driven directly by the dGPU, it reflects active status and reads power dynamically.

### 4. 🧩 24-Core Arrow Lake-HX Topology Grid
- Visual matrix of all 24 physical cores:
  - **8 Lion Cove Performance Cores** (P00–P07): Dynamic Cyan indicators (`#00F2FF`).
  - **16 Skymont Efficient Cores** (E00–E15): Dynamic Amber indicators (`#FF9F00`).
- Compact dual-tier layout fitted to viewport with per-core GHz, % load, and instantaneous power split.

### 5. ⏱️ 1-Click Automated Benchmark Suite
- Multi-iteration automated benchmark runs (5, 10, or 20 samples).
- 3-second warmup countdown to exclude shader compilation stutters.
- Outlier filtering engine: eliminates power cuts and loading screen anomalies.
- Produces a comparison table showing Raw vs. Cleaned 1% Lows, average FPS, total platform draw, and efficiency score.

### 6. 🛡️ Single-Instance Guard & Modern Standby Safety
- **System-wide Named Mutex (`VectorPowerHub_SingleInstance_Mutex`)**: Prevents duplicate UI windows or ghost tray icons. Secondary launches signal the primary instance via `EventWaitHandle` (`VectorPowerHub_WakeEvent`) and registered Windows message (`WM_SHOW_HUB`), bringing the existing window to front in <30ms.
- **Sleep / Modern Standby Interceptor**: Listens to `SystemEvents.PowerModeChanged` to gracefully tear down ETW kernel sessions before sleep (Event 506 Standby entry), preventing WHEA deadlocks and BSODs.

---

## Tech Stack & Architecture

- **Language**: C# 5.0 (.NET Framework 4.0+ compatible, 64-bit)
- **UI Engine**: Pure GDI+ double-buffered anti-aliased custom WinForms (`#121318` dark slate palette, zero flickers)
- **Telemetry Sources**:
  - `pdh.dll`: `\Energy Meter(RAPL_Package0_PKG)\Power`, `\Processor Information(0,*)\% Processor Performance`
  - `advapi32.dll`: ETW `OpenTraceW`, `ProcessTrace`, `ControlTraceW`
  - `nvml.dll`: Dynamic runtime load for NVIDIA telemetry with D3cold gating
  - `user32.dll` / `dwmapi.dll`: Drop shadows, borderless hit testing, foreground window checks

---

## Building from Source

VectorPowerHub uses GNU `make` and the built-in Windows 64-bit `csc.exe` compiler with **0 external NuGet dependencies**:

```cmd
make           # Build bin/VectorPowerHub.exe
make test      # Run headless telemetry self-test
make run       # Launch the application
make clean     # Remove build artifacts
```

### Make Targets & Verification

- **Headless Telemetry Test**:
  ```cmd
  make test
  ```
- **Direct Tab Launch**:
  ```cmd
  make topology     # Opens directly to 24-Core Topology view
  make bench        # Opens directly to Benchmark suite
  ```
- **Offscreen UI Rendering (Visual QA)**:
  ```cmd
  make render       # Generates tab0_preview.png, tab1_preview.png, tab2_preview.png in previews/
  ```
- **Knowledge Graph**:
  ```cmd
  make graphify     # Re-extracts AST and updates graphify knowledge graph
  ```

---

## License

MIT License. Engineered for maximum frame pacing consistency and silicon longevity.
