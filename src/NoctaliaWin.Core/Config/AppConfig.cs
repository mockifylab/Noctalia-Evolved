// Configuration schema mirroring Noctalia's config keys/defaults
// (src/config/config_types.h, example.toml — MIT License, noctalia-dev).
// Only sections with Windows equivalents are modeled in Milestone 1.
namespace NoctaliaWin.Core.Config;

public sealed class BarSection
{
    public string Position { get; set; } = "top"; // top | bottom | left | right
    public int Thickness { get; set; } = 34;
    public double BackgroundOpacity { get; set; } = 1.0;
    public int Radius { get; set; } = 12;
    public int? RadiusTopLeft { get; set; }
    public int? RadiusTopRight { get; set; }
    public int? RadiusBottomLeft { get; set; }
    public int? RadiusBottomRight { get; set; }
    public bool ConcaveEdgeCorners { get; set; } = true;
    public int MarginEnds { get; set; } = 180;
    public int MarginEdge { get; set; } = 10;
    public int Padding { get; set; } = 14;
    public int WidgetSpacing { get; set; } = 6;
    public double Scale { get; set; } = 1.0;
    public double FontScale { get; set; } = 1.0;
    public bool Shadow { get; set; } = true;
    public bool AutoHide { get; set; }
    public bool ReserveSpace { get; set; } = true;
    public string Layer { get; set; } = "top"; // top | overlay
    public int PanelOverlap { get; set; } = 1;

    public List<string> Start { get; set; } = ["launcher", "settings", "wallpaper", "workspaces"];
    public List<string> Center { get; set; } = ["clock"];
    public List<string> End { get; set; } =
    [
        "media", "tray", "notifications", "clipboard", "network", "bluetooth",
        "volume", "brightness", "battery", "control-center", "session",
    ];

    public (int Tl, int Tr, int Br, int Bl) ResolvedCornerRadii() => (
        RadiusTopLeft ?? Radius,
        RadiusTopRight ?? Radius,
        RadiusBottomRight ?? Radius,
        RadiusBottomLeft ?? Radius);

    public BarSection Clone() => new()
    {
        Position = Position,
        Thickness = Thickness,
        BackgroundOpacity = BackgroundOpacity,
        Radius = Radius,
        RadiusTopLeft = RadiusTopLeft,
        RadiusTopRight = RadiusTopRight,
        RadiusBottomLeft = RadiusBottomLeft,
        RadiusBottomRight = RadiusBottomRight,
        ConcaveEdgeCorners = ConcaveEdgeCorners,
        MarginEnds = MarginEnds,
        MarginEdge = MarginEdge,
        Padding = Padding,
        WidgetSpacing = WidgetSpacing,
        Scale = Scale,
        FontScale = FontScale,
        Shadow = Shadow,
        AutoHide = AutoHide,
        ReserveSpace = ReserveSpace,
        Layer = Layer,
        PanelOverlap = PanelOverlap,
        Start = [.. Start],
        Center = [.. Center],
        End = [.. End],
    };
}

public sealed class ThemeSection
{
    public string Mode { get; set; } = "dark"; // dark | light | auto
    public string Source { get; set; } = "builtin"; // builtin | wallpaper | community
    public string Builtin { get; set; } = "Noctalia";

    public ThemeSection Clone() => new()
    {
        Mode = Mode,
        Source = Source,
        Builtin = Builtin,
    };
}

public sealed class AccessibilitySection
{
    public double UiScale { get; set; } = 1.0;

    public AccessibilitySection Clone() => new() { UiScale = UiScale };
}

/// Attached vs floating, and the opacity mode, for the pop-out panels. Key names mirror
/// Noctalia's `[shell.panel]` (config_types.h PanelConfig — MIT, noctalia-dev); only the
/// panels this build actually opens are modelled.
public sealed class PanelSection
{
    public string TransparencyMode { get; set; } = "solid"; // solid | soft | glass
    public bool Borders { get; set; } = true;
    public double BorderWidth { get; set; } = 1.0;
    public bool Shadow { get; set; } = true;
    public string ControlCenterPlacement { get; set; } = "attached"; // attached | floating
    public string ControlCenterPosition { get; set; } = "auto";
    public string LauncherPlacement { get; set; } = "floating";
    public string LauncherPosition { get; set; } = "center";
    public bool OpenNearClickControlCenter { get; set; }
    public bool OpenNearClickLauncher { get; set; }

    /// Logical px gap between a floating panel and the bar edge.
    public int FloatingOffset { get; set; } = 8;

    public static string ModeName(PanelTransparencyMode mode) => mode switch
    {
        PanelTransparencyMode.Soft => "soft",
        PanelTransparencyMode.Glass => "glass",
        _ => "solid",
    };

