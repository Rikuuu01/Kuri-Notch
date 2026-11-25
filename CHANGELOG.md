# Changelog

All notable changes to **Kuri (DynamicNotchWindows)** are documented in this file.

---

## [v1.3.0] — 2026-11-25

### ✨ New Features
- **Native Windows Notification Sync (`wpndatabase` SQLite Engine)**:
  - Real-time 400ms notification listener intercepting Windows Action Center / toast notifications with app metadata and body text.
  - Multi-message notification queue (`(Id, ArrivalTime)` tracking) for rapid consecutive notifications.
  - Two-tiered dropdown notification banner (`NotificationPopupCard`) that pops smoothly beneath the notch without interrupting active collapsed widgets.
  - Notifications also pop out automatically when **Manual Hide (Slim Bar)** mode is active on the desktop (while staying silent in fullscreen).
- **Unified System HUD**:
  - Live notch HUD indicators for **Volume**, **Brightness**, **Caps Lock**, and **Num Lock** state changes.
- **OTA Auto-Updater**:
  - Added GitHub release update checking, in-notch update prompt, and dedicated **Updates** panel in Settings (including local test manifest detection and simulation mode).
- **Liquid Glass (Acrylic) Material**:
  - Transformed the `Acrylic` background material into an iOS/macOS-inspired **Liquid Glass** aesthetic featuring ~80% frosted crystal translucency, bright specular rim (`NotchStrokeBrush`), dynamic diagonal glare sheen (`NotchLiquidGlassSheenPath`), and translucent frosted Bento cards (`NotchCardBackgroundBrush` / `NotchCardBorderBrush`).
- **Manual Hide-to-Slim-Bar Toggle**:
  - Added an **Eye / Hide** button beside the **Pin** button in the expanded notch header, allowing users to tuck the notch into a subtle slim bar on the desktop with instant hover-peek (while preserving the 3-second hold-to-peek safety delay in fullscreen).

### 🛠️ Improvements & Bug Fixes
- **Theme & Material Synchronization**:
  - Fixed `ThemeManager` ignoring glass transparency and hiding `NotchStrokeBrush` when `IsAccentColorEnabled` was `false`, which previously caused `OLED`, `Tinted Glass`, and `Acrylic` to look virtually identical.
  - Synchronized `IsAccentColorEnabled`, `CustomAccentColorHex`, and `NotchMaterial` on startup (`App.xaml.cs`) and inside `NotchViewModel.ApplySettings`.
- **Screen Time & Posture Monitor Overhaul**:
  - Fixed 24-hour histogram bar scaling (`BarHeight` proportional up to `42` DIPs instead of clamping percentage values at `32px`) and aligned `00`, `06`, `12`, `18`, `23` hour labels directly beneath their 24 `UniformGrid` columns.
  - Fixed left alignment of application names in **Most Used Apps** (`HorizontalAlignment="Left"`) and replaced the fixed-width bar with a full-column proportional `ProgressBar` and localized category tooltips.
  - Renamed duplicate `"SCREEN TIME"` header on the bottom card to `"POSTURE MONITOR"` (`"MONITOR POSTUR"` in Indonesian), localized `"Stand"`, `"Snooze 2m"`, `"Next break"`, and dynamic target interval (`Water.StandardIntervalText`).
  - Fixed broken button bindings on **Stand** (`Water.ResetSittingCommand`) and **Snooze 2m** (`Water.SnoozeCommand`).
  - Kept passive sitting/posture tracking active even when Water Reminder popup alerts are turned off.
  - Added thread-safe `CloneDayData` snapshots in `ScreenTimeService` and `IsUiActive` UI throttling in `ScreenTimeViewModel`.
  - Increased `ExpandedScreenTimeHeight` (`374.0`) so **Daily Activity Stats** never clips at the bottom edge.
- **Notch Core, Hover, Privacy & Display Stability**:
  - Fixed expanded notch hover/collapse glitches using exact geometry hit-testing (`NotchBackgroundPath.Data.FillContains`) + expanded hover watchdog, keeping the notch open on unpin until the cursor leaves.
  - Fixed single-monitor opening glitch, bottom menu clipping, and false-positive microphone privacy indicator detection (`PrivacyService`).
  - Synced companion pet mascot alerts smoothly across collapsed, alert, and expanded notch states.

---

## [v1.2.0] — 2026-11-09

- Initial v1.2 release with expanded bento widgets, Screen Time tracker, Weather & Prayer schedules, Lyrics Karaoke sync, Drop Shelf & Phone Share, and Universal/x64/x86 installers.
