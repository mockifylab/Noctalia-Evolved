// The settings pages this build can honour. Upstream's registry lists 24 sections
// (src/settings/settings_registry.cpp — MIT License, Copyright (c) 2026 noctalia-dev); the
// ones whose keys have no Windows surface yet — wallpaper, dock, niri, hooks, plugins and the
// rest — are left out rather than shown as rows that would do nothing. Every row that appears
// here changes something on the machine.
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;

namespace NoctaliaWin.Core.Settings;

public static class SettingsCatalog
{
    /// The action ids a Button row can carry; the window decides what each one does.
    public const string OpenConfigFolder = "open-config-folder";
    public const string RestoreDefaults = "restore-defaults";

    public static IReadOnlyList<SettingsPage> Pages { get; } =
    [
        Appearance(),
        Bar(),
        Panels(),
        ControlCenter(),
        Launcher(),
        Clock(),
        Windows(),
    ];

    public static SettingsPage? PageById(string id) => Pages.FirstOrDefault(p => p.Id == id);

    /// Every row of every page, in sidebar order. Used by search and by a page reset.
    public static IEnumerable<(SettingsPage Page, SettingsGroup Group, SettingsField Field)> All()
    {
        foreach (var page in Pages)
            foreach (var group in page.Groups)
                foreach (var field in group.Fields)
                    yield return (page, group, field);
    }

    private static SettingsPage Appearance() => new(
        "appearance", "Appearance", "palette",
        [
            new SettingsGroup("theme", "Theme",
            [
                SettingsField.Choice(
                    "theme.mode", "Colour mode",
                    "Dark and light pick the palette's mode. Auto follows the Windows app-mode "
                        + "setting, so Noctalia changes with the system.",
                    SettingsControlKind.Pills,
                    s => s.Config.Theme.Mode,
                    (s, v) => s.Config.Theme.Mode = v,
                    [new("dark", "Dark"), new("light", "Light"), new("auto", "Auto")],
                    tags: ["color", "scheme", "dark", "light", "mode"]),

                SettingsField.Scheme(
                    "theme.builtin", "Colour scheme",
                    $"{BuiltinPalettes.Names.Count} schemes ported from Noctalia, each with its "
                        + "own dark and light palette.",
                    s => s.Config.Theme.Builtin,
                    (s, v) => s.Config.Theme.Builtin = v,
                    tags: ["palette", "colors", "theme", "scheme"]),

                SettingsField.Slider(
                    "accessibility.ui_scale", "Interface scale",
                    "Scales the bar, the panels and this window together.",
                    s => s.Config.Accessibility.UiScale,
                    (s, v) => s.Config.Accessibility.UiScale = v,
                    0.5, 3.0, 0.05, 2, "\u00d7",
                    tags: ["dpi", "zoom", "size", "text"]),
            ]),

            new SettingsGroup("transparency", "Transparency",
            [
                SettingsField.Slider(
                    "bar.main.background_opacity", "Bar background opacity",
                    "The panel backgrounds are derived from this, the way Noctalia derives them.",
                    s => s.Config.Main.BackgroundOpacity,
                    (s, v) => s.Config.Main.BackgroundOpacity = v,
                    0.0, 1.0, 0.01, 2, "",
                    tags: ["alpha", "opaque", "clear", "blur"]),

                SettingsField.Choice(
                    "shell.panel.transparency_mode", "Panel transparency",
                    "Solid paints the surface opaque, soft and glass let the desktop through. "
                        + "Windows has no documented backdrop blur behind a layered window, so "
                        + "glass is translucent here but not blurred.",
                    SettingsControlKind.Pills,
                    s => s.Config.Panels.TransparencyMode,
                    (s, v) => s.Config.Panels.TransparencyMode = v,
                    [new("solid", "Solid"), new("soft", "Soft"), new("glass", "Glass")],
                    tags: ["alpha", "blur", "clear"]),
            ]),
        ]);

