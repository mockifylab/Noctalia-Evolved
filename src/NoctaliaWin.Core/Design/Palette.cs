// Ported palette model and builtin palette table from Noctalia
// src/ui/palette.h and src/theme/builtin_palettes.cpp
// (MIT License, Copyright (c) 2026 noctalia-dev).
using System.Globalization;
using System.Text;

namespace NoctaliaWin.Core.Design;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb FromHex(string hex)
    {
        var s = hex.StartsWith('#') ? hex[1..] : hex;
        if (s.Length == 3)
            s = new string([s[0], s[0], s[1], s[1], s[2], s[2]]);
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"Invalid hex color: '{hex}'");
        return new Rgb((byte)(v >> 16 & 0xFF), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF));
    }

    public string ToHex() => $"#{R:x2}{G:x2}{B:x2}";
}

public enum ColorRole
{
    Primary,
    OnPrimary,
    Secondary,
    OnSecondary,
    Tertiary,
    OnTertiary,
    Error,
    OnError,
    Surface,
    OnSurface,
    SurfaceVariant,
    OnSurfaceVariant,
    Outline,
    Shadow,
    Hover,
    OnHover,
}

public static class ColorRoleTokens
{
    public static readonly (ColorRole Role, string Token)[] All =
    [
        (ColorRole.Primary, "primary"),
        (ColorRole.OnPrimary, "on_primary"),
        (ColorRole.Secondary, "secondary"),
        (ColorRole.OnSecondary, "on_secondary"),
        (ColorRole.Tertiary, "tertiary"),
        (ColorRole.OnTertiary, "on_tertiary"),
        (ColorRole.Error, "error"),
        (ColorRole.OnError, "on_error"),
        (ColorRole.Surface, "surface"),
        (ColorRole.OnSurface, "on_surface"),
        (ColorRole.SurfaceVariant, "surface_variant"),
        (ColorRole.OnSurfaceVariant, "on_surface_variant"),
        (ColorRole.Outline, "outline"),
        (ColorRole.Shadow, "shadow"),
        (ColorRole.Hover, "hover"),
        (ColorRole.OnHover, "on_hover"),
    ];

    public static ColorRole FromToken(string token)
    {
        foreach (var (role, t) in All)
            if (t == token)
                return role;
        throw new ArgumentException($"Unknown color role token: {token}");
    }

    public static string ToToken(ColorRole role)
    {
        foreach (var (r, t) in All)
            if (r == role)
                return t;
        throw new ArgumentException($"Unknown color role: {role}");
    }
}

public sealed record Palette
{
    required public Rgb Primary { get; init; }
    required public Rgb OnPrimary { get; init; }
    required public Rgb Secondary { get; init; }
    required public Rgb OnSecondary { get; init; }
    required public Rgb Tertiary { get; init; }
    required public Rgb OnTertiary { get; init; }
    required public Rgb Error { get; init; }
    required public Rgb OnError { get; init; }
    required public Rgb Surface { get; init; }
    required public Rgb OnSurface { get; init; }
    required public Rgb SurfaceVariant { get; init; }
    required public Rgb OnSurfaceVariant { get; init; }
    required public Rgb Outline { get; init; }
    required public Rgb Shadow { get; init; }
    required public Rgb Hover { get; init; }
    required public Rgb OnHover { get; init; }

