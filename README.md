# Noctalia Win

A native Windows 11 desktop app porting the visual design and interaction model of
[Noctalia Shell](https://github.com/noctalia-dev/noctalia) — the concave-corners top bar
and its pop-out panels — with Windows-native system integration behind clean interfaces.

This is **not** a Linux desktop environment on Windows, and **not** a re-skin of the Windows
shell. It reuses Noctalia's geometry, design tokens and config contract where the license
permits, and replaces Wayland/Linux mechanisms with documented Windows APIs.

> **Status: the bar runs; panels and launcher do not yet.** `dotnet run --project
> src/NoctaliaWin.App` shows a Noctalia bar on every monitor — concave silhouette, clock,
> network, volume, battery, auto-hide, tray presence — backed by 275 passing tests. The
> pop-out panels, the launcher window and the settings window are the next milestones.
> See [Current state](#current-state).

## Current state

| Area | State |
|---|---|
| Concave bar geometry, design tokens, palettes | Done, ported from Noctalia and unit-tested |
| Config (`config.toml`) load/save/validate/migrate | Done, unit-tested |
| fzy fuzzy search + launcher ranking | Done, unit-tested (no window yet) |
| Monitors, DPI, power, network, audio, startup, tray | Done against documented Win32/COM APIs |
| Bar window (position, scale, auto-hide, widgets) | **Runs**; clock/network/volume/battery/spacer show live data |
| Concave pop-out panel | Not built |
| Launcher window | Not built (service layer is ready) |
| Settings window | Not built (store is ready; edit `config.toml` by hand) |

Widgets that have no Windows backing yet are **skipped and reported**, never drawn as empty
tiles: `media`, `clipboard`, `bluetooth`, `brightness`, `wallpaper`, `workspaces`, `text`,
`tray` and `notifications`. The last two are on that list because reading another process's
status-bar icons or toast content has no supported API. `launcher`, `settings`,
`control-center` and `session` are named as supported in the catalog but are not drawn until
their window exists.

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

## Test

```powershell
dotnet test tests/NoctaliaWin.Tests
```

Most of the suite is platform-independent (geometry, config, search, bar layout) and runs
anywhere. The `NoctaliaWin.Tests/Windows` tests need Windows because they exercise the real
service layer: some inject a fake reader for the decision logic, and a few read the live
monitor list, power status, `App Paths` key, default audio endpoint and tray. The live tests
only ever read; none of them change your volume, your `Run` key or your work area.

Windows Code Integrity (Smart App Control) can refuse a freshly built, unsigned test
assembly. That is a machine policy, not a code fault: the same build is verified on
`windows-latest` in [`.github/workflows/build-and-test.yml`](.github/workflows/build-and-test.yml).

## Run

```powershell
dotnet run --project src/NoctaliaWin.App
```

A bar appears on every monitor inside the work area (below the Windows taskbar), with a tray
icon whose menu has **Exit Noctalia**. `tools/capture-screen.ps1` and `tools/crop-screen.ps1`
exist for checking the result by eye:

```powershell
powershell -File tools/crop-screen.ps1 -X 1560 -Y 28 -Width 360 -Height 50 -Scale 3
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

There is no settings window yet, so edit the file by hand and restart the app (or save while
it runs: the bar rebuilds on the next config change it observes).

## Layout

| Path | Contents |
|---|---|
| `src/NoctaliaWin.Core` | Platform-independent: design tokens, palettes, concave corner + panel geometry, config schema/codec, settings store, fzy search, launcher ranking, bar layout/auto-hide/view model, service interfaces |
| `src/NoctaliaWin.Windows` | Windows implementations of those interfaces (P/Invoke, COM). Kept thin so nothing else depends on Win32 |
| `src/NoctaliaWin.App` | WPF app: bar window, theme brushes, composition root; panels, launcher and settings land here |
| `tests/NoctaliaWin.Tests` | xUnit suite for the Core, Windows and shell layers |
| `tools` | Screen-capture helpers used to check shell windows by eye |
| `docs/IMPLEMENTATION-ASSESSMENT.md` | Upstream inspection, reuse plan, Linux→Windows replacement table, risks |
| `THIRD-PARTY-NOTICES.md` | Licenses and exactly which upstream pieces were ported |

## Design notes

- **Concave corners are real geometry, not a bitmap.** Noctalia draws corners with a signed
  distance shader; `ShapeOutline` reproduces the same silhouette as vector arcs and lines so
  WPF renders them with antialiasing at any DPI.
- **The bar is laid out in pixels, then divided once.** `BarLayout` turns a monitor and a
  config section into physical rectangles (device scale × bar scale × UI scale); the WPF
  window divides by that monitor's device scale to get DIPs, and positions itself with
  `SetWindowPos`. Mixed-DPI monitors therefore cannot round a bar out of place.
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
