// The declarative settings model: the same page -> group -> row shape Noctalia's settings
// registry builds (src/ui/sections/*, src/widgets/settings_row.cpp — MIT License,
// Copyright (c) 2026 noctalia-dev), narrowed to the keys and actions this port can carry out
// in Windows. A row that could not change anything on the machine is not in the catalog.
//
// Every row carries its value as a string, the way a config file does, so the editor, the
// search index, reset and the "is this overridden" badge all work on one type. The control
// kinds know how to read that string back (a slider parses a number, a toggle a bool).
using System.Globalization;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Config;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Input;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.Core.Settings;

public enum SettingsControlKind
{
    Toggle,
    Pills,
    Dropdown,
    Slider,
    Text,
    Keybind,
    PaletteGrid,
    WidgetLane,
    Button,
}

public sealed record SettingsChoice(string Value, string Label);

/// What a row may touch. `Config` is the working copy the editor saves; the rest are the
/// Windows-side facts and services a row acts on, which is why a row is not simply a config
/// path — "start with Windows" lives in the registry, and the version is a build fact.
public sealed record SettingsScope(
    AppConfig Config,
    IStartupService? Autostart = null,
    string? ConfigPath = null,
    string? AppVersion = null)
{
    /// A scope with only the config, for rows whose default value is a config default.
    public static SettingsScope Of(AppConfig config) => new(config);
}

/// Applies a value to a scope. Returns null when it took effect, or the reason it did not.
public delegate string? SettingsWriter(SettingsScope scope, string value);

public sealed class SettingsField
{
    /// The dotted config path this row writes, used for search and to label the row's origin.
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public SettingsControlKind Control { get; init; } = SettingsControlKind.Toggle;

    public required Func<SettingsScope, string> Read { get; init; }

    /// null for a row that only displays or only acts, which is what makes it read-only.
    public SettingsWriter? Write { get; init; }

    /// The value the config ships with, so Reset means the same thing it does upstream.
    public string DefaultValue { get; init; } = "";

    public IReadOnlyList<SettingsChoice> Choices { get; init; } = [];
    public double Min { get; init; }
    public double Max { get; init; } = 1;
    public double Step { get; init; } = 1;
    public int Decimals { get; init; }
    public string Unit { get; init; } = "";

    /// What a slider's lowest value means in words, for the config keys that store an unset
    /// sentinel: the per-corner radii are optional, and -1 is "follow the corner radius above".
    public string OffLabel { get; init; } = "";

    public bool Advanced { get; init; }
    public string Placeholder { get; init; } = "";

