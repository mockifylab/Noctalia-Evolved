# Third-party notices

Noctalia Win is a clean-room Windows port that reuses logic, design tokens and asset
contracts from upstream open-source projects. Each reuse is annotated in the source
file header, and the full license texts are kept in `licenses/`.

## Noctalia (MIT)

- Upstream: https://github.com/noctalia-dev/noctalia
- License: MIT, Copyright (c) 2026 noctalia-dev — full text in `licenses/NOCTALIA-MIT.txt`
- What is reused, and where:
  - Concave corner model and bar shape rules — ported from
    `src/render/core/render_styles.h` and `src/shell/bar/bar_corner_shape.h`
    into `src/NoctaliaWin.Core/Geometry/CornerModel.cs` and `BarShape.cs`.
  - Concave silhouette math — derived from the rect signed-distance boundary rules in
    `src/render/programs/rect_program.cpp`, reimplemented as vector geometry in
    `src/NoctaliaWin.Core/Geometry/ShapeOutline.cs`.
  - Attached-panel placement — ported from `src/shell/panel/attached_panel_context.h`
    and `src/shell/panel/panel_manager.cpp` into
    `src/NoctaliaWin.Core/Geometry/AttachedPanelPlacement.cs`.
  - Design tokens — ported from `src/ui/style.h` into `src/NoctaliaWin.Core/Design/StyleTokens.cs`.
  - Colour roles and builtin palettes — ported from `src/ui/palette.h` and
    `src/theme/builtin_palettes.cpp` into `src/NoctaliaWin.Core/Design/Palette.cs`.
  - Configuration keys, defaults and validation ranges — follow `example.toml` and
    `src/config/config_types.h` in
    `src/NoctaliaWin.Core/Config/AppConfig.cs` and `ConfigToml.cs`.
  - Bar widget identifiers — kept identical to upstream widget ids so configs stay
    schema-compatible; see `src/NoctaliaWin.Core/Shell/WidgetCatalog.cs`.
- No Wayland, OpenGL, C++ or Linux-specific code was copied. UI, rendering and system
  integration are original C#/WPF implementations against these ported contracts.

## fzy (MIT)

- Source: the copy vendored inside Noctalia at `third_party/fzy`
- License: MIT, Copyright (c) 2014 John Hawthorn — full text in `licenses/FZY-MIT.txt`
- What is reused: the fuzzy matching algorithm (DP scoring, bonus table, gap constants,
  backtrace for match positions) ported from `third_party/fzy/src/{match.c,bonus.h,config.h}`
  into `src/NoctaliaWin.Core/Fuzzy/FzyScorer.cs`. The port is a line-by-line translation of
  the C algorithm, not a call into the C library.

## Tabler Icons (MIT)

- Upstream: https://tabler.io/icons
- License: MIT
- Status: **not yet bundled.** Noctalia's icon glyphs come from a Tabler-derived font
  (`assets/fonts/noctalia-tabler.ttf`) plus its glyph name table. The bar and panel UI
  that consumes icons is not implemented yet; when it lands, the font and its license
  text will be added here and under `licenses/`.

## Material Color Utilities (Apache-2.0)

- Upstream: https://github.com/material-foundation/material-color-utilities
- License: Apache-2.0
- Status: **planned, not used.** Only needed if wallpaper-derived theming (Noctalia's
  `source = "wallpaper"`) is ported. Not linked or vendored in the current tree.

## Seelen UI (AGPL-3.0) — consulted, not copied

- Upstream: https://github.com/eythaann/Seelen-UI (AGPL-3.0)
- No code, assets or distinctive design elements from Seelen UI are used in this
  repository. It was inspected only to confirm that Windows shell integration in the
  ecosystem is feasible, and its AGPL-3.0 license is the reason it was excluded from
  reuse. See `docs/IMPLEMENTATION-ASSESSMENT.md`.

## Windows platform APIs

All Win32 / COM interop used by this project targets documented, publicly described
Windows APIs. No undocumented hooks, process patching or shell replacement is used.
