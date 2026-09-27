# Vector Power Hub - UI/UX Design System

## Aesthetic & Tone
**Modern Minimalist Pro Tool**
- Sleek, flat, and subdued accents.
- Focus strictly on clean data legibility and high contrast text (using `Consolas` for numeric data).
- Eliminates visual noise in favor of immediate data processing.

## Architecture & Layout
**Modular Multi-Window Architecture**
- The main telemetry hub, OS tweaks panel, and Graphing utility are kept as distinct, separate windows.
- Enables tear-away positioning across multi-monitor setups (e.g., Tweaks on one monitor, Telemetry overlay on another).

## Data Visualization
**Limit/Bottleneck Focused**
- Instead of forcing the user to interpret raw numbers, the UI actively calculates and highlights the limiting factor.
- Warning states (yellow/red text or borders) trigger dynamically when hitting CPU thermal limits, GPU power limits, or game engine FPS caps.
- **Graphing Window:** Uses Multi-Track Synchronized Lanes. CPU power, GPU power, and FPS run on separate, parallel horizontal bands with smooth gradient fills to prevent overlapping spaghetti-lines.

## Controls & Interactivity
**Custom, Seamless Controls with Lightweight Transitions**
- Replaces native Windows `TrackBar` elements with custom-painted, dark-themed sliders that blend seamlessly into the application.
- Employs lightweight eased transitions (subtle hover fades, pulsing record indicators) to make the UI feel fluid and responsive without burning DWM cycles.

## Empty States
**Elegant Standby States**
- When a game is not active or telemetry is not yet flowing, the UI gracefully dims.
- Displays "Awaiting Telemetry..." with skeleton outlines instead of jarring "0" values, clearly communicating the standby state of the application.
