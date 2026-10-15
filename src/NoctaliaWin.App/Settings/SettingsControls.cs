// The settings window's controls, drawn from the palette the same way the bar and the panels
// are: no system chrome and no icon font (upstream's icon set is not part of this
// repository's licence grant), and every value read back out of the config rather than
// remembered locally.
//
// The shapes follow Noctalia's settings rows (src/widgets/settings_row.cpp,
// src/ui/sections/settings_page.cpp — MIT License, Copyright (c) 2026 noctalia-dev): a row is
// its title and description on the left and one control on the right, inside a card whose
// fill is the palette's surface variant.
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using NoctaliaWin.App.Design;
using NoctaliaWin.Core.Design;
using NoctaliaWin.Core.Settings;
using NoctaliaWin.Core.Shell;

namespace NoctaliaWin.App.Settings;

/// Everything a control needs to read, write and repaint. `Committed` is what makes the window
/// re-render the page from the store, so a row never shows a value the file disagrees with.
internal sealed record SettingsContext(
    ThemeBrushes Theme,
    double Zoom,
    double Font,
    SettingsEditor Editor,
    Action Committed,
    Action<string?> Notice,
    Func<string, string?> RunAction);

internal static class SettingsControls
{
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    public const double SliderWidth = 190;

    /// Wide enough for the largest read-out the catalog produces ("1000 px" at the caption
    /// size), since a truncated number reads as a broken control.
    public const double NumericWidth = 76;

    // ── rows ─────────────────────────────────────────────────────────────────

