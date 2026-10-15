// Minimal TOML parser + schema-validated codec for the ported config.
// Supports exactly what this config format needs: comments, [table] headers,
// key = value scalars (int, float, bool, string), and inline/multiline arrays.
// Key names and defaults follow Noctalia's example.toml / config schema
// (MIT License, Copyright (c) 2026 noctalia-dev).
using System.Globalization;
using System.Text;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Input;

namespace NoctaliaWin.Core.Config;

public sealed record ConfigDiagnostic(string Path, string Message, bool IsError);

/// Parsed TOML model: values are long, double, bool, string, List&lt;object&gt;, or MiniTomlTable.
internal sealed class MiniTomlTable
{
    public Dictionary<string, object> Entries { get; } = new(StringComparer.Ordinal);
}

internal static class MiniToml
{
    public static (MiniTomlTable Root, List<string> Errors) Parse(string text)
    {
        var root = new MiniTomlTable();
        var errors = new List<string>();
        var current = root;
        List<string>? pendingArray = null;
        string pendingKey = "";
        int depth = 0;

        var rawLines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i];

            if (pendingArray is not null)
            {
                pendingArray.Add(line);
                depth += BracketDelta(line);
                if (depth <= 0)
                {
                    TryAddValue(current, pendingKey, string.Join("\n", pendingArray), i + 1, errors);
                    pendingArray = null;
                }
                continue;
            }

            var stripped = StripComment(line).Trim();
            if (stripped.Length == 0) continue;

            if (stripped.StartsWith('['))
            {
                if (!stripped.EndsWith(']'))
                {
                    errors.Add($"line {i + 1}: malformed table header");
                    continue;
                }
                var path = stripped[1..^1].Trim();
                current = EnsureTable(root, path.Split('.'), i + 1, errors);
                continue;
            }