    private static SettingsPage Bar() => new(
        "bar", "Bar", "bar",
        [
            new SettingsGroup("layout", "Layout",
            [
                SettingsField.Choice(
                    "bar.main.position", "Screen edge",
                    "Which edge of the display the bar sits on.",
                    SettingsControlKind.Pills,
                    s => s.Config.Main.Position,
                    (s, v) => s.Config.Main.Position = v,
                    [new("top", "Top"), new("bottom", "Bottom"), new("left", "Left"), new("right", "Right")],
                    tags: ["edge", "location"]),

                SettingsField.IntSlider(
                    "bar.main.thickness", "Bar thickness",
                    "The bar's height on a top or bottom edge, its width on a side edge.",
                    s => s.Config.Main.Thickness,
                    (s, v) => s.Config.Main.Thickness = v,
                    20, 128, 1, "px",
                    tags: ["height", "size"]),

                SettingsField.IntSlider(
                    "bar.main.margin_ends", "End margin",
                    "How far the bar pulls in from the ends of the screen.",
                    s => s.Config.Main.MarginEnds,
                    (s, v) => s.Config.Main.MarginEnds = v,
                    0, 1000, 2, "px",
                    tags: ["width", "gap"]),

                SettingsField.IntSlider(
                    "bar.main.margin_edge", "Edge margin",
                    "The gap between the bar and the screen edge it sits on.",
                    s => s.Config.Main.MarginEdge,
                    (s, v) => s.Config.Main.MarginEdge = v,
                    0, 100, 1, "px",
                    tags: ["gap", "offset"]),

                SettingsField.IntSlider(
                    "bar.main.padding", "Inner padding",
                    "Space inside the bar, between its edge and the first widget.",
                    s => s.Config.Main.Padding,
                    (s, v) => s.Config.Main.Padding = v,
                    0, 64, 1, "px",
                    tags: ["spacing"]),

                SettingsField.IntSlider(
                    "bar.main.widget_spacing", "Widget spacing",
                    "The gap between widgets in a lane.",
                    s => s.Config.Main.WidgetSpacing,
                    (s, v) => s.Config.Main.WidgetSpacing = v,
                    0, 64, 1, "px",
                    tags: ["gap", "spacing"]),

                SettingsField.IntSlider(
                    "bar.main.panel_overlap", "Panel overlap",
                    "How far a panel reaches under the bar to hide the seam where it grows out.",
                    s => s.Config.Main.PanelOverlap,
                    (s, v) => s.Config.Main.PanelOverlap = v,
                    -16, 16, 1, "px",
                    advanced: true,
                    tags: ["seam", "gap"]),
            ]),

            new SettingsGroup("size", "Size",
            [
                SettingsField.Slider(
                    "bar.main.scale", "Bar scale",
                    "Scales the bar and everything attached to it, including its panels.",
                    s => s.Config.Main.Scale,
                    (s, v) => s.Config.Main.Scale = v,
                    0.25, 4.0, 0.05, 2, "\u00d7",
                    tags: ["zoom", "size"]),

                SettingsField.Slider(
                    "bar.main.font_scale", "Bar text scale",
                    "Multiplies the widget text on top of the bar's own scale.",
                    s => s.Config.Main.FontScale,
                    (s, v) => s.Config.Main.FontScale = v,
                    0.5, 3.0, 0.05, 2, "\u00d7",
                    tags: ["text", "size", "zoom"]),
            ]),

            new SettingsGroup("corners", "Corners",
            [
                SettingsField.IntSlider(
                    "bar.main.radius", "Corner radius",
                    "The rounding of the bar's corners, and the size of the concave cutout a "
                        + "panel nests into.",
                    s => s.Config.Main.Radius,
                    (s, v) => s.Config.Main.Radius = v,
                    0, 40, 1, "px",
                    tags: ["round", "radius"]),

                SettingsField.Toggle(
                    "bar.main.concave_edge_corners", "Concave edge corners",
                    "Carves the bar's outer corners inward instead of rounding them. Only "
                        + "visible when the edge margin is 0, because the carve happens at the "
                        + "screen edge.",
                    s => s.Config.Main.ConcaveEdgeCorners,
                    (s, v) => s.Config.Main.ConcaveEdgeCorners = v,
                    tags: ["shape", "corner", "notch"]),

                Corner("bar.main.radius_top_left", "Top left radius", RadiusSide.TopLeft),
                Corner("bar.main.radius_top_right", "Top right radius", RadiusSide.TopRight),
                Corner("bar.main.radius_bottom_left", "Bottom left radius", RadiusSide.BottomLeft),
                Corner("bar.main.radius_bottom_right", "Bottom right radius", RadiusSide.BottomRight),
            ]),

            new SettingsGroup("behaviour", "Behaviour",
            [
                SettingsField.Toggle(
                    "bar.main.auto_hide", "Auto-hide the bar",
                    "The bar slides off its edge and returns when the pointer reaches the screen "
                        + "border.",
                    s => s.Config.Main.AutoHide,
                    (s, v) => s.Config.Main.AutoHide = v,
                    tags: ["hide", "reveal", "slide"]),
            ]),

            new SettingsGroup("widgets", "Widgets",
            [
                SettingsField.Lane(
                    "bar.main.start", "Left lane",
                    "Widgets at the start of the bar, in the order they appear.",
                    s => [.. s.Config.Main.Start],
                    (s, v) => s.Config.Main.Start = v,
                    tags: ["modules", "plugins", "add", "remove"]),

                SettingsField.Lane(
                    "bar.main.center", "Centre lane",
                    "Widgets in the middle of the bar.",
                    s => [.. s.Config.Main.Center],
                    (s, v) => s.Config.Main.Center = v,
                    tags: ["modules", "plugins", "add", "remove"]),

                SettingsField.Lane(
                    "bar.main.end", "Right lane",
                    "Widgets at the end of the bar.",
                    s => [.. s.Config.Main.End],
                    (s, v) => s.Config.Main.End = v,
                    tags: ["modules", "plugins", "add", "remove"]),
            ]),
        ]);

