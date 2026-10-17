# Noctalia Evolved 1.0.0

The Noctalia experience on Windows.

This is the first release that installs: a Noctalia top bar with live Windows data, the concave
pop-out panels, a launcher over the machine's own installed applications, and a settings window
in Noctalia's layout — every row of it writing `config.toml` and redrawing the bar.

## What is in it

**The bar.** A capsule on every monitor, in the work area, below the Windows taskbar. Position,
thickness, margins, transparency, corner radii, auto-hide, per-lane widgets and UI scale are all
configurable, on any DPI, on any mix of monitors.

**The panels.** Clicking a widget opens Noctalia's concave panel — the silhouette with the
inverted corners, not a rectangular card. The clock opens a real month grid; the control-centre
button opens a panel that reads and writes the actual master volume, reports the network
connection and the battery, and holds the media controls. Panels reveal with Noctalia's animation,
dismiss on Escape or an outside click, keep to one-open, and place per monitor.

**Widgets with a Windows backing.** `clock`, `network`, `volume`, `battery`, `media` (the
system's own media controls, read through SMTC and controlled from the panel), `launcher`,
`control-center`, `settings`, `spacer`. Widgets without a supported API are named, skipped and
reported — never drawn as empty tiles.

**The launcher.** Start Menu and `App Paths` entries, fuzzy-filtered the way Noctalia scores
them, keyboard-driven, launched through the shell, ranked by usage. A global `Ctrl+Alt+Space`
answers from anywhere; `shell.launcher.hotkey` rebinds it.

**Settings.** Noctalia's layout — search, section list, grouped cards — with the row kinds
upstream uses: switches, pills, dropdowns, sliders with a typed read-out, text fields, a
key-capture box, a scheme grid, widget lane editing, reset-per-row and restore-all-defaults.
Config is versioned TOML, written atomically, and a hand edit in another editor is watched for
and reloaded.

**Update check.** Settings → Windows asks GitHub for the newest release and reports the answer.
One HTTPS GET, no user data, nothing downloaded, nothing installed.

## Installing

See [Install](../README.md#install). `tools/make-installer.ps1` produces
`NoctaliaEvolved-1.0.0-Setup-win-x64.zip` and `SHA256SUMS.txt`. Setup is a six-step wizard — the
bundle, this machine's three checks, the destination (changeable to any folder inside your own
profile), two switches, the copy with a progress bar that counts real files, and a page saying
what was written and how to take it back. It needs no network, no administrator, and writes only
inside this user's folders.

## Known limits, said plainly

- `transparency_mode = "glass"` sets the card's opacity but cannot blur what is behind it; WPF
  exposes no documented backdrop blur for a layered window.
- `shadow`, `reserve_space`, `layer` and `theme.source` are not acted on yet, and the settings
  window does not offer them as if they worked.
- Notifications, brightness, clipboard, wallpaper, workspaces, text and the tray's foreign icons
  have no widgets in this build; `session` is named in the catalog but has no button yet.
- Launcher entries are Start Menu and `App Paths` only; UWP/MSIX packages are not enumerated.
  Each row carries the application's own icon, read from the file Windows names for it, and a
  program with no readable icon is drawn without one.
- Nothing is code-signed, and there is no publisher certificate. Expect SmartScreen on the first
  run; on a machine with Smart App Control enabled a locally built binary may be refused, which
  is why the project is verified in CI on `windows-latest`.
- The media widget follows whichever app holds the system media controls; a player that
  publishes no title or artist shows the status word instead.

## Verification

609 unit tests pass (`dotnet test tests/NoctaliaWin.Tests`), covering the geometry, design
tokens, config load/save/validate/migrate and reload, the bar layout and strftime labels, fuzzy
search, launcher ranking and the icon references a launcher entry carries, the settings catalog
and editor, the panels' calendar and paging, the version and repository parsing behind the
update check, and the installer's payload, version and destination-folder decisions. The Release
build and the full suite run in
[`.github/workflows/build-and-test.yml`](../.github/workflows/build-and-test.yml).

Live checks this release was looked at in person: the bar on a 1920×1080 display at 100%, the
calendar and control-centre panels opening, the launcher filtering and starting an app with each
result carrying its application's own icon, `Ctrl+Alt+Space` from another window, a hand edit of
`config.toml` redrawing the bar, the media widget showing and controlling a session published
through the system's own media controls, and the setup wizard walked through to a finished
install.

## License

MIT, with the upstream Noctalia Shell design credited and its notices preserved. See
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
