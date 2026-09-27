# Vector Power Hub - UI/UX Upgrade Tasks

## 1. Custom Controls & UI Primitives
- [ ] Create `CustomSlider.cs` (Replaces `TrackBar`, dark-themed, seamless).
- [ ] Update `GlowButton.cs` for lightweight eased transitions (subtle hover fades, pulsing record state).

## 2. Empty States & Awaiting Telemetry
- [ ] Add `IsAwaitingTelemetry` state to `PowerCoreEngine`.
- [ ] Update `FpsHeroCard.cs` to show dimmed skeleton state when inactive.
- [ ] Update `CpuTelemetryCard.cs` and `GpuTelemetryCard.cs` to dim/show "Awaiting Telemetry".

## 3. Limit/Bottleneck Focus (Colorization)
- [x] Calculate bottlenecks (e.g., CPU Temp > 90C, GPU Power > Max, FPS == Cap).
- [x] Update card drawing logic to render warning states (Yellow/Red) instead of default colors when limits are hit.

## 4. Multi-Track Graphing Window
- [ ] Refactor `VectorPowerHubGraphForm.cs` and `GraphCanvas` logic.
- [x] Separate lanes for CPU Power, GPU Power, and FPS.
- [x] Implement smooth gradient fills for area charts.

## 5. View Implementations
- [ ] Replace all `TrackBar` instances with `CustomSlider` in `VectorPowerHubForm.TopologyView.cs` and `VectorPowerHubForm.CustomTuner.cs`.
