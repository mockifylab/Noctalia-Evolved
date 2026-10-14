# Noctalia → Windows 11 port — implementation assessment

Date: 2026-10-14 · Author: lead engineering agent · Status: approved architecture, pre-milestone-1

Sources inspected:
- `noctalia/` — cloned from https://github.com/noctalia-dev/noctalia @ main (shallow), **MIT License** (Copyright (c) 2026 noctalia-dev)
- https://github.com/eythaann/Seelen-UI — **AGPL-3.0** → studied for Windows integration patterns only; **no code, assets, or distinctive design will be copied**
- The former Rust/egui `noctalia-shell` repo now redirects to the same C++ monorepo — no reusable egui code exists upstream anymore.

## 1. What Noctalia actually is

- **Language/build:** C++23, Meson + ninja, clang/gcc, Nix flake. ~1,311 files / ~184k LOC under `src/`.
- **UI framework:** a custom in-repo scene graph + retained widget tree (`src/ui/`, `src/shell/`) rendered by a **hand-written OpenGL ES backend** (rect/blur/shadow glyph shaders) on **Wayland EGL surfaces**. Text via **Pango/Cairo/FreeType**; SVG via librsvg. No Qt/GTK.
- **Platform coupling:** Wayland layer-shell (`wlr-layer-shell-unstable-v1`), compositor IPC (Hyprland/niri/sway/mango protocols in `protocols/`), D-Bus (sdbus-c++, StatusNotifier tray, MPRIS-like services), PipeWire/WirePlumber (audio), libinput-side `xkbcommon`, PAM/polkit (lock screen), `wlr-screencopy` (screenshots), inotify, libsecret, network managers, `/sys` brightness — all Linux-only.
- **Concave panels:** implemented as per-corner `CornerShape::Concave` in a generic rect shader + surface input-region tessellation. **All shape math is pure logic** in headers we can port verbatim (MIT):
  - `src/shell/bar/bar_corner_shape.h` — bar concave corner/inset/bulge rules for all 4 dock positions, incl. `concave_edge_corners` and margin flare behavior.
  - `src/ui/style.h` — exact tokens: bar thickness 34, radii 3/6/9/12, spacing 4/8/12/16, control heights 32/38/44, font sizes 11–20, anim durations 100/200/400 ms.
  - `src/ui/palette.h` — 16 color roles (Primary…OnHover) with TOML token names.
  - Default dark palette is exact in `src/theme/builtin_palettes.cpp` (surface `#0B0E14`, primary `#E6B450`, …).
- **Config:** TOML (`example.toml`, 617 lines = full default schema), parsed with toml++; schema + defaults + migrations + override persistence (`settings.toml`) + state (`state.toml`) in `src/config/`. Key names/defaults/enum values are portable *as a specification*.
- **Launcher:** provider-based; XDG desktop-entry scanning + vendored **fzy** fuzzy scorer (MIT).
- **Assets:** `assets/fonts/noctalia-tabler.ttf` — Tabler Icons font, **MIT** (license file present; attribution kept). Logo/owl artwork is project branding — used only for attribution, not republished as our own.

## 2. Can the C++ UI code be reused on Windows?

**No, not practically.** The rendering path is GLES-on-Wayland-EGL; surfaces are layer-shell; input/text is Pango; every service is D-Bus/Linux-API. Building it on Windows would require porting the entire compositor-facing substrate (essentially writing a Wayland-runtime for Windows) — a multi-year effort with no user-visible benefit over a thin reimplementation.

**What we reuse (MIT, with copyright preserved):**
1. **Geometry/shape logic** — port `bar_corner_shape.h` and rect-shape semantics to C# (bit-for-bit behavior, unit-tested).
2. **Design tokens** — `style.h` constants, `palette.h` color roles, builtin palettes → transcribed to code.
3. **Configuration contract** — TOML section/key names, defaults, enum values, and settings-registry structure from `src/config/` + `example.toml`, so user configs read naturally and future sync backports are possible.
4. **Icon font** — Tabler font + `tabler.json` glyph registry.
5. **Interaction models** — widget list (`src/shell/bar/widgets/*`: clock, media, audio, network, battery, bluetooth, brightness, clipboard, control center, launcher, notification, sysmon, taskbar, tray, weather, spacer, custom button…), panel lifecycle (`panel_manager`), settings UX (`src/shell/settings/`).

## 3. Chosen Windows architecture