    private static SettingsPage Panels() => new(
        "panels", "Panels", "panel",
        [
            new SettingsGroup("look", "Look",
            [
                SettingsField.Toggle(
                    "shell.panel.borders", "Panel borders",
                    "The hairline around a pop-out panel.",
                    s => s.Config.Panels.Borders,
                    (s, v) => s.Config.Panels.Borders = v,
                    tags: ["outline", "edge"]),

                SettingsField.Slider(
                    "shell.panel.border_width", "Border width",
                    "Any value above 0 draws the border; this build renders it at one weight, so "
                        + "above 0 the slider acts as on and off.",
                    s => s.Config.Panels.BorderWidth,
                    (s, v) => s.Config.Panels.BorderWidth = v,
                    0.0, 8.0, 0.5, 1, "px",
                    tags: ["outline", "thickness"]),

                SettingsField.IntSlider(
                    "shell.panel.floating_offset", "Floating gap",
                    "How far a floating panel sits away from the bar.",
                    s => s.Config.Panels.FloatingOffset,
                    (s, v) => s.Config.Panels.FloatingOffset = v,
                    0, 128, 1, "px",
                    tags: ["gap", "distance"]),
            ]),
        ]);

    private static SettingsPage ControlCenter() => new(
        "control-center", "Control center", "sliders",
        [
            new SettingsGroup("panel", "Panel",
            [
                SettingsField.Choice(
                    "shell.panel.control_center_placement", "Placement",
                    "Attached grows out of the bar and nests its corners into it; floating is a "
                        + "card that sits beside the bar.",
                    SettingsControlKind.Pills,
                    s => s.Config.Panels.ControlCenterPlacement,
                    (s, v) => s.Config.Panels.ControlCenterPlacement = v,
                    [new("attached", "Attached"), new("floating", "Floating")],
                    tags: ["position", "window"]),

                SettingsField.Choice(
                    "shell.panel.control_center_position", "Position",
                    "Auto anchors the panel to the bar, centre puts it in the middle of the "
                        + "screen. Upstream's corner positions are not wired on Windows yet, so "
                        + "they are not offered.",
                    SettingsControlKind.Dropdown,
                    s => s.Config.Panels.ControlCenterPosition,
                    (s, v) => s.Config.Panels.ControlCenterPosition = v,
                    [new("auto", "Auto (against the bar)"), new("center", "Screen centre")],
                    tags: ["location", "anchor"]),

                SettingsField.IntSlider(
                    "control_center.width", "Panel width",
                    "The control center's width, before the compact sidebar factor is applied.",
                    s => s.Config.ControlCenter.Width,
                    (s, v) => s.Config.ControlCenter.Width = v,
                    240, 1400, 10, "px",
                    tags: ["size"]),

                SettingsField.Toggle(
                    "shell.panel.open_near_click_control_center", "Open where the button was clicked",
                    "The panel attaches under the widget that opened it instead of at the bar's "
                        + "usual anchor.",
                    s => s.Config.Panels.OpenNearClickControlCenter,
                    (s, v) => s.Config.Panels.OpenNearClickControlCenter = v,
                    tags: ["anchor", "click"]),
            ]),
        ]);

