# Release checklist

What a 1.0.0 publish needs, and where each line stands. Anything not marked done is not done —
this file exists so nobody has to guess from the release notes.

## Code and correctness

- [x] Single version source: `Directory.Build.props` → 1.0.0, read by the settings window's
      `Version` row, by the file metadata the apphost carries, and by the update check.
- [x] `dotnet build NoctaliaWin.slnx -c Release` succeeds.
- [x] `dotnet test tests/NoctaliaWin.Tests` passes: 570 tests.
- [x] CI on `windows-latest` builds Release and runs the suite for every push to `main`.
- [x] No secrets, no personal paths, no machine-specific settings and no build artifacts are
      committed; `build/`, `bin/` and `obj/` are git-ignored.
- [x] Upstream Noctalia attribution and MIT notices preserved in code headers,
      `THIRD-PARTY-NOTICES.md` and `licenses/`.
- [x] Seelen UI was consulted for integration patterns only; no code, asset or design was taken
      from it (AGPL-3.0).

## Installer

- [x] `tools/make-installer.ps1` produces `build\NoctaliaEvolved-1.0.0-Setup-win-x64.zip` plus
      `build\SHA256SUMS.txt` (SHA-256 for the bundle and every payload file).
- [x] The setup window is offline: it reads the `payload` folder beside it and never reaches the
      network.
- [x] It states the version in the bundle, the version on disk, which replaces which, and the
      folders it writes before a press; no silent or automatic install.
- [x] It checks for the .NET 10 Desktop Runtime before copying; refuses while a running install
      holds the target instead of killing it.
- [x] Writes stay inside this user's folders: `%LOCALAPPDATA%\Programs\Noctalia Evolved`, the
      per-user Start Menu, and one `Run` value only when the box is ticked.
- [x] No elevation, no shell or taskbar patching, no protected process touched.
- [ ] Code signing / publisher certificate — **not done**. Nothing here is signed, so SmartScreen
      answers the first run and Smart App Control may refuse a locally built binary outright.
- [ ] A machine-wide install with an MSI — not done; the per-user bundle is what exists.

## Update check

- [x] One HTTPS GET of `https://api.github.com/repos/<owner>/<name>/releases/latest`, with only
      the User-Agent GitHub requires. No settings, paths or identifiers are sent.
- [x] The repository defaults to this project's own (`mockifylab/Noctalia-Evolved`) and is
      overridable with `NOCTALIA_REPOSITORY`; an unreadable value is reported, never fallen back
      from silently. No invented URL is anywhere in the code.
- [x] Version precedence, the response body and every failure path are unit-tested in Core.
- [x] Nothing is downloaded and nothing is installed; the second row opens the release page so
      the person decides.
- [ ] Download-and-verify-then-install flow — not done, and deliberately: it needs a signed
      artifact and an Authenticode check before it is worth offering.

## Presentation

- [x] Name: **Noctalia Evolved**. Description: **The Noctalia experience on Windows.**
- [x] Visual identity: the deep-charcoal / dark-green / muted-mint palette ported from Noctalia's
      own scheme in `BuiltinPalettes`, with its tokens unit-tested; no Apple logos, branding,
      proprietary graphics, text or product assets are used.
- [x] `docs/RELEASE-NOTES-1.0.0.md`, README install/update/troubleshooting sections.
- [ ] A GitHub release with the zip attached — **not done**: publishing a release or any artifact
      needs explicit authorization from the owner.
- [ ] Screenshots on the release page — not taken for this document; `tools/capture-screen.ps1`
      is how they are produced.

## Before pressing publish

1. `dotnet build NoctaliaWin.slnx -c Release && dotnet test tests/NoctaliaWin.Tests`
2. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-installer.ps1`
3. Unpack the zip on a clean Windows 11 account, run `NoctaliaEvolved-Setup.exe`, and check the
   bar, both panels, the launcher, the settings window and the tray.
4. Verify the checksums from `SHA256SUMS.txt` match the files that get uploaded.
5. Publish only with the owner's go-ahead.