    public Rgb this[ColorRole role] => role switch
    {
        ColorRole.Primary => Primary,
        ColorRole.OnPrimary => OnPrimary,
        ColorRole.Secondary => Secondary,
        ColorRole.OnSecondary => OnSecondary,
        ColorRole.Tertiary => Tertiary,
        ColorRole.OnTertiary => OnTertiary,
        ColorRole.Error => Error,
        ColorRole.OnError => OnError,
        ColorRole.Surface => Surface,
        ColorRole.OnSurface => OnSurface,
        ColorRole.SurfaceVariant => SurfaceVariant,
        ColorRole.OnSurfaceVariant => OnSurfaceVariant,
        ColorRole.Outline => Outline,
        ColorRole.Shadow => Shadow,
        ColorRole.Hover => Hover,
        ColorRole.OnHover => OnHover,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

public enum PaletteMode
{
    Dark,
    Light,
}

public sealed record PaletteSet(Palette Dark, Palette Light)
{
    public Palette For(PaletteMode mode) => mode == PaletteMode.Dark ? Dark : Light;
}

public static class BuiltinPalettes
{
    /// Upstream's builtin set, in upstream's order. Each mode column holds the sixteen roles
    /// of `ColorRoleTokens.All` in that order as bare hex triplets: ten palettes x two modes x
    /// sixteen named assignments is 320 lines of one shape, and the order is pinned by the
    /// reader below and checked by a test rather than by eye.
    private static readonly (string Name, string Dark, string Light)[] Table =
    [
        ("Ayu",
            "E6B450 0B0E14 AAD94C 0B0E14 39BAE6 0B0E14 D95757 0B0E14 0B0E14 D1D1C7 1e222a 8E959E 565B66 000000 39BAE6 0B0E14",
            "FF8F40 F8F9FA 86B300 F8F9FA 55B4D4 F8F9FA E65050 F8F9FA F8F9FA 42474C E4E6E9 6E757C 8A9199 F8F9FA 55B4D4 F8F9FA"),
        ("Catppuccin",
            "cba6f7 11111b fab387 11111b 94e2d5 11111b f38ba8 11111b 1e1e2e cdd6f4 313244 a3b4eb 4c4f69 11111b 94e2d5 11111b",
            "8839ef eff1f5 fe640b eff1f5 40a02b eff1f5 d20f39 dce0e8 eff1f5 4c4f69 ccd0da 6c6f85 a5adcb dce0e8 40a02b eff1f5"),
        ("Dracula",
            "bd93f9 282A36 ff79c6 4e1d32 8be9fd 003543 FF5555 282A36 282A36 F8F8F2 44475A d6d8e0 5a5e77 282A36 8be9fd 003543",
            "8332f4 ffffff ff1399 ffffff 0398b9 ffffff FF5555 282A36 f8f8f2 282a36 e6e6ea 44475a cacad3 d6d8e0 0398b9 ffffff"),
        ("Eldritch",
            "37f499 171928 04d1f9 171928 a48cf2 171928 f16c75 171928 212337 ebfafa 292e42 ABB4DA 3b4261 414868 a48cf2 171928",
            "fb5bb6 1e2029 0ad6ff 1e2029 8a69f7 1e2029 fb5b66 1e2029 f0f3f4 1e2029 d5d9db 1e2029 c9cbcd e2e6e8 8a69f7 1e2029"),
        ("Gruvbox",
            "b8bb26 282828 fabd2F 282828 83a598 282828 fb4934 282828 282828 fbf1c7 3c3836 ebdbb2 57514e 282828 83a598 282828",
            "98971a fbf1c7 d79921 fbf1c7 458588 fbf1c7 cc241d fbf1c7 fbf1c7 3c3836 ebdbb2 7c6f64 bdae93 d5c4a1 458588 fbf1c7"),
        ("Kanagawa",
            "76946a 1f1f28 c0a36e 1f1f28 7e9cd8 1f1f28 c34043 1f1f28 1f1f28 c8c093 2a2a37 717c7c 363646 1f1f28 7e9cd8 1f1f28",
            "6f894e f2ecbc 77713F f2ecbc 4d699b f2ecbc c84053 f2ecbc f2ecbc 545464 e5ddb0 8a8980 cfc49c f2ecbc 4d699b f2ecbc"),
        ("Noctalia",
            "fff59b 0e0e43 a9aefe 0e0e43 9BFECE 0e0e43 FD4663 0e0e43 070722 f3edf7 11112d 7c80b4 21215F 070722 9BFECE 0e0e43",
            "5d65f5 dadcff 8E93D8 dadcff 0e0e43 fef29a FD4663 0e0e43 e6e8fa 0e0e43 eff0ff 4b55c8 8288fc f3edf7 0e0e43 fef29a"),
        ("Nord",
            "8fbcbb 2e3440 88c0d0 2e3440 5e81ac 2e3440 bf616a 2e3440 2e3440 eceff4 3b4252 d8dee9 505a70 2e3440 5e81ac 2e3440",
            "5e81ac eceff4 64adc2 eceff4 6fa9a8 eceff4 bf616a eceff4 eceff4 2e3440 e5e9f0 4c566a c5cedd d8dee9 6fa9a8 eceff4"),
        ("Rosé Pine",
            "ebbcba 191724 9ccfd8 191724 31748F e0def4 eb6f92 191724 191724 e0def4 26233a 908caa 403d52 191724 524f67 e0def4",
            "d7827e faf4ed 56949F faf4ed 286983 faf4ed b4637a faf4ed fffaf3 575279 f2e9e1 797593 dfdad9 faf4ed cecacd 575279"),
        ("Tokyo-Night",
            "7aa2f7 16161e bb9af7 16161e 9ece6a 16161e f7768e 16161e 1a1b26 c0caf5 24283b 9aa5ce 353D57 15161e 9ece6a 16161e",
            "2e7de9 e1e2e7 9854f1 e1e2e7 587539 e1e2e7 f52a65 e1e2e7 e1e2e7 3760bf d0d5e3 6172b0 b4b5b9 a8aecb 587539 e1e2e7"),
    ];

    private static readonly PaletteSet[] Sets =
        [.. Table.Select(p => new PaletteSet(Parse(p.Dark), Parse(p.Light)))];

    private static readonly string[] Slugs = [.. Table.Select(p => Slug(p.Name))];

    /// The names a settings page offers, in upstream's order.
    public static IReadOnlyList<string> Names { get; } = [.. Table.Select(p => p.Name)];

    /// The four dots upstream paints on a scheme card, over a strip tinted with the surface
    /// (upstream's builtinPalettePreview and ColorSwatchPreviewStrip).
    public static readonly IReadOnlyList<ColorRole> PreviewRoles =
        [ColorRole.Primary, ColorRole.Secondary, ColorRole.Tertiary, ColorRole.Error];

    /// Upstream's default, and what an unknown name resolves to.
    public static PaletteSet Noctalia => Sets[Array.IndexOf(Slugs, "noctalia")];

    public static PaletteSet? ByName(string? name)
    {
        var slug = Slug(name);
        for (int i = 0; i < Slugs.Length; i++)
            if (Slugs[i] == slug)
                return Sets[i];

        return null;
    }

    /// "Rosé Pine", "rose-pine" and "ROSE PINE" are one palette to someone typing a name into
    /// a config file, so accents, case and separators all collapse away.
    public static string Slug(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormKD))
        {
            // FormKD splits an accented letter into its base plus a combining mark, and the
            // mark is what sits above 0x2FF - dropping those is the point of decomposing.
            if (c < 0x300 && char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static Palette Parse(string hexes)
    {
        var parts = hexes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != ColorRoleTokens.All.Length)
            throw new InvalidOperationException(
                $"a builtin palette needs {ColorRoleTokens.All.Length} roles, got {parts.Length}");

        var rgb = parts.Select(Rgb.FromHex).ToArray();
        return new Palette
        {
            Primary = rgb[0],
            OnPrimary = rgb[1],
            Secondary = rgb[2],
            OnSecondary = rgb[3],
            Tertiary = rgb[4],
            OnTertiary = rgb[5],
            Error = rgb[6],
            OnError = rgb[7],
            Surface = rgb[8],
            OnSurface = rgb[9],
            SurfaceVariant = rgb[10],
            OnSurfaceVariant = rgb[11],
            Outline = rgb[12],
            Shadow = rgb[13],
            Hover = rgb[14],
            OnHover = rgb[15],
        };
    }
}