    private static SettingsPage Launcher() => new(
        "launcher", "Launcher", "search",
        [
            new SettingsGroup("panel", "Panel",
            [
                SettingsField.Choice(
                    "shell.panel.launcher_placement", "Placement",
                    "Attached grows out of the bar; floating is the card upstream ships by "
                        + "default.",
                    SettingsControlKind.Pills,
                    s => s.Config.Panels.LauncherPlacement,
                    (s, v) => s.Config.Panels.LauncherPlacement = v,
                    [new("attached", "Attached"), new("floating", "Floating")],
                    tags: ["position", "window"]),

                SettingsField.Choice(
                    "shell.panel.launcher_position", "Position",
                    "Auto anchors the launcher to the bar, centre puts it in the middle of the "
                        + "screen.",
                    SettingsControlKind.Dropdown,
                    s => s.Config.Panels.LauncherPosition,
                    (s, v) => s.Config.Panels.LauncherPosition = v,
                    [new("auto", "Auto (against the bar)"), new("center", "Screen centre")],
                    tags: ["location", "anchor"]),

                SettingsField.IntSlider(
                    "shell.launcher.width", "Launcher width",
                    "The launcher card's width.",
                    s => s.Config.Launcher.Width,
                    (s, v) => s.Config.Launcher.Width = v,
                    LauncherSection.MinWidth, LauncherSection.MaxWidth, 10, "px",
                    tags: ["size"]),

                SettingsField.Choice(
                    "shell.launcher.search_position", "Search field",
                    "Which end of the card the search row sits at.",
                    SettingsControlKind.Pills,
                    s => s.Config.Launcher.SearchPosition,
                    (s, v) => s.Config.Launcher.SearchPosition = v,
                    [new("top", "Top"), new("bottom", "Bottom")],
                    tags: ["input", "field"]),

                SettingsField.Toggle(
                    "shell.panel.open_near_click_launcher", "Open where the button was clicked",
                    "The launcher attaches under the Search button instead of at the bar's usual "
                        + "anchor.",
                    s => s.Config.Panels.OpenNearClickLauncher,
                    (s, v) => s.Config.Panels.OpenNearClickLauncher = v,
                    tags: ["anchor", "click"]),

                SettingsField.Keybind(
                    "shell.launcher.hotkey", "Show launcher",
                    "A global shortcut that opens the launcher from anywhere. Leave it empty for "
                        + "no shortcut; a combination another app already owns is refused, and "
                        + "the bar's Search button still works.",
                    s => s.Config.Launcher.Hotkey,
                    (s, v) => s.Config.Launcher.Hotkey = v,
                    tags: ["key", "shortcut", "keyboard", "bind"]),
            ]),
        ]);

