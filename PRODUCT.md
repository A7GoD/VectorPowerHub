# Product

<!-- impeccable:product-schema 1 -->

## Platform
web

## Users
Gamers and hardware enthusiasts tuning Intel Core Ultra 9 275HX (Arrow Lake-HX) and NVIDIA RTX 5070 Mobile laptops for maximum competitive frame rates, optimal power-efficiency pacing, and whisper-quiet thermals.

## Product Purpose
Vector Power Hub provides real-time hardware telemetry (ETW DXGI in-game FPS, RAPL CPU package power, per-core Lion Cove / Skymont topology, BenQ external display & internal panel D3cold/D0 GPU status), 1-click dynamic power profile switching (Snappy-Pacing default), and automated multi-run benchmarking with outlier elimination.

## Positioning
Unlike generic polling utilities that inadvertently wake discrete mobile GPUs from low-power D3cold sleep or produce skewed benchmark averages from power cuts, Vector Power Hub provides zero-overhead ETW/RAPL telemetry, explicit BenQ display pipeline detection, and disciplined 215W platform ceiling management.

## Operating Context
- Windows 11 x64 on MSI Vector 16 HX (Arrow Lake-HX 24 cores: 8 Lion Cove P-cores + 16 Skymont E-cores, RTX 5070 Mobile).
- Dual display support: Built-in 240Hz display and BenQ EX2710Q external QHD 165Hz G-Sync monitor.
- Runs in system tray HUD and high-DPI desktop overlay during gaming sessions.
- Interfaces directly with ETW, PDH RAPL, NVML, and BenQ display state queries.

## Capabilities and Constraints
- Default Active Profile: "Snappy-Pacing" (Mode 4 Efficient Aggressive boost, EPP 30%, uncapped P/E clocks).
- Profiles: Snappy-Pacing (Active), Sweet-Spot Efficiency (4.9 GHz clamp, EPP 25%), Cold & Quiet (GPU clamp 2100 MHz, Mode 3), Desktop Idle (EPP 50%).
- Real-time ETW DXGI Present frame rate tracking (Event 42).
- Dynamic D3cold/D0 state detection (distinguishes BenQ external display active D0 vs true D3cold sleep).
- 1-Click Multi-Run Automated Benchmark Suite with power-cut and loading outlier elimination.
- Full 24-core Lion Cove (P0-P7) and Skymont (E00-E15) per-core live frequency and load topology.

## Brand Commitments
- Modern dark cyberpunk/sleek telemetry aesthetic: Charcoal/Slate background (`#121318`), elevated cards (`#1B1E26`), subtle slate borders (`#2D3242`).
- Vibrant high-contrast accent palette: Electric Cyan (`#00F2FF`) for FPS/GPU, Amber Gold (`#FF9F00`) for CPU, Royal Purple (`#A855F7`) for Power Split, Emerald Green (`#00E676`) for active state, and Warning Coral (`#FF5252`) for platform ceiling limits.

## Product Principles
1. Never poll or wake sleeping hardware unnecessarily (preserve battery and thermal headroom).
2. Measure ground truth: ETW Present calls for FPS, RAPL energy meter for Package Watts.
3. Zero clutter: immediate glanceability, clear visual hierarchy, consistent typography and dark-theme discipline.
4. Robustness: Outlier detection in benchmarks, clean thread exit, fail-safe fallback to stock power management.