            var eq = IndexOutsideQuotes(stripped, '=');
            if (eq < 0)
            {
                errors.Add($"line {i + 1}: expected key = value");
                continue;
            }
            var key = stripped[..eq].Trim().Trim('"');
            var value = stripped[(eq + 1)..].Trim();
            var open = BracketDelta(value);
            if (value.StartsWith('[') && open > 0)
            {
                pendingArray = [value];
                pendingKey = key;
                depth = open;
                continue;
            }
            TryAddValue(current, key, value, i + 1, errors);
        }

        if (pendingArray is not null)
            errors.Add("unterminated array at end of file");

        return (root, errors);
    }

    private static void TryAddValue(MiniTomlTable table, string key, string raw, int line, List<string> errors)
    {
        if (table.Entries.ContainsKey(key))
        {
            errors.Add($"line {line}: duplicate key '{key}'");
            return;
        }
        try
        {
            table.Entries[key] = ParseValue(raw);
        }
        catch (FormatException ex)
        {
            errors.Add($"line {line}: value for '{key}' is not a recognized TOML value ({ex.Message})");
        }
    }

    public static object ParseValue(string raw)
    {
        if (raw == "true") return true;
        if (raw == "false") return false;

        if (raw.StartsWith('['))
        {
            var inner = raw.Trim();
            if (!inner.EndsWith(']')) throw new FormatException("unterminated array");
            var list = new List<object>();
            foreach (var part in SplitArrayItems(inner[1..^1]))
            {
                var item = part.Trim().TrimEnd(',').Trim();
                if (item.Length == 0) continue;
                list.Add(ParseValue(item));
            }
            return list;
        }

        if (raw.StartsWith('"'))
            return UnquoteBasic(raw);
        if (raw.StartsWith('\''))
            return UnquoteLiteral(raw);

        var cleaned = raw.Replace("_", "");
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return cleaned.Contains('.') || cleaned.Contains('e') || cleaned.Contains('E')
                ? d
                : (object)(long)d;
        throw new FormatException($"unrecognized value '{raw}'");
    }

    private static int BracketDelta(string line)
    {
        int delta = 0;
        bool inQuote = false;
        char quoteChar = '"';
        foreach (var c in line)
        {
            if (inQuote)
            {
                if (c == quoteChar) inQuote = false;
                continue;
            }
            if (c is '"' or '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == '[') delta++;
            else if (c == ']') delta--;
        }
        return delta;
    }

    private static List<string> SplitArrayItems(string inner)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inQuote = false;
        char quoteChar = '"';
        var sb = new StringBuilder();
        foreach (var c in inner)
        {
            if (inQuote)
            {
                sb.Append(c);
                if (c == quoteChar) inQuote = false;
                continue;
            }
            switch (c)
            {
                case '"' or '\'':
                    inQuote = true;
                    quoteChar = c;
                    sb.Append(c);
                    break;
                case '[' when depth == 0:
                    depth++;
                    sb.Append(c);
                    break;
                case ']' when depth > 0:
                    depth--;
                    sb.Append(c);
                    break;
                case ',' when depth == 0:
                    parts.Add(sb.ToString());
                    sb.Clear();
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        if (sb.Length > 0) parts.Add(sb.ToString());
        return parts;
    }

    private static string UnquoteBasic(string raw)
    {
        var end = IndexClosingQuote(raw, '"');
        var body = raw[1..end];
        return body
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }

    private static string UnquoteLiteral(string raw)
    {
        var end = IndexClosingQuote(raw, '\'');
        return raw[1..end];
    }

    private static int IndexClosingQuote(string s, char quote)
    {
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == '\\' && quote == '"') i++;
            else if (s[i] == quote) return i;
        }
        throw new FormatException("unterminated string");
    }

    private static int IndexOutsideQuotes(string s, char target)
    {
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (inQuote)
            {
                if (c == '\\' && quoteChar == '"') i++;
                else if (c == quoteChar) inQuote = false;
            }
            else if (c is '"' or '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == target) return i;
        }
        return -1;
    }

    private static string StripComment(string line)
    {
        bool inQuote = false;
        char quoteChar = '"';
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuote)
            {
                if (c == '\\' && quoteChar == '"') i++;
                else if (c == quoteChar) inQuote = false;
            }
            else if (c is '"' or '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }
        return line;
    }

    private static MiniTomlTable EnsureTable(MiniTomlTable root, string[] path, int line, List<string> errors)
    {
        var table = root;
        foreach (var segment in path)
        {
            var name = segment.Trim().Trim('"');
            if (!table.Entries.TryGetValue(name, out var existing))
            {
                var created = new MiniTomlTable();
                table.Entries[name] = created;
                table = created;
            }
            else if (existing is MiniTomlTable sub)
            {
                table = sub;
            }
            else
            {
                errors.Add($"line {line}: '{name}' conflicts with a non-table value");
                return table;
            }
        }
        return table;
    }
}