    /// A stable handle for a row that does something instead of holding a value; the window
    /// owns what each action means.
    public string? Action { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool IsReadOnly => Write is null || Action is not null;
    public bool IsButton => Action is not null;

    public string ValueFor(SettingsScope scope) => Read(scope);

    /// The value as a number, for a slider's thumb and its numeric box. Rows that are not
    /// numeric return null so the window never has to guess.
    public double? NumberFor(SettingsScope scope) =>
        double.TryParse(Read(scope), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// The value the way a person reads it: units, and the words a sentinel stands for.
    public string DisplayValueFor(SettingsScope scope)
    {
        var raw = Read(scope);
        if (OffLabel.Length > 0 && NumberFor(scope) == Min)
            return OffLabel;
        if (Unit.Length == 0 || raw.Length == 0)
            return raw;
        return $"{raw} {Unit}";
    }

    /// The label of the chosen option, for pills, dropdowns and the scheme cards.
    public string LabelFor(SettingsScope scope)
    {
        var value = Read(scope);
        return Choices.FirstOrDefault(c => c.Value == value)?.Label ?? value;
    }

    /// Upstream shows a reset affordance only on a row that differs from its default.
    public bool IsOverridden(SettingsScope scope) =>
        !IsReadOnly && Read(scope) != DefaultValue;

    public string? WriteValue(SettingsScope scope, string value) => Write?.Invoke(scope, value);

    public string? Reset(SettingsScope scope) => Write?.Invoke(scope, DefaultValue);

    /// The text the search index reads for this row.
    public string SearchText(string pageTitle, string groupTitle) =>
        string.Join('\n', [Id, Title, Description, pageTitle, groupTitle, string.Join(' ', Tags)])
            .ToLowerInvariant();

    public static SettingsField Toggle(
        string id, string title, string description,
        Func<SettingsScope, bool> get, Action<SettingsScope, bool> set,
        bool advanced = false, params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Toggle,
            Read = s => BoolText(get(s)),
            Write = (s, v) => OnBool(v, s, set),
            DefaultValue = BoolText(get(SettingsScope.Of(AppConfig.Default()))),
            Advanced = advanced,
            Tags = tags,
        };

    /// A row whose only job is to say something true: the version, the config file's path.
    public static SettingsField Info(
        string id, string title, string description, Func<SettingsScope, string?> get,
        params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Text,
            Read = s => get(s) ?? "",
            Write = null,
            Tags = tags,
        };

    public static SettingsField Push(
        string id, string title, string description, string action, params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Button,
            Action = action,
            Read = _ => "",
            Write = null,
            Tags = tags,
        };

    public static SettingsField Slider(
        string id, string title, string description,
        Func<SettingsScope, double> get, Action<SettingsScope, double> set,
        double min, double max, double step = 1, int decimals = 0,
        string unit = "", bool advanced = false, string offLabel = "", params string[] tags)
    {
        string Format(double value) =>
            value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        return new SettingsField
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Slider,            Read = s => Format(get(s)),
            Write = (s, v) =>
            {
                if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return $"'{v}' is not a number";

                // The same clamp the config loader applies, so a typed value lands where a
                // dragged one would and the row never shows something the file disagrees with.
                set(s, Math.Clamp(parsed, min, max));
                return null;
            },
            DefaultValue = Format(get(SettingsScope.Of(AppConfig.Default()))),
            Min = min,
            Max = max,
            Step = step,
            Decimals = decimals,
            Unit = unit,
            OffLabel = offLabel,
            Advanced = advanced,
            Tags = tags,
        };
    }

    /// A slider for a whole-number key. Rounding happens here so a row can never write a
    /// fractional thickness into a config field the loader reads back as an integer.
    public static SettingsField IntSlider(
        string id, string title, string description,
        Func<SettingsScope, int> get, Action<SettingsScope, int> set,
        int min, int max, int step = 1, string unit = "",
        bool advanced = false, string offLabel = "", params string[] tags) =>
        Slider(
            id, title, description,
            s => get(s),
            (s, v) => set(s, (int)Math.Round(v, MidpointRounding.AwayFromZero)),
            min, max, step, 0, unit, advanced, offLabel, tags);

    /// A value chosen from a fixed list: the pills and dropdowns of upstream's settings rows.
    public static SettingsField Choice(
        string id, string title, string description, SettingsControlKind control,
        Func<SettingsScope, string> get, Action<SettingsScope, string> set,
        IReadOnlyList<SettingsChoice> choices, bool advanced = false, params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = control,
            Read = get,
            Write = (s, v) =>
            {
                if (!choices.Any(c => c.Value == v))
                    return $"'{v}' is not one of the offered values";
                set(s, v);
                return null;
            },
            DefaultValue = get(SettingsScope.Of(AppConfig.Default())),
            Choices = choices,
            Advanced = advanced,
            Tags = tags,
        };

    /// A free-text row. `required` refuses blank, which some formats cannot render from.
    public static SettingsField Text(
        string id, string title, string description,
        Func<SettingsScope, string> get, Action<SettingsScope, string> set,
        string placeholder = "", bool required = false, params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Text,
            Read = get,
            Write = (s, v) =>
            {
                if (required && string.IsNullOrWhiteSpace(v))
                    return "this cannot be empty";
                set(s, v);
                return null;
            },
            DefaultValue = get(SettingsScope.Of(AppConfig.Default())),
            Placeholder = placeholder,
            Tags = tags,
        };

    /// A one-line list of widget names, which is how the config stores a bar's lane. The
    /// editor writes it back in the same order the user arranged.
    public static SettingsField Lane(
        string id, string title, string description,
        Func<SettingsScope, List<string>> get, Action<SettingsScope, List<string>> set,
        params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.WidgetLane,
            Read = s => string.Join(',', get(s)),
            Write = (s, v) =>
            {
                var names = Split(v);
                var unknown = names.FirstOrDefault(n => !WidgetCatalog.IsKnown(n));
                if (unknown is not null)
                    return $"'{unknown}' is not a widget this build knows";
                set(s, names);
                return null;
            },
            DefaultValue = string.Join(',', get(SettingsScope.Of(AppConfig.Default()))),
            Tags = tags,
        };

    /// A global shortcut. The gesture text is validated here and registered by the app, so a
    /// row cannot end up holding a combination Windows would refuse. An empty value means the
    /// user unbound it, which is a valid answer rather than a missing one.
    public static SettingsField Keybind(
        string id, string title, string description,
        Func<SettingsScope, string> get, Action<SettingsScope, string> set,
        params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.Keybind,
            Read = get,
            Write = (s, v) => HotkeyGesture.TryParse(v, out _, out var reason) is false
                ? reason
                : SetAnd(s, v, set),
            DefaultValue = get(SettingsScope.Of(AppConfig.Default())),
            Tags = tags,
        };

    /// One of the builtin colour schemes, chosen from cards rather than a list.
    public static SettingsField Scheme(
        string id, string title, string description,
        Func<SettingsScope, string> get, Action<SettingsScope, string> set,
        params string[] tags) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Control = SettingsControlKind.PaletteGrid,
            Read = get,
            Choices = [.. BuiltinPalettes.Names.Select(n => new SettingsChoice(n, n))],
            Write = (s, v) => BuiltinPalettes.ByName(v) is null
                ? $"'{v}' is not a builtin scheme"
                : SetAnd(s, v, set),
            DefaultValue = get(SettingsScope.Of(AppConfig.Default())),
            Tags = tags,
        };

    private static string? SetAnd(SettingsScope scope, string value, Action<SettingsScope, string> set)
    {
        set(scope, value);
        return null;
    }

    private static string BoolText(bool value) => value ? "true" : "false";
    private static string? OnBool(string value, SettingsScope scope, Action<SettingsScope, bool> set)
    {
        if (!bool.TryParse(value, out var parsed))
            return $"'{value}' is not true or false";
        set(scope, parsed);
        return null;
    }

    public static List<string> Split(string value) =>
        [.. value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}

public sealed record SettingsGroup(string Id, string Title, IReadOnlyList<SettingsField> Fields);

/// A sidebar entry and the rows under it. `Glyph` is a semantic name the window maps onto a
/// font it is allowed to ship, not a glyph copied from upstream.
public sealed record SettingsPage(
    string Id,
    string Title,
    string Glyph,
    IReadOnlyList<SettingsGroup> Groups)
{
    public string SearchText => (Id + "\n" + Title).ToLowerInvariant();

    public IEnumerable<SettingsField> Fields => Groups.SelectMany(g => g.Fields);
}
