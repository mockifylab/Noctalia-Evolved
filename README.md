# Noctalia Win

A native Windows 11 desktop app porting the visual design and interaction model of
[Noctalia Shell](https://github.com/noctalia-dev/noctalia) — the concave-corners top bar
and its pop-out panels — with Windows-native system integration behind clean interfaces.

This is **not** a Linux desktop environment on Windows, and **not** a re-skin of the Windows
shell. It reuses Noctalia's geometry, design tokens and config contract where the license
permits, and replaces Wayland/Linux mechanisms with documented Windows APIs.

> **Status: the bar, its pop-out panels, the launcher and the settings window run.**
> `dotnet run --project src/NoctaliaWin.App` shows a Noctalia bar on every monitor — concave
> silhouette, Search and Quick settings buttons, clock, network, volume, battery, auto-hide,
> tray presence — and clicking a widget opens the concave panel behind it: a real month
> calendar under the clock, a quick-settings panel that reads and writes the actual master
> volume, and a launcher that lists the installed applications, filters them as you type and
> starts the selected one with Enter. The launcher also answers a global `Ctrl+Alt+Space` from
> anywhere on the desktop. The settings window is laid out the way Noctalia's is — search,
> section list, grouped cards — and every row in it writes `config.toml` and redraws the bar.
> 476 tests pass. See [Current state](#current-state).

## Current state

| Area | State |
|---|---|
| Concave bar geometry, design tokens, palettes | Done, ported from Noctalia and unit-tested |
| Config (`config.toml`) load/save/validate/migrate | Done, unit-tested; a hand edit in an outside editor is watched for and reloaded |
| fzy fuzzy search + launcher ranking | Done, unit-tested |
| Monitors, DPI, power, network, audio, startup, tray | Done against documented Win32/COM APIs |
| Bar window (position, scale, auto-hide, widgets) | Runs; clock/network/volume/battery/spacer show live data |
| Concave pop-out panel | **Runs**; calendar (clock) and control center (volume/network/battery), attached or floating, with reveal, Escape/outside-click dismissal, one-open rule and per-monitor placement. `borders` turns the outline on and off and `border_width` sets its thickness in logical pixels |
| Launcher | **Runs**; the same panel surface, listing Start Menu and `App Paths` entries, fuzzy-filtered, keyboard-driven, launching through the shell. Left-clicking the tray icon opens it too |
| Global hotkey | **Runs**; `shell.launcher.hotkey` (default `CTRL+ALT+SPACE`) toggles the launcher through `RegisterHotKey` on a message-only window. The Launcher section's `Show launcher` row rebinds it, and a rebuild re-registers it; if Windows will not hand the combination over, the tray tooltip says the shortcut is unavailable |
| Settings window | **Runs**; Noctalia's layout (search, section list, grouped cards), seven sections, and the row kinds upstream uses — switches, pills, dropdowns, sliders with a typed read-out, text fields, a key-capture box, a scheme grid, widget lanes, reset-per-row and restore-all-defaults. Every row writes `config.toml` through the same editor the tests round-trip, and the bar rebuilds from it |

Widgets that have no Windows backing yet are **skipped and reported**, never drawn as empty
tiles: `media`, `clipboard`, `bluetooth`, `brightness`, `wallpaper`, `workspaces`, `text`,
`tray` and `notifications`. The last two are on that list because reading another process's
status-bar icons or toast content has no supported API. `launcher` and `control-center` are
drawn as bar buttons with a word instead of Noctalia's icon-font glyph; `settings` is a bar
button that opens the settings window, and `session` is named in the catalog but has no button
yet.

What the panels knowingly do not do: `transparency_mode = "glass"` sets the card's opacity but
cannot blur what is behind it, because WPF exposes no documented backdrop blur for a layered
window; `shadow` is inert for panels as it is for the bar; the control center's height is
the height its rows actually occupy rather than a measure of live content, so a panel with
more rows later will need the same constant revisited; and the launcher lists names and paths
as text, because `show_icons` would mean loading shell icon handlers that this build does not
call yet.

Two bar options are knowingly inert: `shadow` (a drop shadow needs a bleed margin the
placement math does not reserve yet) and `reserve_space` (shrinking the real work area means
registering an appbar, which is a shell modification this port does not make by default).
The bar always stays inside the work area, so it can never cover the Windows taskbar.

## Requirements

- Windows 11 (build 22621+)
- .NET 10 SDK with the Windows Desktop workload (`winget install Microsoft.DotNet.SDK.10`)
- Git

No admin rights are needed to build or run.

## Build

```powershell
dotnet build NoctaliaWin.slnx
```

The version every project carries — the one the settings window's `Version` row shows, and the
one a packaged build or an update check would compare — is set once, in
`Directory.Build.props`.

## Test

```powershell
dotnet test tests/NoctaliaWin.Tests
```

Most of the suite is platform-independent (geometry, config, search, bar layout) and runs
anywhere. The `NoctaliaWin.Tests/Windows` tests need Windows because they exercise the real
service layer: some inject a fake reader for the decision logic, and a few read the live
monitor list, power status, `App Paths` key, default audio endpoint and tray. The live tests
only ever read; none of them change your volume, your `Run` key or your work area. Registering
a global hotkey is not in the suite — it needs a real desktop and a real input queue — so
`tools/press-hotkey.ps1` is how that is checked.

Windows Code Integrity (Smart App Control) can refuse a freshly built, unsigned test
assembly. That is a machine policy, not a code fault: the same build is verified on
`windows-latest` in [`.github/workflows/build-and-test.yml`](.github/workflows/build-and-test.yml).

## Run

```powershell
dotnet run --project src/NoctaliaWin.App
```

A bar appears on every monitor inside the work area (below the Windows taskbar), with a tray
icon whose menu has **Settings** and **Exit Noctalia** and whose left click opens the launcher.
`Ctrl+Alt+Space` opens the same launcher from anywhere, whatever has focus.
`tools/capture-screen.ps1`, `tools/crop-screen.ps1`, `tools/click-screen.ps1` and
`tools/press-hotkey.ps1` exist for checking the result by eye:

```powershell
powershell -File tools/crop-screen.ps1 -X 1560 -Y 28 -Width 360 -Height 50 -Scale 3
powershell -File tools/press-hotkey.ps1 -Ctrl -Alt -Vk 0x20 -Out hotkey.png
```

## Configuration

Config lives at:

```
%LOCALAPPDATA%\NoctaliaWin\config.toml
```

It is a schema-compatible subset of Noctalia's `config.toml`: the same key names, defaults
and value ranges, so an existing Noctalia bar section can be pasted in and will either load
or report a diagnostic telling you what was ignored. The file is versioned
(`schema_version`) and old files migrate forward; unknown or newer sections are reported,
not fatal. Writes are atomic (temp file + replace), and a malformed file falls back to
defaults while preserving your file for inspection.

The panel keys are the upstream ones: `[shell.panel]` (`transparency_mode`, `borders`,
`border_width`, `shadow`, `control_center_placement`, `control_center_position`,
`open_near_click_control_center`, `launcher_placement`, `launcher_position`,
`open_near_click_launcher`, `floating_offset`), `[shell.launcher]` (`width`,
`search_position`, `hotkey`) and `[control_center] width`. A panel opens attached to the bar's
corners by default; `launcher_placement = "floating"` (the upstream default) gives a plain
rounded card instead, and `launcher_position` chooses between centring it and hanging it off
the edge the bar docked on.

`hotkey` is not an upstream key: Noctalia's shortcuts are bound by the Wayland compositor, and
this port has none, so it asks Windows for the keystroke instead. It takes modifiers plus one
key (`CTRL+ALT+SPACE`, `SUPER+SHIFT+D`, `WIN+F12`), `SUPER`/`WIN`/`MOD4` and
`CTRL`/`CONTROL`/`ALT`/`MOD1`/`SHIFT` are accepted spellings, and an empty value turns the
binding off. A bare key with no modifier is refused — it would be stolen from every
application on the desktop.

Upstream's other launcher keys — `layout`, `default_view`, `show_icons`, `track_usage`,
`sort_by_usage`, `pinned` and the result providers — are **not read**, because this build has
nothing to render them with: there are no icons, no recent/pinned/categories views and no
usage history yet. A key nothing draws is left out rather than accepted and ignored.

The settings window writes this file, and the bar rebuilds as soon as a write lands. Three
doors open it: the **Settings** entry in the tray menu, the **Settings** button in the bar's
default Start lane (next to **Search**, and movable with the lane editor), and
`NoctaliaWin.exe --settings [section]`, where `section` is one of the sidebar's ids
(`appearance`, `bar`, `panels`, `control-center`, `launcher`, `clock`, `windows`) for a
shortcut that lands on one page.

A hand edit to `config.toml` is picked up too: the app watches the file, and a change in its
text — not merely a touch — reloads the store and rebuilds the bar. The app's own writes are
filtered the same way, which is what keeps one save (an atomic replace, which arrives as
several file-system events) from rebuilding the bar half a dozen times. A malformed edit
falls back to defaults with a diagnostic rather than taking the bar down.

## Layout

| Path | Contents |
|---|---|
| `src/NoctaliaWin.Core` | Platform-independent: design tokens, palettes, concave corner + panel geometry, config schema/codec, settings store, fzy search, launcher ranking, hotkey gesture parsing, bar layout/auto-hide/view model, service interfaces |
| `src/NoctaliaWin.Windows` | Windows implementations of those interfaces (P/Invoke, COM). Kept thin so nothing else depends on Win32 |
| `src/NoctaliaWin.App` | WPF app: bar window, pop-out panel windows (calendar, control center, launcher), theme brushes, composition root; the settings window lands here |
| `tests/NoctaliaWin.Tests` | xUnit suite for the Core, Windows and shell layers |
| `tools` | Screen-capture and key-press helpers used to check shell windows by eye |
| `docs/IMPLEMENTATION-ASSESSMENT.md` | Upstream inspection, reuse plan, Linux→Windows replacement table, risks |
| `THIRD-PARTY-NOTICES.md` | Licenses and exactly which upstream pieces were ported |

## Design notes

- **Concave corners are real geometry, not a bitmap.** Noctalia draws corners with a signed
  distance shader; `ShapeOutline` reproduces the same silhouette as vector arcs and lines so
  WPF renders them with antialiasing at any DPI.
- **A panel reveals by moving its content, not its window.** The panel window is placed once
  at its final surface and the painted body slides inside it, which is upstream's
  `applyAttachedReveal`: the concave cutouts stay registered against the bar on every frame,
  and the panel appears to grow out from behind it. The bars are re-raised above the panel
  after it opens, because a panel that paints over its own bar shows no seam at all.
- **One panel at a time, and the bar is never the way out.** Opening a second widget replaces
  the first with no close animation (upstream's behaviour), and the click that opened a panel
  cannot dismiss it: dismissal is Escape, a click outside the body, or the window losing
  activation to another application.
- **The bar is laid out in pixels, then divided once.** `BarLayout` turns a monitor and a
  config section into physical rectangles (device scale × bar scale × UI scale); the WPF
  window divides by that monitor's device scale to get DIPs, and positions itself with
  `SetWindowPos`. Mixed-DPI monitors therefore cannot round a bar out of place.
- **The launcher is a panel, not a second window system.** Upstream's launcher and its
  quick-settings surface share the same shell surface, and so do these: the same window class,
  reveal, focus rules, one-open replacement and dismissal. The ranking, paging, wrapping and
  Enter behaviour live in `LauncherModel` in Core, which is why they have tests; the WPF side
  only draws that model and forwards keys.
- **A hotkey outlives the windows.** `RegisterHotKey` binds to a window and `WM_HOTKEY` is
  delivered to that window's thread, so the shell cannot hang the binding off a bar or a panel:
  both are destroyed on every config and display change. `HotkeyService` owns one invisible
  message-only window for the life of the process, and re-registers the configured gesture
  after each rebuild. Parsing and the modifier rules live in Core (`HotkeyGesture`), which is
  why they have tests; the virtual-key table lives next to the P/Invoke it feeds.
- **Widgets are text labels, deliberately.** Noctalia's bar uses an icon font that is not
  part of this repository's licence grant, so a widget says "Wi-Fi" where a copied glyph
  would be an unlicensed asset.
- **UI never calls Win32 directly.** Bar windows ask `IWindowPlacementService` for monitor
  and DPI info; widgets ask `ISystemStatusService`, `IAudioService`, etc. Each has a
  documented API, and anything without a clean Windows equivalent is labelled rather than faked.
- **Nothing destructive to the shell.** No process patching, no undocumented hooks. Hiding the
  Windows taskbar is an explicit, reversible opt-out, and the desktop stays usable if this app
  exits or crashes. A bar that cannot be placed stays where it was instead of closing.

## Troubleshooting and recovery

- **Bad config:** delete `%LOCALAPPDATA%\NoctaliaWin\config.toml` (or use Restore defaults in
  settings, once it exists) to return to defaults. Diagnostics for what was ignored are
  reported at load.
- **Bar covers the wrong area, or is missing:** it is always inside the monitor work area, so
  a missing bar usually means the monitor list changed. Re-plug the display or restart the app;
  `WM_DISPLAYCHANGE` rebuilds the bars on its own.
- **Auto-hide bar will not come back:** the reveal band is a 2px strip on the bar's docked edge
  *of the work area* — with a top bar and the Windows taskbar showing, that is a couple of
  pixels below the taskbar, not at screen y=0.
- **`Ctrl+Alt+Space` does nothing:** the tray tooltip reads "launcher hotkey unavailable" when
  Windows refused the binding, which in practice means another application already owns that
  combination. Point `shell.launcher.hotkey` at something else and restart. The exact reason
  is not surfaced anywhere yet — that is a settings-window job.
- **Test run aborts with `0x800711C7` or Smart App Control:** Code Integrity is refusing an
  unsigned freshly built assembly. Use CI, or build a Release copy you have signed; do not
  turn the security feature off.
- **Build fails with an SDK error:** confirm `dotnet --version` reports 10.x and that the
  Windows Desktop workload is present (`dotnet workload list`).
- **Stopping the app:** tray icon → Exit Noctalia. There is no other quit path while the bar
  has no panel to open.

## License

Source in this repository is MIT-licensed where it derives from Noctalia (see
`licenses/NOCTALIA-MIT.txt`); ported files carry a header naming the upstream file they came
from. Read `THIRD-PARTY-NOTICES.md` before reusing any part of this project.