public static class ConfigToml
{
    public static (AppConfig Config, List<ConfigDiagnostic> Diagnostics) Load(string tomlText)
    {
        var diagnostics = new List<ConfigDiagnostic>();
        var config = AppConfig.Default();

        var (root, errors) = MiniToml.Parse(tomlText);
        foreach (var error in errors)
            diagnostics.Add(new("", error, IsError: true));

        config.SchemaVersion = GetInt(root, "schema_version", config.SchemaVersion, "");
        if (config.SchemaVersion < AppConfig.CurrentSchemaVersion)
        {
            diagnostics.Add(new("", $"migrated schema v{config.SchemaVersion} -> v{AppConfig.CurrentSchemaVersion}", false));
            config.SchemaVersion = AppConfig.CurrentSchemaVersion;
        }
        else if (config.SchemaVersion > AppConfig.CurrentSchemaVersion)
        {
            diagnostics.Add(new("", $"config schema v{config.SchemaVersion} is newer than this build supports (v{AppConfig.CurrentSchemaVersion}); loading with current defaults where unknown", false));
        }

        if (GetTable(root, "theme") is { } theme)
            ReadTheme(theme, config.Theme, diagnostics);
        if (GetTable(root, "accessibility") is { } acc)
            config.Accessibility.UiScale = ClampD(acc, "ui_scale", config.Accessibility.UiScale, 0.5, 3.0, "accessibility", diagnostics);
        if (GetTable(root, "shell") is { } shell)
        {
            config.Shell.TimeFormat = GetString(shell, "time_format", config.Shell.TimeFormat, "shell", diagnostics);
            config.Shell.DateFormat = GetString(shell, "date_format", config.Shell.DateFormat, "shell", diagnostics);
            config.Shell.OfflineMode = GetBool(shell, "offline_mode", config.Shell.OfflineMode, "shell", diagnostics);

            if (GetTable(shell, "panel") is { } panel)
                ReadPanel(panel, config.Panels, diagnostics);

            if (GetTable(shell, "launcher") is { } launcher)
                ReadLauncher(launcher, config.Launcher, diagnostics);
        }

        if (GetTable(root, "control_center") is { } controlCenter)
        {
            config.ControlCenter.Width = ClampI(
                controlCenter, "width", config.ControlCenter.Width, 240, 1400, "control_center", diagnostics);
        }

        if (GetTable(root, "bar") is { } bars)
        {
            var parsed = new Dictionary<string, BarSection>();
            foreach (var (name, value) in bars.Entries)
            {
                if (value is not MiniTomlTable barTable)
                {
                    diagnostics.Add(new($"bar.{name}", "expected a table", false));
                    continue;
                }
                var section = name == "main" ? config.Bars["main"].Clone() : new BarSection();
                ReadBar(barTable, section, $"bar.{name}", diagnostics);
                parsed[name] = section;
            }
            if (parsed.Count > 0)
            {
                config.Bars = parsed;
                if (!config.Bars.ContainsKey("main"))
                    config.Bars["main"] = new BarSection();
            }
        }

        foreach (var key in root.Entries.Keys.Except(
            ["schema_version", "theme", "accessibility", "shell", "bar", "control_center"]))
            diagnostics.Add(new(key, "unknown section ignored by the Windows port", false));

        return (config, diagnostics);
    }

    private static void ReadTheme(MiniTomlTable t, ThemeSection s, List<ConfigDiagnostic> d)
    {
        var mode = GetString(t, "mode", s.Mode, "theme", d);
        if (mode is "dark" or "light" or "auto") s.Mode = mode;
        else d.Add(new("theme.mode", $"'{mode}' is not a valid value; using default", false));

        var source = GetString(t, "source", s.Source, "theme", d);
        if (source is "builtin" or "wallpaper" or "community") s.Source = source;
        else d.Add(new("theme.source", $"'{source}' is not a valid value; using default", false));

        var builtin = GetString(t, "builtin", s.Builtin, "theme", d);
        // An unknown name would quietly render as Noctalia, which is the accepted-then-ignored
        // pattern this layer refuses everywhere else.
        if (BuiltinPalettes.ByName(builtin) is not null) s.Builtin = builtin;
        else d.Add(new("theme.builtin", $"'{builtin}' is not a builtin palette; using default", false));
    }

    private static void ReadBar(MiniTomlTable t, BarSection s, string path, List<ConfigDiagnostic> d)
    {
        var position = GetString(t, "position", s.Position, path, d);
        if (position is "top" or "bottom" or "left" or "right") s.Position = position;
        else d.Add(new($"{path}.position", $"'{position}' is not a valid value; using default", false));

        s.Thickness = ClampI(t, "thickness", s.Thickness, 20, 128, path, d);
        s.BackgroundOpacity = ClampD(t, "background_opacity", s.BackgroundOpacity, 0.0, 1.0, path, d);
        s.Radius = ClampI(t, "radius", s.Radius, 0, 100, path, d);
        s.RadiusTopLeft = OptI(t, "radius_top_left", 0, 100, path, d);
        s.RadiusTopRight = OptI(t, "radius_top_right", 0, 100, path, d);
        s.RadiusBottomLeft = OptI(t, "radius_bottom_left", 0, 100, path, d);
        s.RadiusBottomRight = OptI(t, "radius_bottom_right", 0, 100, path, d);
        s.ConcaveEdgeCorners = GetBool(t, "concave_edge_corners", s.ConcaveEdgeCorners, path, d);
        s.MarginEnds = ClampI(t, "margin_ends", s.MarginEnds, 0, 4096, path, d);
        s.MarginEdge = ClampI(t, "margin_edge", s.MarginEdge, 0, 4096, path, d);
        s.Padding = ClampI(t, "padding", s.Padding, 0, 64, path, d);
        s.WidgetSpacing = ClampI(t, "widget_spacing", s.WidgetSpacing, 0, 64, path, d);
        s.Scale = ClampD(t, "scale", s.Scale, 0.25, 4.0, path, d);
        s.FontScale = ClampD(t, "font_scale", s.FontScale, 0.5, 3.0, path, d);
        s.Shadow = GetBool(t, "shadow", s.Shadow, path, d);
        s.AutoHide = GetBool(t, "auto_hide", s.AutoHide, path, d);
        s.ReserveSpace = GetBool(t, "reserve_space", s.ReserveSpace, path, d);
        s.PanelOverlap = ClampI(t, "panel_overlap", s.PanelOverlap, -16, 16, path, d);
        var layer = GetString(t, "layer", s.Layer, path, d);
        if (layer is "top" or "overlay") s.Layer = layer;
        else d.Add(new($"{path}.layer", $"'{layer}' is not a valid value; using default", false));

        if (GetStringArray(t, "start", path, d) is { } start) s.Start = start;
        if (GetStringArray(t, "center", path, d) is { } center) s.Center = center;
        if (GetStringArray(t, "end", path, d) is { } end) s.End = end;
    }