    public static FrameworkElement Row(SettingsField field, SettingsContext ctx)
    {
        var scope = ctx.Editor.Scope();
        var grid = new Grid { MinHeight = StyleTokens.ControlHeight * ctx.Zoom };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, StyleTokens.SpaceXs * ctx.Zoom, StyleTokens.SpaceXs * ctx.Zoom, 0),
        };
        text.Children.Add(new TextBlock
        {
            Text = field.Title,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            FontWeight = FontWeights.Bold,
            Foreground = ctx.Theme[ColorRole.OnSurface],
        });

        if (field.Description.Length > 0)
        {
            text.Children.Add(new TextBlock
            {
                Text = field.Description,
                FontFamily = UiFont,
                FontSize = StyleTokens.FontSizeCaption * ctx.Font,
                Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 88 * ctx.Zoom,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2 * ctx.Zoom, StyleTokens.SpaceMd * ctx.Zoom, 0),
            });
        }

        if (field.Advanced)
            text.Children.Add(Badge("advanced",
                Alpha(ctx.Theme, ColorRole.OnSurfaceVariant, 0.12),
                ctx.Theme[ColorRole.OnSurfaceVariant], ctx));

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (field.IsOverridden(scope) && field.Control != SettingsControlKind.Button)
        {
            right.Children.Add(ResetButton(field, ctx));
            right.Children.Add(Badge("changed",
                Alpha(ctx.Theme, ColorRole.Primary, 0.15),
                ctx.Theme[ColorRole.Primary], ctx));
        }

        right.Children.Add(Build(field, ctx));
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(0, StyleTokens.SpaceXs * ctx.Zoom, 0, StyleTokens.SpaceXs * ctx.Zoom),
        };
    }

    private static UIElement Build(SettingsField field, SettingsContext ctx) => field.Control switch
    {
        SettingsControlKind.Toggle => Toggle(field, ctx),
        SettingsControlKind.Pills => Pills(field, ctx),
        SettingsControlKind.Dropdown => Dropdown(field, ctx),
        SettingsControlKind.Slider => SliderRow(field, ctx),
        SettingsControlKind.Text => TextRow(field, ctx),
        SettingsControlKind.Keybind => Keybind(field, ctx),
        SettingsControlKind.PaletteGrid => Schemes(field, ctx),
        SettingsControlKind.WidgetLane => Lane(field, ctx),
        SettingsControlKind.Button => PushButton(field, ctx),
        _ => new TextBlock { Text = string.Empty },
    };

    // ── individual controls ──────────────────────────────────────────────────

    private static FrameworkElement Toggle(SettingsField field, SettingsContext ctx) => SwitchTrack(
        field.ValueFor(ctx.Editor.Scope()) == "true", ctx,
        on => Commit(field, on ? "false" : "true", ctx));

    /// The switch itself: an outline track that becomes primary when on, with an on-primary
    /// thumb that slides to the other side. Shared by the rows and the sidebar's advanced
    /// toggle, because the second one is the same control with no config key behind it.
    private static FrameworkElement SwitchTrack(bool on, SettingsContext ctx, Action<bool> OnChange)
    {
        var thumb = new Border
        {
            Width = 16 * ctx.Zoom,
            Height = 16 * ctx.Zoom,
            CornerRadius = new CornerRadius(8 * ctx.Zoom),
            Background = ctx.Theme[ColorRole.OnPrimary],
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Margin = new Thickness(3 * ctx.Zoom, 0, 3 * ctx.Zoom, 0),
        };

        var track = new Border
        {
            Width = 44 * ctx.Zoom,
            Height = 24 * ctx.Zoom,
            CornerRadius = new CornerRadius(12 * ctx.Zoom),
            BorderThickness = new Thickness(1),
            Child = thumb,
        };

        var button = BareButton(track, ctx);

        void Paint(bool state)
        {
            track.Background = state
                ? ctx.Theme[ColorRole.Primary]
                : Alpha(ctx.Theme, ColorRole.Outline, 0.7);
            track.BorderBrush = state
                ? ctx.Theme[ColorRole.Primary]
                : Alpha(ctx.Theme, ColorRole.OnSurfaceVariant, 0.5);
            thumb.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            button.ToolTip = state ? "On" : "Off";
        }

        Paint(on);
        button.Click += (_, _) =>
        {
            // The switch paints at once and the window then re-renders from the store, so a
            // refused write cannot leave it showing a state the file does not hold.
            Paint(!on);
            OnChange(!on);
        };
        return button;
    }

    /// A labelled switch for a state the window itself owns, drawn exactly like a row's toggle.
    public static FrameworkElement SwitchRow(string label, bool on, SettingsContext ctx, Action OnToggle)
    {
        var row = Stack(Orientation.Horizontal, ctx);
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, StyleTokens.SpaceSm * ctx.Zoom, 0),
        });

        var state = SwitchTrack(on, ctx, _ => OnToggle());
        row.Children.Add(state);
        return row;
    }

    /// A sidebar entry. Upstream draws these with an icon font this port may not ship, so the
    /// word alone carries the row, with the selected state in the palette's primary.
    public static Button NavButton(string text, bool selected, SettingsContext ctx, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            FontWeight = selected ? FontWeights.Bold : FontWeights.Medium,
            Foreground = selected
                ? ctx.Theme[ColorRole.OnPrimary]
                : ctx.Theme[ColorRole.OnSurface],
            Background = selected ? ctx.Theme[ColorRole.Primary] : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 6 * ctx.Zoom,
                StyleTokens.SpaceSm * ctx.Zoom, 6 * ctx.Zoom),
            Margin = new Thickness(0, 0, 0, 2 * ctx.Zoom),
            Cursor = Cursors.Hand,
        };
        button.Template = NavTemplate(ctx, selected);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static ControlTemplate NavTemplate(SettingsContext ctx, bool selected)
    {
        var border = new FrameworkElementFactory(typeof(Border), "root");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(StyleTokens.RadiusMd * ctx.Zoom));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };

        // A selected row is already primary-coloured; tinting it on hover would say the click
        // had somewhere else to land.
        if (!selected)
        {
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, ctx.HoverFill()) { TargetName = "root" });
            template.Triggers.Add(hover);
        }
        return template;
    }

    /// The sidebar's search field. WPF's TextBox has no placeholder of its own, so the hint is
    /// a text block under the editor that goes away the moment there is something typed.
    public static TextBox SearchBox(string placeholder, SettingsContext ctx, out FrameworkElement host)
    {
        var box = new TextBox
        {
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            CaretBrush = ctx.Theme[ColorRole.OnSurface],
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            Height = StyleTokens.ControlHeight * ctx.Zoom,
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = FieldTemplate(ctx, StyleTokens.RadiusMd),
        };

        var hint = new TextBlock
        {
            Text = placeholder,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(StyleTokens.SpaceSm * ctx.Zoom + 2, 0, 0, 0),
            IsHitTestVisible = false,
        };

        void PaintHint() => hint.Visibility = box.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        box.TextChanged += (_, _) => PaintHint();
        PaintHint();

        var grid = new Grid();
        grid.Children.Add(hint);
        grid.Children.Add(box);
        host = grid;
        return box;
    }


    private static FrameworkElement Pills(SettingsField field, SettingsContext ctx)
    {
        var current = field.ValueFor(ctx.Editor.Scope());
        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var track = new Border
        {
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            CornerRadius = new CornerRadius(StyleTokens.RadiusMd * ctx.Zoom),
            BorderBrush = Alpha(ctx.Theme, ColorRole.Outline, 0.6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2 * ctx.Zoom),
            Child = row,
            VerticalAlignment = VerticalAlignment.Center,
        };

        for (int index = 0; index < field.Choices.Count; index++)
        {
            var choice = field.Choices[index];

            if (index > 0)
            {
                row.Children.Add(new Border
                {
                    Width = 1,
                    Height = 18 * ctx.Zoom,
                    Background = Alpha(ctx.Theme, ColorRole.Outline, 0.8),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            bool selected = choice.Value == current;
            var pill = new Border
            {
                Child = new TextBlock
                {
                    Text = choice.Label,
                    FontFamily = UiFont,
                    FontSize = StyleTokens.FontSizeCaption * ctx.Font,
                    FontWeight = selected ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = selected
                        ? ctx.Theme[ColorRole.OnPrimary]
                        : ctx.Theme[ColorRole.OnSurface],
                },
                Background = selected ? ctx.Theme[ColorRole.Primary] : Brushes.Transparent,
                CornerRadius = new CornerRadius(StyleTokens.RadiusSm * ctx.Zoom),
                Padding = new Thickness(StyleTokens.SpaceMd * ctx.Zoom * 0.75, 5 * ctx.Zoom,
                    StyleTokens.SpaceMd * ctx.Zoom * 0.75, 5 * ctx.Zoom),
            };

            var button = BareButton(pill, ctx);
            button.Click += (_, _) => Commit(field, choice.Value, ctx);
            row.Children.Add(button);
        }

        return track;
    }

    /// A menu button: the chosen value stays on show and the popup lists the rest. WPF's
    /// default ComboBox paints a system-coloured box over a white popup that no palette can
    /// tint, so its template is replaced wholesale.
    private static FrameworkElement Dropdown(SettingsField field, SettingsContext ctx) =>
        DropdownWith(field.Choices, field.ValueFor(ctx.Editor.Scope()),
            value => Commit(field, value, ctx), ctx, StyleTokens.ControlHeight * ctx.Zoom, 190 * ctx.Zoom);

    private static ComboBox DropdownWith(
        IReadOnlyList<SettingsChoice> choices, string current, Action<string> OnPick,
        SettingsContext ctx, double height, double minWidth)
    {
        var box = new ComboBox
        {
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            Height = height,
            MinWidth = minWidth,
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = DropdownTemplate(ctx),
            Cursor = Cursors.Hand,
        };

        var byValue = new Dictionary<string, ComboBoxItem>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            var item = Item(choice.Label, choice.Value, ctx);
            byValue[choice.Value] = item;
            box.Items.Add(item);
        }

        if (byValue.TryGetValue(current, out var selected))
            box.SelectedItem = selected;

        box.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, box) || box.SelectedItem is not ComboBoxItem item) return;
            var picked = (string)item.Tag;
            if (picked != current) OnPick(picked);
        };

        // The template's toggle reads IsDropDownOpen one way, the way WPF's own template shows
        // the popup, so the click that opens or closes it is handled here rather than with a
        // two-way binding inside the XAML.
        box.AddHandler(ToggleButton.ClickEvent, new RoutedEventHandler(
            (sender, _) => { if (sender is ComboBox owner) owner.IsDropDownOpen = !owner.IsDropDownOpen; }));
        return box;
    }

    private static ComboBoxItem Item(string label, string value, SettingsContext ctx) => new()
    {
        Content = label,
        Tag = value,
        FontFamily = UiFont,
        FontSize = StyleTokens.FontSizeCaption * ctx.Font,
        Foreground = ctx.Theme[ColorRole.OnSurface],
        Background = Brushes.Transparent,
        Template = ItemTemplate(ctx),
        Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 5 * ctx.Zoom,
            StyleTokens.SpaceSm * ctx.Zoom, 5 * ctx.Zoom),
    };

    private static FrameworkElement SliderRow(SettingsField field, SettingsContext ctx)
    {
        var scope = ctx.Editor.Scope();
        var number = field.NumberFor(scope) ?? field.Min;

        var slider = new Slider
        {
            Minimum = field.Min,
            Maximum = field.Max,
            SmallChange = field.Step,
            LargeChange = field.Step * 4,
            Value = Math.Clamp(number, field.Min, field.Max),
            IsMoveToPointEnabled = true,
            Width = SliderWidth * ctx.Zoom,
            Height = 24 * ctx.Zoom,
            VerticalAlignment = VerticalAlignment.Center,
            Template = SliderTemplate(ctx),
            Foreground = ctx.Theme[ColorRole.Primary],
            IsSnapToTickEnabled = field.Decimals == 0,
            TickFrequency = field.Step,
        };

        var box = new TextBox
        {
            Width = NumericWidth * ctx.Zoom,
            Height = StyleTokens.ControlHeightSm * ctx.Zoom,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            Text = field.DisplayValueFor(scope),
            Template = FieldTemplate(ctx, StyleTokens.RadiusSm),
            Margin = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 0, 0, 0),
        };

        // Dragging repaints the read-out only; the file is written when the thumb is released,
        // so one drag cannot rebuild the bar sixty times. DragCompleted is raised by the thumb
        // inside the template, so it arrives as a routed event.
        slider.ValueChanged += (_, e) => box.Text = Format(e.NewValue, field);
        slider.AddHandler(Thumb.DragCompletedEvent, new RoutedEventHandler(
            (_, _) => Commit(field, Format(slider.Value, field), ctx)));

        void CommitTyped() => Commit(field, box.Text, ctx);

        box.LostFocus += (_, _) => CommitTyped();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitTyped();
            if (e.Key == Key.Escape) box.Text = field.DisplayValueFor(ctx.Editor.Scope());
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        }.Add(slider).Add(box);
    }

    private static FrameworkElement TextRow(SettingsField field, SettingsContext ctx)
    {
        var value = field.ValueFor(ctx.Editor.Scope());

        if (field.IsReadOnly)
        {
            // Read-only but selectable: the config path is here so a person can copy it into a
            // bug report, which is the only reason the row exists.
            return new TextBox
            {
                Text = value,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontFamily = UiFont,
                FontSize = StyleTokens.FontSizeCaption * ctx.Font,
                Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxWidth = 340 * ctx.Zoom,
                Cursor = Cursors.Arrow,
                Template = FieldTemplate(ctx, StyleTokens.RadiusSm),
            };
        }

        var box = new TextBox
        {
            Text = value,
            Width = 220 * ctx.Zoom,
            Height = StyleTokens.ControlHeight * ctx.Zoom,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = FieldTemplate(ctx, StyleTokens.RadiusMd),
        };

        if (field.Placeholder.Length > 0)
            box.ToolTip = $"for example {field.Placeholder}";

        void CommitTyped()
        {
            if (box.Text == value) return;
            if (Commit(field, box.Text, ctx) is not null) box.Text = value;
        }

        box.LostFocus += (_, _) => CommitTyped();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitTyped();
            if (e.Key == Key.Escape) box.Text = value;
        };
        return box;
    }

    /// The shortcut recorder. Clicking arms it and the button keeps focus, so the keys arrive
    /// as a chord rather than as separate clicks; Escape cancels and blur disarms, because a
    /// recorder that stays armed swallows the next thing the user means to type.
    private static FrameworkElement Keybind(SettingsField field, SettingsContext ctx)
    {
        var value = field.ValueFor(ctx.Editor.Scope());

        var label = new TextBlock
        {
            Text = value.Length == 0 ? "not bound" : value,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            FontWeight = FontWeights.Medium,
            Foreground = value.Length == 0
                ? ctx.Theme[ColorRole.OnSurfaceVariant]
                : ctx.Theme[ColorRole.OnPrimary],
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var pill = new Border
        {
            Child = label,
            MinWidth = 150 * ctx.Zoom,
            Height = StyleTokens.ControlHeight * ctx.Zoom,
            CornerRadius = new CornerRadius(StyleTokens.RadiusMd * ctx.Zoom),
            Background = value.Length == 0
                ? Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9)
                : ctx.Theme[ColorRole.Primary],
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 0, StyleTokens.SpaceSm * ctx.Zoom, 0),
        };

        var record = BareButton(pill, ctx);
        record.ToolTip = "Click, then press the shortcut you want";
        var row = Stack(Orientation.Horizontal, ctx);

        bool armed = false;

        void Disarm()
        {
            if (!armed) return;
            armed = false;
            label.Text = value.Length == 0 ? "not bound" : value;
            pill.Background = value.Length == 0
                ? Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9)
                : ctx.Theme[ColorRole.Primary];
        }

        record.Click += (_, _) =>
        {
            armed = true;
            label.Text = "press a shortcut";
            pill.Background = Alpha(ctx.Theme, ColorRole.Primary, 0.25);
            Keyboard.Focus(record);
        };

        record.PreviewKeyDown += (_, e) =>
        {
            if (!armed) return;
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                Disarm();
                return;
            }

            // Alt makes WPF report the chord as Key.System with the real key in SystemKey.
            var physical = e.Key == Key.System ? e.SystemKey : e.Key;
            string? key = KeyName(physical);
            if (key is null) return; // a modifier on its own is still the chord arriving

            var gesture = $"{Modifiers(Keyboard.Modifiers)}{key}";
            armed = false;
            Commit(field, gesture, ctx);
        };

        record.LostKeyboardFocus += (_, _) => Disarm();
        row.Children.Add(record);

        if (value.Length > 0)
        {
            var clear = GhostButton("unbind", ctx);
            clear.Click += (_, _) => Commit(field, "", ctx);
            row.Children.Add(clear);
        }

        return row;
    }

    /// The scheme cards upstream shows on the appearance page: the scheme's own name over its
    /// own surface, with four dots for the roles a bar is drawn from.
    private static FrameworkElement Schemes(SettingsField field, SettingsContext ctx)
    {
        var current = field.ValueFor(ctx.Editor.Scope());
        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            MaxWidth = 470 * ctx.Zoom,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        foreach (var choice in field.Choices)
        {
            var set = BuiltinPalettes.ByName(choice.Value);
            if (set is null) continue;

            bool selected = choice.Value == current;
            var dark = set.Dark;

            var dots = Stack(Orientation.Horizontal, ctx);
            dots.HorizontalAlignment = HorizontalAlignment.Left;
            foreach (var role in BuiltinPalettes.PreviewRoles)
            {
                dots.Children.Add(new Border
                {
                    Width = 10 * ctx.Zoom,
                    Height = 10 * ctx.Zoom,
                    CornerRadius = new CornerRadius(5 * ctx.Zoom),
                    Background = Brush(dark[role]),
                    Margin = new Thickness(0, 0, 5 * ctx.Zoom, 0),
                });
            }

            var card = Stack(Orientation.Vertical, ctx);
            card.Children.Add(new TextBlock
            {
                Text = choice.Label,
                FontFamily = UiFont,
                FontSize = StyleTokens.FontSizeMini * ctx.Font,
                FontWeight = selected ? FontWeights.Bold : FontWeights.Medium,
                Foreground = Brush(dark[ColorRole.OnSurface]),
            });
            card.Children.Add(new Border
            {
                Child = dots,
                Background = ThemeBrushes.Alpha(dark[ColorRole.Surface], 0.85),
                CornerRadius = new CornerRadius(StyleTokens.RadiusSm * ctx.Zoom),
                Padding = new Thickness(4 * ctx.Zoom, 3 * ctx.Zoom, 0, 3 * ctx.Zoom),
                Margin = new Thickness(0, 3 * ctx.Zoom, 0, 0),
            });

            var shell = new Border
            {
                Child = card,
                CornerRadius = new CornerRadius(StyleTokens.RadiusMd * ctx.Zoom),
                Background = ThemeBrushes.Alpha(dark[ColorRole.SurfaceVariant], 0.95),
                BorderBrush = selected ? ctx.Theme[ColorRole.Primary] : ctx.Theme[ColorRole.Outline],
                BorderThickness = new Thickness(selected ? 2 : 1),
                Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 5 * ctx.Zoom,
                    StyleTokens.SpaceSm * ctx.Zoom, 5 * ctx.Zoom),
                Margin = new Thickness(0, 0, StyleTokens.SpaceXs * ctx.Zoom, StyleTokens.SpaceXs * ctx.Zoom),
            };

            var button = BareButton(shell, ctx);
            button.ToolTip = $"{choice.Label}: primary {dark[ColorRole.Primary].ToHex()} on surface {dark[ColorRole.Surface].ToHex()}";
            button.Click += (_, _) => Commit(field, choice.Value, ctx);
            wrap.Children.Add(button);
        }

        return wrap;
    }

    private static FrameworkElement Lane(SettingsField field, SettingsContext ctx)
    {
        var names = SettingsField.Split(field.ValueFor(ctx.Editor.Scope()));
        var card = Stack(Orientation.Vertical, ctx);
        card.MinWidth = 320 * ctx.Zoom;

        for (int i = 0; i < names.Count; i++)
            card.Children.Add(LaneChip(field, names, i, ctx));

        if (names.Count == 0)
        {
            card.Children.Add(new TextBlock
            {
                Text = "nothing in this lane",
                FontFamily = UiFont,
                FontSize = StyleTokens.FontSizeCaption * ctx.Font,
                Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
                Margin = new Thickness(2 * ctx.Zoom, 2 * ctx.Zoom, 0, StyleTokens.SpaceXs * ctx.Zoom),
            });
        }

        card.Children.Add(AddWidgetRow(field, names, ctx));
        return card;
    }

    private static FrameworkElement LaneChip(
        SettingsField field, List<string> names, int index, SettingsContext ctx)
    {
        string name = names[index];
        bool supported = WidgetCatalog.IsSupported(name);

        var row = Stack(Orientation.Horizontal, ctx);
        row.HorizontalAlignment = HorizontalAlignment.Left;
        row.Children.Add(new TextBlock
        {
            Text = WidgetLabel(name),
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            Foreground = supported
                ? ctx.Theme[ColorRole.OnSurface]
                : ctx.Theme[ColorRole.OnSurfaceVariant],
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 160 * ctx.Zoom,
        });

        if (!supported)
        {
            row.Children.Add(Badge(WidgetCatalog.IsKnown(name) ? "not on Windows yet" : "unknown",
                Alpha(ctx.Theme, ColorRole.OnSurfaceVariant, 0.12),
                ctx.Theme[ColorRole.OnSurfaceVariant], ctx));
        }

        if (index > 0)
        {
            var up = TinyButton("↑", ctx);
            up.ToolTip = "Move up";
            up.Click += (_, _) => Swap(field, names, index, index - 1, ctx);
            row.Children.Add(up);
        }

        if (index < names.Count - 1)
        {
            var down = TinyButton("↓", ctx);
            down.ToolTip = "Move down";
            down.Click += (_, _) => Swap(field, names, index, index + 1, ctx);
            row.Children.Add(down);
        }

        var remove = TinyButton("×", ctx);
        remove.ToolTip = $"Remove {WidgetLabel(name)} from this lane";
        remove.Click += (_, _) =>
        {
            var next = new List<string>(names);
            next.RemoveAt(index);
            Commit(field, string.Join(',', next), ctx);
        };
        row.Children.Add(remove);

        return new Border
        {
            Child = row,
            Padding = new Thickness(2 * ctx.Zoom),
        };
    }

    private static FrameworkElement AddWidgetRow(SettingsField field, List<string> names, SettingsContext ctx)
    {
        var choices = WidgetCatalog.Supported
            .Order(StringComparer.Ordinal)
            .Select(n => new SettingsChoice(n, WidgetLabel(n)))
            .ToList();

        var picker = DropdownWith(choices, "", _ => { }, ctx,
            StyleTokens.ControlHeightSm * ctx.Zoom, 170 * ctx.Zoom);
        picker.SelectedIndex = -1;

        var add = GhostButton("add widget", ctx);
        add.Click += (_, _) =>
        {
            if (picker.SelectedItem is not ComboBoxItem item)
            {
                ctx.Notice("Choose a widget to add first.");
                return;
            }

            var next = new List<string>(names) { (string)item.Tag };
            Commit(field, string.Join(',', next), ctx);
        };

        var row = Stack(Orientation.Horizontal, ctx);
        row.Margin = new Thickness(0, StyleTokens.SpaceXs * ctx.Zoom, 0, 0);
        row.Children.Add(picker);
        row.Children.Add(add);
        return row;
    }

    private static FrameworkElement PushButton(SettingsField field, SettingsContext ctx)
    {
        var destructive = field.Id == SettingsCatalog.RestoreDefaults;
        var button = GhostButton(destructive ? "restore all defaults" : "run", ctx);
        button.ToolTip = field.Description;
        if (destructive)
        {
            button.Foreground = ctx.Theme[ColorRole.Error];
            button.BorderBrush = ctx.Theme[ColorRole.Error];
        }

        button.Click += (_, _) => ctx.Notice(ctx.RunAction(field.Action ?? string.Empty));
        return button;
    }

    private static FrameworkElement ResetButton(SettingsField field, SettingsContext ctx)
    {
        bool pending = false;
        var label = new TextBlock
        {
            Text = "reset",
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeMini * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
        };

        var button = BareButton(new Border
        {
            Child = label,
            CornerRadius = new CornerRadius(StyleTokens.RadiusSm * ctx.Zoom),
            Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 3 * ctx.Zoom,
                StyleTokens.SpaceSm * ctx.Zoom, 3 * ctx.Zoom),
            Background = Brushes.Transparent,
        }, ctx);

        // Upstream asks twice. The first click arms the row and colours it with the palette's
        // error role; the second writes. A settings window that reset a value on a stray click
        // would be worse than one that asks.
        button.Click += (_, _) =>
        {
            if (!pending)
            {
                pending = true;
                label.Text = "reset?";
                label.Foreground = ctx.Theme[ColorRole.Error];
                return;
            }

            ctx.Notice(ctx.Editor.ResetField(field));
            ctx.Committed();
        };
        return button;
    }

    // ── shared pieces ────────────────────────────────────────────────────────

    private static StackPanel Stack(Orientation orientation, SettingsContext ctx) => new()
    {
        Orientation = orientation,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Swap(SettingsField field, List<string> names, int from, int to, SettingsContext ctx)
    {
        var next = new List<string>(names);
        (next[from], next[to]) = (next[to], next[from]);
        Commit(field, string.Join(',', next), ctx);
    }

    private static string WidgetLabel(string widget) => widget switch
    {
        "control-center" => "Quick settings",
        "keyboard-layout" => "Keyboard layout",
        "active-window" => "Active window",
        "idle-inhibitor" => "Idle inhibitor",
        _ => string.Join(' ', widget.Split('-').Select(word =>
            word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..])),
    };

    private static string Format(double value, SettingsField field) =>
        value.ToString("F" + field.Decimals, CultureInfo.InvariantCulture);

    /// Writes, reports, and repaints from the store. Returns the error so a caller can undo
    /// what it just typed.
    private static string? Commit(SettingsField field, string value, SettingsContext ctx)
    {
        var error = ctx.Editor.Apply(field, value);
        ctx.Notice(error);
        ctx.Committed();
        return error;
    }

    private static SolidColorBrush Alpha(ThemeBrushes theme, ColorRole role, double opacity) =>
        ThemeBrushes.Alpha(theme.Palette[role], opacity);

    private static SolidColorBrush Brush(Rgb rgb) =>
        ThemeBrushes.Freeze(Color.FromRgb(rgb.R, rgb.G, rgb.B));

    /// The gesture names the ported parser accepts, in the spelling it wants. A key Windows
    /// gave us that the parser cannot register comes back as null, so the recorder never
    /// writes a binding the hotkey service would refuse.
    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F24 => key.ToString(),
        Key.Space => "SPACE",
        Key.Enter or Key.Return => "ENTER",
        Key.Tab => "TAB",
        Key.Back => "BACKSPACE",
        Key.Insert => "INSERT",
        Key.Delete => "DELETE",
        Key.Home => "HOME",
        Key.End => "END",
        Key.PageUp => "PAGEUP",
        Key.PageDown => "PAGEDOWN",
        Key.Left => "LEFT",
        Key.Up => "UP",
        Key.Right => "RIGHT",
        Key.Down => "DOWN",
        Key.NumPad0 => "0",
        _ => null,
    };

    private static string Modifiers(ModifierKeys modifiers)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("CTRL");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("ALT");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("SHIFT");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("SUPER");
        return parts.Count == 0 ? string.Empty : string.Join('+', parts) + "+";
    }

    private static BadgeView Badge(string text, Brush background, Brush foreground, SettingsContext ctx) =>
        new(text, background, foreground, StyleTokens.FontSizeMini * ctx.Font)
        {
            Margin = new Thickness(0, 2 * ctx.Zoom, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

    /// A button with no chrome of its own: the Border it wraps is already the palette's shape,
    /// and WPF's default template would draw a gradient card over it.
    private static Button BareButton(Border content, SettingsContext ctx)
    {
        var button = new Button
        {
            Content = content,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            FontFamily = UiFont,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Template = ChromelessTemplate();
        return button;
    }

    private static ControlTemplate ChromelessTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static Button GhostButton(string text, SettingsContext ctx)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeCaption * ctx.Font,
            FontWeight = FontWeights.Medium,
            Foreground = ctx.Theme[ColorRole.OnSurface],
            Background = Alpha(ctx.Theme, ColorRole.SurfaceVariant, 0.9),
            BorderBrush = ctx.Theme[ColorRole.Outline],
            BorderThickness = new Thickness(1),
            Height = StyleTokens.ControlHeightSm * ctx.Zoom,
            Padding = new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 0, StyleTokens.SpaceSm * ctx.Zoom, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(StyleTokens.SpaceXs * ctx.Zoom, 0, 0, 0),
        };
        button.Template = OutlinedTemplate(ctx, StyleTokens.RadiusMd);
        return button;
    }

    private static Button TinyButton(string glyph, SettingsContext ctx)
    {
        var button = new Button
        {
            Content = glyph,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody * ctx.Font,
            Foreground = ctx.Theme[ColorRole.OnSurfaceVariant],
            Background = Brushes.Transparent,
            BorderBrush = Alpha(ctx.Theme, ColorRole.Outline, 0.7),
            BorderThickness = new Thickness(1),
            Width = 26 * ctx.Zoom,
            Height = 26 * ctx.Zoom,
            Margin = new Thickness(2 * ctx.Zoom, 0, 0, 0),
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Template = OutlinedTemplate(ctx, StyleTokens.RadiusSm);
        return button;
    }

    private static ControlTemplate OutlinedTemplate(SettingsContext ctx, float radius)
    {
        var border = new FrameworkElementFactory(typeof(Border), "root");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius * ctx.Zoom));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, ctx.HoverFill()) { TargetName = "root" });
        template.Triggers.Add(hover);
        return template;
    }

    private static ControlTemplate FieldTemplate(SettingsContext ctx, float radius)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius * ctx.Zoom));
        border.SetValue(Border.PaddingProperty,
            new Thickness(StyleTokens.SpaceSm * ctx.Zoom, 0, StyleTokens.SpaceSm * ctx.Zoom, 0));

        // TextBox finds its editor by this exact name, which is what registers it in the
        // template's namescope.
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(FrameworkElement.FocusableProperty, false);
        host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        border.AppendChild(host);

        return new ControlTemplate(typeof(TextBox)) { VisualTree = border };
    }

    // ── the two templates that have to be XAML ───────────────────────────────
    //
    // Popup.Child and Track.Thumb are plain CLR properties, not dependency properties, so a
    // FrameworkElementFactory cannot express them and the factory API has no other setter.
    // These two templates are therefore parsed from XAML. Every colour in them comes from the
    // palette, which is the whole reason the default templates were replaced.

    private const string PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string ControlsNs = "clr-namespace:System.Windows.Controls;assembly=PresentationFramework";

    private static ControlTemplate Parse(string xaml) => (ControlTemplate)XamlReader.Parse(xaml);

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Hex(SettingsContext ctx, ColorRole role, double opacity = 1)
    {
        var color = ctx.Theme.Palette[role];
        if (opacity >= 1) return color.ToHex();
        var a = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return $"#{a:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
    }

    private static ControlTemplate DropdownTemplate(SettingsContext ctx)
    {
        double radius = StyleTokens.RadiusMd * ctx.Zoom;
        double textPadding = StyleTokens.SpaceSm * ctx.Zoom;
        double maxHeight = 280 * ctx.Zoom;

        return Parse($$"""
            <ControlTemplate TargetType="ComboBox"
                             xmlns="{{PresentationNs}}"
                             xmlns:x="{{XamlNs}}"
                             xmlns:sc="{{ControlsNs}}">
              <Grid>
                <!-- Above the toggle, which paints the field's own background: in document
                     order the first child ends up underneath, and the label reads as ghosted. -->
                <ContentPresenter Panel.ZIndex="1"
                                  VerticalAlignment="Center"
                                  Margin="{{Num(textPadding)}},0,{{Num(textPadding * 1.5)}},0"
                                  IsHitTestVisible="False"
                                  Content="{TemplateBinding SelectionBoxItem}"
                                  ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/>
                <ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press"
                              Background="Transparent"
                              IsChecked="{TemplateBinding IsDropDownOpen}">
                  <ToggleButton.Template>
                    <ControlTemplate TargetType="ToggleButton">
                      <Border x:Name="box"
                              Background="{{Hex(ctx, ColorRole.SurfaceVariant, 0.94)}}"
                              BorderBrush="{{Hex(ctx, ColorRole.Outline, 0.8)}}"
                              BorderThickness="1"
                              CornerRadius="{{Num(radius)}}"/>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="box" Property="Background" Value="{{Hex(ctx, ColorRole.Hover, 0.3)}}"/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </ToggleButton.Template>
                </ToggleButton>
                <Popup x:Name="PART_Popup" AllowsTransparency="True" Placement="Bottom"
                       StaysOpen="False" VerticalOffset="2"
                       MinWidth="{TemplateBinding ActualWidth}"
                       IsOpen="{TemplateBinding IsDropDownOpen}">
                  <Border Background="{{Hex(ctx, ColorRole.SurfaceVariant)}}"
                          BorderBrush="{{Hex(ctx, ColorRole.Outline, 0.9)}}"
                          BorderThickness="1"
                          CornerRadius="{{Num(radius)}}"
                          Padding="2">
                    <ScrollViewer MaxHeight="{{Num(maxHeight)}}" VerticalScrollBarVisibility="Auto">
                      <ItemsPresenter/>
                    </ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
            </ControlTemplate>
            """);
    }

    /// The popup rows. WPF's default item template highlights with a system brush that belongs
    /// to no palette, so the highlight is drawn here instead.
    private static ControlTemplate ItemTemplate(SettingsContext ctx)
    {
        double radius = StyleTokens.RadiusSm * ctx.Zoom;
        double pad = StyleTokens.SpaceSm * ctx.Zoom;

        return Parse($$"""
            <ControlTemplate TargetType="ComboBoxItem"
                             xmlns="{{PresentationNs}}"
                             xmlns:x="{{XamlNs}}">
              <Border x:Name="box"
                      Background="{TemplateBinding Background}"
                      CornerRadius="{{Num(radius)}}"
                      Padding="{{Num(pad)}},5,{{Num(pad)}},5">
                <ContentPresenter/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsHighlighted" Value="True">
                  <Setter TargetName="box" Property="Background" Value="{{Hex(ctx, ColorRole.Hover, 0.4)}}"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
    }

    /// The slider's own drawing: an 8px track of the palette's outline, the part before the
    /// thumb filled with primary, and a thumb of on-primary with an outline rim. WPF's default
    /// slider paints system chrome that no palette reaches.
    private static ControlTemplate SliderTemplate(SettingsContext ctx)
    {
        double groove = 8 * ctx.Zoom;
        double thumb = 18 * ctx.Zoom;
        string outline = Hex(ctx, ColorRole.Outline, 0.85);

        return Parse($$"""
            <ControlTemplate TargetType="Slider"
                             xmlns="{{PresentationNs}}"
                             xmlns:x="{{XamlNs}}"
                             xmlns:sc="{{ControlsNs}}">
              <Grid>
                <Border Height="{{Num(groove)}}" CornerRadius="{{Num(groove / 2)}}"
                        Background="{{outline}}" VerticalAlignment="Center"/>
                <Track x:Name="PART_Track" VerticalAlignment="Center">
                  <Track.Thumb>
                    <Thumb Width="{{Num(thumb)}}" Height="{{Num(thumb)}}">
                      <Thumb.Template>
                        <ControlTemplate TargetType="Thumb">
                          <Border CornerRadius="{{Num(thumb / 2)}}"
                                  Background="{{Hex(ctx, ColorRole.OnPrimary)}}"
                                  BorderBrush="{{outline}}" BorderThickness="1"/>
                        </ControlTemplate>
                      </Thumb.Template>
                    </Thumb>
                  </Track.Thumb>
                  <Track.DecreaseRepeatButton>
                    <RepeatButton Focusable="False" Command="{x:Static sc:Slider.DecreaseLarge}">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton">
                          <Border Height="{{Num(groove)}}" CornerRadius="{{Num(groove / 2)}}"
                                  Background="{{Hex(ctx, ColorRole.Primary)}}"
                                  VerticalAlignment="Center"/>
                        </ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.DecreaseRepeatButton>
                  <Track.IncreaseRepeatButton>
                    <RepeatButton Focusable="False" Command="{x:Static sc:Slider.IncreaseLarge}">
                      <RepeatButton.Template>
                        <ControlTemplate TargetType="RepeatButton">
                          <Border Height="{{Num(groove)}}" CornerRadius="{{Num(groove / 2)}}"
                                  Background="Transparent"
                                  VerticalAlignment="Center"/>
                        </ControlTemplate>
                      </RepeatButton.Template>
                    </RepeatButton>
                  </Track.IncreaseRepeatButton>
                </Track>
              </Grid>
            </ControlTemplate>
            """);
    }
}

internal static class ControlExtensions
{
    /// The palette's hover capsule, kept partly transparent so the surface behind still reads.
    public static SolidColorBrush HoverFill(this SettingsContext ctx) =>
        ThemeBrushes.Alpha(ctx.Theme.Palette[ColorRole.Hover], 0.35);

    public static StackPanel Add(this StackPanel panel, UIElement child)
    {
        panel.Children.Add(child);
        return panel;
    }
}

/// The small rounded word tag upstream puts beside a row: an override hint, an advanced marker.
internal sealed class BadgeView : Border
{
    public BadgeView(string text, Brush background, Brush foreground, double fontSize)
    {
        CornerRadius = new CornerRadius(StyleTokens.RadiusSm);
        Padding = new Thickness(6, 2, 6, 2);
        VerticalAlignment = VerticalAlignment.Center;
        Background = background;
        Child = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = fontSize,
            FontWeight = FontWeights.Medium,
            Foreground = foreground,
        };
    }
}