    /// Ported from panelCardOpacityForTransparencyMode (config_types.cpp): the alpha the
    /// panel's own cards and sections use for a given mode. `baseOpacity` is the panel's
    /// background opacity, which this port derives from the bar's, since Noctalia's
    /// standalone-panel opacity key has no Windows surface to apply to yet.
    ///
    /// "glass" asks for background blur as well as low opacity. WPF has no documented
    /// backdrop blur behind a layered window, so this build applies the opacity and not the
    /// blur; the difference is visible and is reported as a diagnostic rather than faked.
    public static double CardOpacity(PanelTransparencyMode mode, double baseOpacity)
    {
        double clamped = Math.Clamp(baseOpacity, 0.0, 1.0);
        return mode switch
        {
            PanelTransparencyMode.Soft => Math.Clamp(clamped + 0.08, 0.82, 0.92),
            PanelTransparencyMode.Glass => Math.Clamp(clamped + 0.10, 0.62, 0.75),
            _ => 1.0,
        };
    }

    /// Upstream ships three control-center sidebar modes and scales the configured width
    /// down for the two compact ones. This build renders the compact layout, so the factor
    /// is a constant rather than a config key nothing can set yet.
    public const double CompactSidebarWidthFactor = 0.85;

    public PanelTransparencyMode ParsedTransparencyMode => TransparencyMode switch
    {
        "soft" => PanelTransparencyMode.Soft,
        "glass" => PanelTransparencyMode.Glass,
        _ => PanelTransparencyMode.Solid,
    };

    public bool ControlCenterIsAttached => ControlCenterPlacement != "floating";

    /// Upstream defaults the launcher to a floating card rather than a bar-attached panel.
    public bool LauncherIsAttached => LauncherPlacement == "attached";

    public PanelSection Clone() => new()
    {
        TransparencyMode = TransparencyMode,
        Borders = Borders,
        BorderWidth = BorderWidth,
        Shadow = Shadow,
        ControlCenterPlacement = ControlCenterPlacement,
        ControlCenterPosition = ControlCenterPosition,
        LauncherPlacement = LauncherPlacement,
        LauncherPosition = LauncherPosition,
        OpenNearClickControlCenter = OpenNearClickControlCenter,
        OpenNearClickLauncher = OpenNearClickLauncher,
        FloatingOffset = FloatingOffset,
    };
}

public enum PanelTransparencyMode
{
    Solid,
    Soft,
    Glass,
}

/// Mirrors Noctalia's `[control_center]` sizing keys. The tabs and their shortcuts are
/// Windows-specific content and are not modelled as config yet.
public sealed class ControlCenterSection
{
    public const int DefaultWidth = 700;

    public int Width { get; set; } = DefaultWidth;

    public ControlCenterSection Clone() => new() { Width = Width };
}

/// The `[shell.launcher]` keys this build honours: its width and where the search row sits.
/// Upstream's launcher also has views (recent/pinned/categories), result providers, icons and
/// usage-based sorting, none of which a Windows launcher can render from this repository's
/// data yet, so those keys are left out rather than accepted and ignored.
public sealed class LauncherSection
{
    public const int DefaultWidth = 760;
    public const int MinWidth = 560;
    public const int MaxWidth = 1200;

    /// A Windows-side addition: upstream's global shortcuts belong to the Wayland compositor,
    /// which this port has none of, so the launcher needs a key Windows itself can hand us.
    /// Empty means no global hotkey, leaving the bar button and the tray click as the doors.
    public const string DefaultHotkey = "CTRL+ALT+SPACE";

    public int Width { get; set; } = DefaultWidth;
    public string SearchPosition { get; set; } = "top"; // top | bottom
    public string Hotkey { get; set; } = DefaultHotkey;

    public bool SearchOnTop => SearchPosition != "bottom";

    public LauncherSection Clone() => new()
    {
        Width = Width,
        SearchPosition = SearchPosition,
        Hotkey = Hotkey,
    };
}

public sealed class ShellSection
{
    // Raw strftime-ish formats kept for config parity; the UI layer converts them.
    public string TimeFormat { get; set; } = "{:%H:%M}";
    public string DateFormat { get; set; } = "%A, %x";
    public bool OfflineMode { get; set; }

    public ShellSection Clone() => new()
    {
        TimeFormat = TimeFormat,
        DateFormat = DateFormat,
        OfflineMode = OfflineMode,
    };
}

public sealed class AppConfig
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ThemeSection Theme { get; set; } = new();
    public AccessibilitySection Accessibility { get; set; } = new();
    public ShellSection Shell { get; set; } = new();
    public PanelSection Panels { get; set; } = new();
    public ControlCenterSection ControlCenter { get; set; } = new();
    public LauncherSection Launcher { get; set; } = new();

    /// Bars keyed by name; "main" always exists.
    public Dictionary<string, BarSection> Bars { get; set; } = new() { ["main"] = new BarSection() };

    public BarSection Main => Bars.TryGetValue("main", out var b) ? b : new BarSection();

    public AppConfig Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        Theme = Theme.Clone(),
        Accessibility = Accessibility.Clone(),
        Shell = Shell.Clone(),
        Panels = Panels.Clone(),
        ControlCenter = ControlCenter.Clone(),
        Launcher = Launcher.Clone(),
        Bars = Bars.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
    };

    public static AppConfig Default() => new();
}