    private static void ReadPanel(MiniTomlTable t, PanelSection s, List<ConfigDiagnostic> d)
    {
        Enum(t, "transparency_mode", s.TransparencyMode, v => v is "solid" or "soft" or "glass",
            nv => s.TransparencyMode = nv, "shell.panel", d);
        Enum(t, "control_center_placement", s.ControlCenterPlacement, v => v is "attached" or "floating",
            nv => s.ControlCenterPlacement = nv, "shell.panel", d);
        Enum(t, "launcher_placement", s.LauncherPlacement, v => v is "attached" or "floating",
            nv => s.LauncherPlacement = nv, "shell.panel", d);
        Enum(t, "control_center_position", s.ControlCenterPosition,
            v => PanelPositions.Contains(v), nv => s.ControlCenterPosition = nv, "shell.panel", d);
        Enum(t, "launcher_position", s.LauncherPosition,
            v => PanelPositions.Contains(v), nv => s.LauncherPosition = nv, "shell.panel", d);

        s.Borders = GetBool(t, "borders", s.Borders, "shell.panel", d);
        s.BorderWidth = ClampD(t, "border_width", s.BorderWidth, 0.0, 8.0, "shell.panel", d);
        s.Shadow = GetBool(t, "shadow", s.Shadow, "shell.panel", d);
        s.OpenNearClickControlCenter =
            GetBool(t, "open_near_click_control_center", s.OpenNearClickControlCenter, "shell.panel", d);
        s.OpenNearClickLauncher = GetBool(t, "open_near_click_launcher", s.OpenNearClickLauncher, "shell.panel", d);
        s.FloatingOffset = ClampI(t, "floating_offset", s.FloatingOffset, 0, 128, "shell.panel", d);
    }

    /// The `[shell.launcher]` keys with a Windows meaning in this build. Everything else in
    /// upstream's launcher section (views, providers, icons, usage sorting) has no rendered
    /// surface here yet, so it is not read: an unknown key keeps its default instead of being
    /// accepted and then silently dropped.
    private static void ReadLauncher(MiniTomlTable t, LauncherSection s, List<ConfigDiagnostic> d)
    {
        s.Width = ClampI(t, "width", s.Width, LauncherSection.MinWidth, LauncherSection.MaxWidth, "shell.launcher", d);
        Enum(t, "search_position", s.SearchPosition, v => v is "top" or "bottom",
            nv => s.SearchPosition = nv, "shell.launcher", d);

        if (!t.Entries.ContainsKey("hotkey")) return;
        var raw = GetString(t, "hotkey", s.Hotkey, "shell.launcher", d);
        // A blank value is a valid answer - it means the user turned the binding off.
        if (HotkeyGesture.TryParse(raw, out _, out var reason)) s.Hotkey = raw.Trim();
        else d.Add(new("shell.launcher.hotkey", reason ?? "not a usable hotkey; using default", false));
    }