    private static SettingsPage Clock() => new(
        "clock", "Clock and date", "clock",
        [
            new SettingsGroup("formats", "Formats",
            [
                SettingsField.Text(
                    "shell.time_format", "Time format",
                    "strftime-style tokens, used by the bar clock. %H %M %S for 24-hour time, "
                        + "%I %M %p for 12-hour, %A %a %B %b for day and month names, %d %m %Y "
                        + "for the date. Text inside {:%H:%M} braces is passed through the way "
                        + "Noctalia passes it.",
                    s => s.Config.Shell.TimeFormat,
                    (s, v) => s.Config.Shell.TimeFormat = v,
                    placeholder: "{:%H:%M}",
                    required: true,
                    tags: ["date", "time", "format", "strftime"]),

                SettingsField.Text(
                    "shell.date_format", "Date format",
                    "The headline date inside the calendar panel and the bar clock's tooltip.",
                    s => s.Config.Shell.DateFormat,
                    (s, v) => s.Config.Shell.DateFormat = v,
                    placeholder: "%A, %x",
                    required: true,
                    tags: ["time", "format", "strftime"]),
            ]),
        ]);

    private static SettingsPage Windows() => new(
        "windows", "Windows", "power",
        [
            new SettingsGroup("startup", "Startup",
            [
                SettingsField.Toggle(
                    "system.autostart", "Start with Windows",
                    "Writes this app's own path to the current user's Run key. Turning it off "
                        + "removes only that one value.",
                    s => s.Autostart?.Enabled ?? false,
                    (s, v) => s.Autostart?.SetEnabled(v),
                    tags: ["login", "boot", "run", "startup"]),
            ]),

            new SettingsGroup("config", "Configuration file",
            [
                SettingsField.Info(
                    "system.config_path", "Config file",
                    "The file every row on this page writes to. It is replaced atomically, so a "
                        + "crash cannot leave a half-written config.",
                    s => s.ConfigPath,
                    tags: ["file", "path", "toml", "location"]),

                SettingsField.Info(
                    "system.version", "Version",
                    "The build that is running.",
                    s => s.AppVersion,
                    tags: ["build"]),

                SettingsField.Push(
                    "system.open_config_folder", "Open the settings folder",
                    "Shows the folder named above in Explorer.",
                    OpenConfigFolder,
                    tags: ["file", "folder", "explorer"]),

                SettingsField.Push(
                    "system.restore_defaults", "Restore all defaults",
                    "Rewrites the config file with Noctalia's defaults and rebuilds the bar and "
                        + "panels. It does not touch the startup entry.",
                    RestoreDefaults,
                    tags: ["reset", "factory", "clear"]),
            ]),
        ]);

    private enum RadiusSide
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// A per-corner override. The config stores these as optional, so the row's lowest value
    /// is the sentinel for "no override", which is also what resetting it writes back.
    private static SettingsField Corner(string id, string title, RadiusSide side)
    {
        int? Of(BarSection b) => side switch
        {
            RadiusSide.TopLeft => b.RadiusTopLeft,
            RadiusSide.TopRight => b.RadiusTopRight,
            RadiusSide.BottomLeft => b.RadiusBottomLeft,
            _ => b.RadiusBottomRight,
        };

        void Apply(BarSection b, int value)
        {
            int? optional = value < 0 ? null : value;
            switch (side)
            {
                case RadiusSide.TopLeft:
                    b.RadiusTopLeft = optional;
                    break;
                case RadiusSide.TopRight:
                    b.RadiusTopRight = optional;
                    break;
                case RadiusSide.BottomLeft:
                    b.RadiusBottomLeft = optional;
                    break;
                default:
                    b.RadiusBottomRight = optional;
                    break;
            }
        }

        return SettingsField.IntSlider(
            id, title,
            "Overrides the corner radius for this corner alone. The global radius applies when "
                + "this says global.",
            s => Of(s.Config.Main) ?? -1,
            (s, v) => Apply(s.Config.Main, v),
            -1, 40, 1, "px",
            advanced: true,
            offLabel: "global",
            tags: ["round", "corner"]);
    }
}
