---
name: Vector Power Hub
description: Real-time telemetry, 24-core Arrow Lake-HX topology, and dynamic power profiles for MSI Vector 16 HX
colors:
  background: "#121318"
  card-bg: "#1B1E26"
  card-border: "#2D3242"
  text-primary: "#FFFFFF"
  text-secondary: "#8F9CAE"
  text-muted: "#5A6578"
  accent-cyan: "#00F2FF"
  accent-gold: "#FF9F00"
  accent-purple: "#A855F7"
  accent-green: "#00E676"
  accent-coral: "#FF5252"
  tab-inactive-bg: "#161820"
  tab-active-bg: "#1F232D"
typography:
  display:
    fontFamily: "Segoe UI"
    fontSize: "26pt"
    fontWeight: 700
  headline:
    fontFamily: "Segoe UI"
    fontSize: "14pt"
    fontWeight: 700
  title:
    fontFamily: "Segoe UI"
    fontSize: "11pt"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI"
    fontSize: "9pt"
    fontWeight: 400
  label:
    fontFamily: "Segoe UI"
    fontSize: "8pt"
    fontWeight: 600
rounded:
  sm: "6px"
  md: "10px"
  lg: "14px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
components:
  card:
    backgroundColor: "{colors.card-bg}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: "16px"
  button-primary:
    backgroundColor: "{colors.accent-cyan}"
    textColor: "{colors.background}"
    rounded: "{rounded.sm}"
  badge-active:
    backgroundColor: "{colors.accent-green}"
    textColor: "{colors.background}"
    rounded: "{rounded.sm}"
---

## Overview
Vector Power Hub is a precision Windows hardware tuning and telemetry suite for Intel Core Ultra 9 275HX and NVIDIA RTX 5070 Mobile. The visual language emphasizes deep dark-mode contrast, legible monospace and high-visibility telemetry figures, and color-coded subsystem metrics.

## Colors
- **Background (`#121318`)**: Deep neutral slate that reduces eye strain and provides infinite contrast for fluorescent accents.
- **Surface / Cards (`#1B1E26`)**: Elevated panels with 1px border (`#2D3242`) for crisp separation without visual clutter.
- **Accents**:
  - **Electric Cyan (`#00F2FF`)**: Frame rate (ETW DXGI), GPU telemetry, and primary CTA actions.
  - **Amber Gold (`#FF9F00`)**: CPU Package Power, P-Core / E-Core clocks, and computing load.
  - **Royal Purple (`#A855F7`)**: Power split distribution and platform balance indicators.
  - **Emerald Green (`#00E676`)**: Active profile, healthy telemetry state, and benchmark completion.
  - **Coral Red (`#FF5252`)**: Power ceiling threshold (215W) warning and outlier rejection.

## Typography
- **Font Family**: Segoe UI across all controls for crisp native ClearType rendering.
- **Metric Big Numbers**: 24pt–28pt Bold for instant glanceability (e.g. 161.9 FPS, 197W Platform).
- **Subsystem Labels**: 9pt–11pt Semi-bold uppercase with muted secondary text (`#8F9CAE`).
- **Data Tables & Core Grid**: 8pt–9pt Regular with colored progress indicators.

## Layout
- **Tabbed Interface**:
  - Tab 0: Power Profiles HUD (Glanceable telemetry cards + 4 interactive profile selectors).
  - Tab 1: Automated Benchmark Suite (Iteration controls, outlier rejection, live progress bar, aggregate statistics).
  - Tab 2: Arrow Lake-HX Topology (8 Lion Cove P-cores + 16 Skymont E-cores in a dual-column responsive grid).
- **Responsive Geometry**: Automatic proportional scaling on resize, preserving padding and card boundaries.

## Elevation & Depth
- Flat modern Windows GDI+ rendering using subtle border outlines (`#2D3242`) and contrasting surface fills rather than heavy drop shadows.
- Active profile card features an illuminated 2px colored accent border (`#00F2FF`) and badge.

## Shapes
- Rounded rectangles with 8px–10px corner radius on cards and buttons.
- Progress bars use smooth rounded pills for fluid power and load visualization.

## Components
- **Telemetry Card**: Container with header label, large metric readout, sub-metric badges, and load bar.
- **Profile Selector Card**: Clickable card with status pill ("ACTIVE", "APPLY"), description, and power parameters.
- **Topology Core Tile**: Compact core box displaying Core ID, type badge (P/E), real-time GHz, and load bar.
- **Benchmark Panel**: Controls for test duration, iterations, power cut detection threshold, and results summary.

## Do's and Don'ts
- **DO** use Electric Cyan for GPU/FPS and Amber Gold for CPU metrics consistently.
- **DO** keep background completely dark to prevent jarring bright flashes during window resizing.
- **DON'T** use low-contrast gray text on dark gray backgrounds (maintain WCAG AA minimum 4.5:1 ratio).
- **DON'T** let text clip on high DPI screens; always measure string bounds dynamically.