**C# / .NET 10 (WPF)** — rationale:
- The machine already has the .NET 10 *WindowsDesktop* runtime; only the SDK must be installed (winget). WPF gives hardware-accelerated, anti-aliased **vector `Path` geometry** — the natural match for concave corners/flare arcs — plus per-monitor-V2 DPI, multi-monitor support, animations, and borderless layered windows.
- Alternatives considered: **Rust+egui** (no upstream egui code exists to reuse anymore; translucency/shape fidelity harder), **Tauri/webview** (would mirror Seelen's stack; heavy runtime, no Noctalia reuse, AGPL adjacent ecosystem), **WinUI3** (Mica/Acrylic are native but shape/layered-window control is more awkward; packaging friction). WPF + P/Invoke to documented Win32 APIs is the best fidelity/maintainability tradeoff. (Direct2D-backed acrylic is a documented later milestone if DWM backdrops prove incompatible with concave transparency.)

Layout:

```
noctalia-win/                     (solution)
  src/NoctaliaWin.App/            WPF UI: bar, panels, launcher, settings, tray (no OS calls inside controls)
  src/NoctaliaWin.Core/           platform-independent: config schema+validation, corner geometry, fzy port,
                                  palettes, service interfaces:
                                  ISystemStatusService, IMediaService, IAudioService, INetworkService,
                                  IBatteryService, INotificationService, IApplicationDiscoveryService,
                                  IWindowPlacementService, IStartupService, ISettingsStore, IHotkeyService
  src/NoctaliaWin.Windows/        all Win32/COM interop implementations (taskbar control, tray, core-audio,
                                  WNB/NetworkList/Battery APIs, ShellExecute, registry Run key, WH_KEYBOARD_LL)
  tests/                          xUnit: config round-trip/validation/migration, corner-shape math, app search,
                                  settings store persistence
```

Config location: `%LOCALAPPDATA%\NoctaliaWin\` with `config.toml` (schema-compatible key names), `settings.toml` overrides, `state.toml`, schema version field, unknown-key tolerance, and restore-defaults.

## 4. Major Linux → Windows replacements

| Noctalia (Linux) | Windows equivalent |
|---|---|
| layer-shell panel surfaces | borderless `WS_POPUP` WPF windows, positioned via work-area math |
| compositor blur region | layered-window translucency; DWM backdrop / screenshot-blur brush evaluated per surface (blur is the known risk, see §5) |
| reserved output area | `INotificationArea` (appbar) via `SHAppBarMessage` where feasible; else work-area offset (reversible) |
| Hyprland/niri workspace+window IPC | `EnumWindows` + `IVirtualDesktopManager`-free workspace model (Windows has no workspaces natively; v1 = window switcher from Alt-Tab enumeration, VD integration later via documented `VirtualDesktopManager` COM) |
| wlr-screencopy | `Windows.Graphics.Capture` |
| PipeWire/WirePlumber | Core Audio endpoint COM APIs (master volume/mute, per-app) |
| status-notifier tray | `Shell_NotifyIcon` for our app + `TaskbarCreated` re-registration |
| XDG desktop entries + apps | Start Menu `.lnk` scan + shell:AppsFolder (UWP) enumeration; `ShellExecute` launch |
| D-Bus MPRIS | `SystemTransportMediaControlsPlayback` (Windows.Media.SMRTP media session API) |
| networkmanager/pulseaudio etc. | `NetworkListManager`, `GetSystemPowerStatus`, `WlanEnumInterfaces`, Bluetooth APIs |
| PAM/polkit lock screen | `LockWorkStation()`; no PAM equivalent needed |
| inotify hot reload | `FileSystemWatcher` |
| systemd-run autostart | `HKCU\...\Run` registry value (own app only, removable) |
| native taskbar hide | `ITaskbarList::HideTaskbar` — **explicit, reversible user setting**, restores on exit/crash via re-show on unhandled-exit + tray fallback; never patches explorer |

## 5. Risks

1. **Concave corners + true background blur together.** DWM acrylic doesn't clip to a layered-window's alpha shape. Mitigation ladder: (a) translucency-only (Noctalia's `opacity` still matches), (b) manual desktop-screenshot blur brush behind the notch, (c) DWM backdrop for rectangular regions. Visual fidelity of the *shape* is prioritized (shape is the signature look).
2. **Fullscreen-app etiquette:** top-most bar must yield in games; needs `QueryFullAccess`-style foreground checks (documented SHQueryUserNotificationState).
3. **Keyboard hooks for launcher hotkey:** `WH_KEYBOARD_LL` is documented but must be fail-safe (unhook on exit).
4. **Taskbar hide recovery:** documented `ITaskbarList`; explicit user opt-in, auto-restore on exit, README recovery steps.
5. **Scale fidelity chase:** Noctalia's precise text metrics were tuned for Pango; Segoe UI Variable + our own line metrics will need per-widget visual iteration against upstream screenshots.
6. **Scope:** upstream is a full shell (lock screen, screenshots, clipboard manager, plugins, weather, calendar). We port incrementally; anything not built is labeled placeholder in the README, never faked.

## 6. Milestone plan

- **M1 (this):** project scaffold + SDK install; top bar (real clock, network/battery/volume/system status, tray, launcher+settings buttons) with concave edge corners; one attached concave panel (clock/calendar or quick-settings) with reveal animation + outside-click/Escape dismissal; launcher window (fzy-port search over installed apps, Enter launches); settings window v0 (bar position/thickness/radius/concave toggle/opacity, persist to TOML, restore defaults); xUnit tests for config validation + corner geometry + search. Tray icon + startup toggle. README w/ build/run/recovery.
- **M2:** concave panels for all bar modules, control center, media + audio (Core Audio/SMTC), notifications inbox (WinRT `UserNotificationListener` only if user opts in).
- **M3:** dock + taskbar widget (window list), app icons + colorization, wallpaper + Material-You palette generation from wallpaper (port of vendored M3 utils logic, Apache-2.0 noted), blur-brush pass 2.
- **M4:** launcher categories/actions parity, bar widget editor parity, hot-reload, multi-monitor per-bar overrides, taskbar-hide polish, screenshots OSD, clipboard panel.
- **Later:** virtual desktops integration, weather/night-light/brightness, settings parity for all `example.toml` sections.

## 7. License/attribution obligations

- Keep Noctalia MIT copyright notice in `THIRD-PARTY-NOTICES.md` for ported logic + assets; keep Tabler MIT notice (already vendored with font).
- Material Color Utilities: Apache-2.0 attribution if/when ported (M3).
- Seelen UI: AGPL-3.0 — studied for techniques only; nothing copied.
- Our repo will ship the ported MIT code with its notices intact and label the Windows port as a derivative/compatibility project, not affiliated with noctalia-dev.