    /// The `[shell.panel].*_position` values upstream accepts, as far as this port models
    /// placement. Screen-anchored positions are parsed but the first panel build only
    /// honours "auto" (bar-relative) and "center".
    private static readonly HashSet<string> PanelPositions =
        new(StringComparer.Ordinal)
        {
            "auto", "center", "top_left", "top_right", "bottom_left", "bottom_right",
            "top", "bottom", "left", "right",
        };

    private static void Enum(
        MiniTomlTable t, string key, string current, Func<string, bool> isValid,
        Action<string> apply, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.ContainsKey(key)) return;
        var value = GetString(t, key, current, path, d);
        if (isValid(value)) apply(value);
        else d.Add(new($"{path}.{key}", $"'{value}' is not a valid value; using default", false));
    }

    public static string Save(AppConfig c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Noctalia Win configuration (schema-compatible subset of Noctalia config.toml).");
        sb.AppendLine($"schema_version = {c.SchemaVersion}");
        sb.AppendLine();
        sb.AppendLine("[theme]");
        sb.AppendLine($"mode = \"{c.Theme.Mode}\"");
        sb.AppendLine($"source = \"{c.Theme.Source}\"");
        sb.AppendLine($"builtin = \"{c.Theme.Builtin}\"");
        sb.AppendLine();
        sb.AppendLine("[accessibility]");
        sb.AppendLine($"ui_scale = {F(c.Accessibility.UiScale)}");
        sb.AppendLine();
        sb.AppendLine("[shell]");
        sb.AppendLine($"time_format = \"{c.Shell.TimeFormat}\"");
        sb.AppendLine($"date_format = \"{c.Shell.DateFormat}\"");
        sb.AppendLine($"offline_mode = {Bool(c.Shell.OfflineMode)}");
        sb.AppendLine();
        sb.AppendLine("[shell.panel]");
        sb.AppendLine($"transparency_mode = \"{c.Panels.TransparencyMode}\"");
        sb.AppendLine($"borders = {Bool(c.Panels.Borders)}");
        sb.AppendLine($"border_width = {F(c.Panels.BorderWidth)}");
        sb.AppendLine($"shadow = {Bool(c.Panels.Shadow)}");
        sb.AppendLine($"control_center_placement = \"{c.Panels.ControlCenterPlacement}\"");
        sb.AppendLine($"control_center_position = \"{c.Panels.ControlCenterPosition}\"");
        sb.AppendLine($"launcher_placement = \"{c.Panels.LauncherPlacement}\"");
        sb.AppendLine($"launcher_position = \"{c.Panels.LauncherPosition}\"");
        sb.AppendLine($"open_near_click_control_center = {Bool(c.Panels.OpenNearClickControlCenter)}");
        sb.AppendLine($"open_near_click_launcher = {Bool(c.Panels.OpenNearClickLauncher)}");
        sb.AppendLine($"floating_offset = {c.Panels.FloatingOffset}");
        sb.AppendLine();
        sb.AppendLine("[shell.launcher]");
        sb.AppendLine($"width = {c.Launcher.Width}");
        sb.AppendLine($"search_position = \"{c.Launcher.SearchPosition}\"");
        sb.AppendLine($"hotkey = \"{c.Launcher.Hotkey}\"");
        sb.AppendLine();
        sb.AppendLine("[control_center]");
        sb.AppendLine($"width = {c.ControlCenter.Width}");
        foreach (var (name, bar) in c.Bars)
        {
            sb.AppendLine();
            sb.AppendLine($"[bar.{name}]");
            sb.AppendLine($"position = \"{bar.Position}\"");
            sb.AppendLine($"thickness = {bar.Thickness}");
            sb.AppendLine($"background_opacity = {F(bar.BackgroundOpacity)}");
            sb.AppendLine($"radius = {bar.Radius}");
            Opt(sb, "radius_top_left", bar.RadiusTopLeft);
            Opt(sb, "radius_top_right", bar.RadiusTopRight);
            Opt(sb, "radius_bottom_left", bar.RadiusBottomLeft);
            Opt(sb, "radius_bottom_right", bar.RadiusBottomRight);
            sb.AppendLine($"concave_edge_corners = {Bool(bar.ConcaveEdgeCorners)}");
            sb.AppendLine($"margin_ends = {bar.MarginEnds}");
            sb.AppendLine($"margin_edge = {bar.MarginEdge}");
            sb.AppendLine($"padding = {bar.Padding}");
            sb.AppendLine($"widget_spacing = {bar.WidgetSpacing}");
            sb.AppendLine($"scale = {F(bar.Scale)}");
            sb.AppendLine($"font_scale = {F(bar.FontScale)}");
            sb.AppendLine($"shadow = {Bool(bar.Shadow)}");
            sb.AppendLine($"auto_hide = {Bool(bar.AutoHide)}");
            sb.AppendLine($"reserve_space = {Bool(bar.ReserveSpace)}");
            sb.AppendLine($"panel_overlap = {bar.PanelOverlap}");
            sb.AppendLine($"layer = \"{bar.Layer}\"");
            sb.AppendLine($"start = {Arr(bar.Start)}");
            sb.AppendLine($"center = {Arr(bar.Center)}");
            sb.AppendLine($"end = {Arr(bar.End)}");
        }
        return sb.ToString();

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Bool(bool v) => v ? "true" : "false";
        static string Arr(List<string> v) => "[" + string.Join(", ", v.Select(s => $"\"{s}\"")) + "]";
        static void Opt(StringBuilder sb, string key, int? v)
        {
            if (v.HasValue) sb.AppendLine($"{key} = {v.Value}");
        }
    }

    // ── accessors ────────────────────────────────────────────────────────────

    private static MiniTomlTable? GetTable(MiniTomlTable t, string key) =>
        t.Entries.TryGetValue(key, out var v) && v is MiniTomlTable table ? table : null;

    private static string GetString(MiniTomlTable t, string key, string fallback, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return fallback;
        if (v is string s) return s;
        d.Add(new($"{path}.{key}", "expected string", false));
        return fallback;
    }

    private static bool GetBool(MiniTomlTable t, string key, bool fallback, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return fallback;
        if (v is bool b) return b;
        d.Add(new($"{path}.{key}", "expected boolean", false));
        return fallback;
    }

    private static int GetInt(MiniTomlTable t, string key, int fallback, string path)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return fallback;
        return v is long l and >= int.MinValue and <= int.MaxValue ? (int)l : fallback;
    }

    private static int ClampI(MiniTomlTable t, string key, int fallback, int min, int max, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return fallback;
        if (v is not long l || l < int.MinValue || l > int.MaxValue)
        {
            d.Add(new($"{path}.{key}", "expected integer", false));
            return fallback;
        }
        var i = (int)l;
        if (i < min || i > max)
        {
            d.Add(new($"{path}.{key}", $"{i} out of range [{min}, {max}]; clamped", false));
            i = Math.Clamp(i, min, max);
        }
        return i;
    }

    private static int? OptI(MiniTomlTable t, string key, int min, int max, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v) || v is null) return null;
        if (v is long l && l >= min && l <= max) return (int)l;
        d.Add(new($"{path}.{key}", $"expected integer in [{min}, {max}]", false));
        return null;
    }

    private static double ClampD(MiniTomlTable t, string key, double fallback, double min, double max, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return fallback;
        double dv = v switch { double dd => dd, long l => l, _ => double.NaN };
        if (double.IsNaN(dv))
        {
            d.Add(new($"{path}.{key}", "expected number", false));
            return fallback;
        }
        if (dv < min || dv > max)
        {
            d.Add(new($"{path}.{key}", $"{dv} out of range [{min}, {max}]; clamped", false));
            dv = Math.Clamp(dv, min, max);
        }
        return dv;
    }

    private static List<string>? GetStringArray(MiniTomlTable t, string key, string path, List<ConfigDiagnostic> d)
    {
        if (!t.Entries.TryGetValue(key, out var v)) return null;
        if (v is List<object> arr)
        {
            var list = new List<string>();
            foreach (var item in arr)
            {
                if (item is string s) list.Add(s);
                else d.Add(new($"{path}.{key}", "array items must be strings", false));
            }
            return list;
        }
        d.Add(new($"{path}.{key}", "expected array of strings", false));
        return null;
    }
}
