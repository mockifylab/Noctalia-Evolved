// Ported palette model and builtin "Noctalia" palette from Noctalia
// src/ui/palette.h and src/theme/builtin_palettes.cpp
// (MIT License, Copyright (c) 2026 noctalia-dev).
using System.Globalization;

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
    // Noctalia builtin palette (upstream default).
    public static readonly PaletteSet Noctalia = new(
        new Palette
        {
            Primary = Rgb.FromHex("#fff59b"),
            OnPrimary = Rgb.FromHex("#0e0e43"),
            Secondary = Rgb.FromHex("#a9aefe"),
            OnSecondary = Rgb.FromHex("#0e0e43"),
            Tertiary = Rgb.FromHex("#9BFECE"),
            OnTertiary = Rgb.FromHex("#0e0e43"),
            Error = Rgb.FromHex("#FD4663"),
            OnError = Rgb.FromHex("#0e0e43"),
            Surface = Rgb.FromHex("#070722"),
            OnSurface = Rgb.FromHex("#f3edf7"),
            SurfaceVariant = Rgb.FromHex("#11112d"),
            OnSurfaceVariant = Rgb.FromHex("#7c80b4"),
            Outline = Rgb.FromHex("#21215F"),
            Shadow = Rgb.FromHex("#070722"),
            Hover = Rgb.FromHex("#9BFECE"),
            OnHover = Rgb.FromHex("#0e0e43"),
        },
        new Palette
        {
            Primary = Rgb.FromHex("#5d65f5"),
            OnPrimary = Rgb.FromHex("#dadcff"),
            Secondary = Rgb.FromHex("#8E93D8"),
            OnSecondary = Rgb.FromHex("#dadcff"),
            Tertiary = Rgb.FromHex("#0e0e43"),
            OnTertiary = Rgb.FromHex("#fef29a"),
            Error = Rgb.FromHex("#FD4663"),
            OnError = Rgb.FromHex("#0e0e43"),
            Surface = Rgb.FromHex("#e6e8fa"),
            OnSurface = Rgb.FromHex("#0e0e43"),
            SurfaceVariant = Rgb.FromHex("#eff0ff"),
            OnSurfaceVariant = Rgb.FromHex("#4b55c8"),
            Outline = Rgb.FromHex("#8288fc"),
            Shadow = Rgb.FromHex("#f3edf7"),
            Hover = Rgb.FromHex("#0e0e43"),
            OnHover = Rgb.FromHex("#fef29a"),
        });

    // Ayu builtin palette, dark mode (upstream builtin for parity tests).
    public static readonly Palette AyuDark = new()
    {
        Primary = Rgb.FromHex("#E6B450"),
        OnPrimary = Rgb.FromHex("#0B0E14"),
        Secondary = Rgb.FromHex("#AAD94C"),
        OnSecondary = Rgb.FromHex("#0B0E14"),
        Tertiary = Rgb.FromHex("#39BAE6"),
        OnTertiary = Rgb.FromHex("#0B0E14"),
        Error = Rgb.FromHex("#D95757"),
        OnError = Rgb.FromHex("#0B0E14"),
        Surface = Rgb.FromHex("#0B0E14"),
        OnSurface = Rgb.FromHex("#D1D1C7"),
        SurfaceVariant = Rgb.FromHex("#1e222a"),
        OnSurfaceVariant = Rgb.FromHex("#8E959E"),
        Outline = Rgb.FromHex("#565B66"),
        Shadow = Rgb.FromHex("#000000"),
        Hover = Rgb.FromHex("#39BAE6"),
        OnHover = Rgb.FromHex("#0B0E14"),
    };

    public static readonly Palette AyuLight = new()
    {
        Primary = Rgb.FromHex("#FF8F40"),
        OnPrimary = Rgb.FromHex("#F8F9FA"),
        Secondary = Rgb.FromHex("#86B300"),
        OnSecondary = Rgb.FromHex("#F8F9FA"),
        Tertiary = Rgb.FromHex("#55B4D4"),
        OnTertiary = Rgb.FromHex("#F8F9FA"),
        Error = Rgb.FromHex("#E65050"),
        OnError = Rgb.FromHex("#F8F9FA"),
        Surface = Rgb.FromHex("#F8F9FA"),
        OnSurface = Rgb.FromHex("#42474C"),
        SurfaceVariant = Rgb.FromHex("#E4E6E9"),
        OnSurfaceVariant = Rgb.FromHex("#6E757C"),
        Outline = Rgb.FromHex("#8A9199"),
        Shadow = Rgb.FromHex("#F8F9FA"),
        Hover = Rgb.FromHex("#55B4D4"),
        OnHover = Rgb.FromHex("#F8F9FA"),
    };

    public static PaletteSet? ByName(string name) => name.ToLowerInvariant() switch
    {
        "noctalia" => Noctalia,
        "ayu" => new PaletteSet(AyuDark, AyuLight),
        _ => null,
    };
}
