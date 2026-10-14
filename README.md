# Noctalia Win

A native Windows 11 desktop app porting the visual design and interaction model of
[Noctalia Shell](https://github.com/noctalia-dev/noctalia) — the concave-corners top bar
and its pop-out panels — with Windows-native system integration behind clean interfaces.

This is **not** a Linux desktop environment on Windows, and **not** a re-skin of the Windows
shell. It reuses Noctalia's geometry, design tokens and config contract where the license
permits, and replaces Wayland/Linux mechanisms with documented Windows APIs.

> **Status: Milestone 1 in progress.** The platform-independent core (concave geometry,
> theming, config, fuzzy search) is implemented and covered by 126 passing tests.
> The Windows service layer and the WPF bar/panel/launcher windows are not built yet,
> so there is no runnable UI in this tree yet. See [Current state](#current-state).

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

The suite covers the platform-independent logic only (geometry, config, search) — those
tests run anywhere and never touch the live system.

## Run

Not runnable yet — `src/NoctaliaWin.App` is still the scaffolded WPF shell. The first
runnable milestone build will ship the bar window.

## Configuration

Once the settings UI exists, config lives at:

```
%LOCALAPPDATA%\NoctaliaWin\config.toml
```

It is a schema-compatible subset of Noctalia's `config.toml`: the same key names, defaults
and value ranges, so an existing Noctalia bar section can be pasted in and will either load
or report a diagnostic telling you what was ignored. The file is versioned
(`schema_version`) and old files migrate forward; unknown or newer sections are reported,
not fatal. Writes are atomic (temp file + replace), and a malformed file falls back to
defaults while preserving your file for inspection.

## Layout

| Path | Contents |
|---|---|
| `src/NoctaliaWin.Core` | Platform-independent: design tokens, palettes, concave corner + panel geometry, config schema/codec, settings store, fzy search, launcher ranking, service interfaces |
| `src/NoctaliaWin.Windows` | Windows implementations of those interfaces (P/Invoke, COM). Kept thin so nothing else depends on Win32 |
| `src/NoctaliaWin.App` | WPF app: bar, concave panels, launcher, settings, tray |
| `tests/NoctaliaWin.Tests` | xUnit suite for the Core layer |
| `docs/IMPLEMENTATION-ASSESSMENT.md` | Upstream inspection, reuse plan, Linux→Windows replacement table, risks |
| `THIRD-PARTY-NOTICES.md` | Licenses and exactly which upstream pieces were ported |

## Design notes

- **Concave corners are real geometry, not a bitmap.** Noctalia draws corners with a signed
  distance shader; `ShapeOutline` reproduces the same silhouette as vector arcs and lines so
  WPF renders them with antialiasing at any DPI.
- **UI never calls Win32 directly.** Bar/panel windows ask `IWindowPlacementService` for
  monitor and DPI info; widgets ask `ISystemStatusService`, `IAudioService`, etc. Each has a
  documented API, and anything without a clean Windows equivalent is labelled rather than faked.
- **Nothing destructive to the shell.** No process patching, no undocumented hooks. Hiding the
  Windows taskbar is an explicit, reversible opt-out, and the desktop stays usable if this app
  exits or crashes.

## Troubleshooting and recovery

- **Bad config:** delete `%LOCALAPPDATA%\NoctaliaWin\config.toml` (or use Restore defaults in
  settings) to return to defaults. Diagnostics for what was ignored are reported at load.
- **Build fails with an SDK error:** confirm `dotnet --version` reports 10.x and that the
  Windows Desktop workload is present (`dotnet workload list`).
- **App not behaving as expected:** this tree is pre-UI; if you found a runnable build,
  report the bar/panel and the config values you used.

## License

Source in this repository is MIT-licensed where it derives from Noctalia (see
`licenses/NOCTALIA-MIT.txt`); ported files carry a header naming the upstream file they came
from. Read `THIRD-PARTY-NOTICES.md` before reusing any part of this project.
