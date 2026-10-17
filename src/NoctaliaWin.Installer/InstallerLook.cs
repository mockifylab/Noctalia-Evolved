// The installer's visual language: Noctalia's charcoal-and-mint palette, the same radii and
// spacing tokens the bar uses, and a calm layout — one idea on screen, generous margins, a single
// accented action. Nothing here is copied from Apple: no marks, no wording, no product graphics.
// The tile is our own crescent, drawn from two circles in the palette's own colours.
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using NoctaliaWin.Core.Design;

namespace NoctaliaWin.Installer;

internal sealed class InstallerLook
{
    public static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");

    /// The one size the tokens do not carry: a wizard's opening line is larger than the bar's
    /// header, and this is the only surface that needs it.
    public const double DisplaySize = 27.0;

    private readonly Palette _palette;

    public InstallerLook(Palette palette) => _palette = palette;

    public SolidColorBrush Ink(ColorRole role, double opacity = 1)
    {
        Rgb rgb = _palette[role];
        var color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        if (opacity < 1) color.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

        return new SolidColorBrush(color);
    }

    public string Hex(ColorRole role, double opacity = 1)
    {
        Rgb rgb = _palette[role];
        if (opacity >= 1) return rgb.ToHex();

        int a = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return $"#{a:x2}{rgb.R:x2}{rgb.G:x2}{rgb.B:x2}";
    }

    public TextBlock Display(string text) =>
        Text(text, DisplaySize, FontWeights.SemiBold, ColorRole.OnSurface,
            new Thickness(0, 0, 0, StyleTokens.SpaceSm));

    public TextBlock Lead(string text) =>
        Text(text, StyleTokens.FontSizeBody, FontWeights.Normal, ColorRole.OnSurfaceVariant,
            new Thickness(0, 0, 0, StyleTokens.SpaceLg));

    public TextBlock Body(string text) =>
        Text(text, StyleTokens.FontSizeCaption, FontWeights.Normal, ColorRole.OnSurface,
            new Thickness(0, 0, 0, StyleTokens.SpaceSm));

    public TextBlock Caption(string text) =>
        Text(text, StyleTokens.FontSizeMini, FontWeights.Normal, ColorRole.OnSurfaceVariant,
            new Thickness(0, 0, 0, StyleTokens.SpaceXs));

    /// A path the person may want to read carefully, or copy out.
    public TextBlock PathText(string text, ColorRole role = ColorRole.OnSurface) => new()
    {
        Text = text,
        FontFamily = new FontFamily("Segoe UI Mono, Consolas"),
        FontSize = StyleTokens.FontSizeMini,
        Foreground = Ink(role),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, StyleTokens.SpaceXs),
    };

    public TextBlock Named(string label, string value, ColorRole role = ColorRole.OnSurface) => new()
    {
        FontSize = StyleTokens.FontSizeCaption,
        Foreground = Ink(role),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, StyleTokens.SpaceXs),
        Inlines =
        {
            new Run(label + "  ") { FontWeight = FontWeights.SemiBold },
            new Run(value),
        },
    };

    public Border Card(params UIElement[] rows)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        foreach (UIElement row in rows) panel.Children.Add(row);

        return new Border
        {
            CornerRadius = new CornerRadius(StyleTokens.RadiusLg),
            Background = Ink(ColorRole.SurfaceVariant, 0.72),
            BorderBrush = Ink(ColorRole.Outline, 0.55),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            Padding = new Thickness(StyleTokens.SpaceLg),
            Margin = new Thickness(0, 0, 0, StyleTokens.SpaceMd),
            Child = panel,
        };
    }

    public Button Primary(string text)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink(ColorRole.OnTertiary),
            Background = Ink(ColorRole.Tertiary),
            BorderThickness = new Thickness(0),
            Height = StyleTokens.ControlHeight,
            MinWidth = 132,
            Padding = new Thickness(StyleTokens.SpaceLg, 0, StyleTokens.SpaceLg, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Template = ButtonTemplate(StyleTokens.RadiusMd, Ink(ColorRole.Hover));
        return button;
    }

    public Button Ghost(string text)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontSize = StyleTokens.FontSizeBody,
            Foreground = Ink(ColorRole.OnSurface),
            Background = Brushes.Transparent,
            BorderBrush = Ink(ColorRole.Outline, 0.7),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            Height = StyleTokens.ControlHeight,
            MinWidth = 108,
            Padding = new Thickness(StyleTokens.SpaceMd, 0, StyleTokens.SpaceMd, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        button.Template = ButtonTemplate(StyleTokens.RadiusMd, Ink(ColorRole.Hover, 0.45));
        return button;
    }

    /// A switch row rather than a tickbox: one option per row, the label beside it, and the whole
    /// row is clickable.
    public CheckBox Toggle(string label, string detail, bool on)
    {
        var text = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, StyleTokens.SpaceMd, 0) };
        text.Children.Add(Text(label, StyleTokens.FontSizeBody, FontWeights.Medium, ColorRole.OnSurface,
            new Thickness(0, 0, 0, 2)));
        text.Children.Add(Text(detail, StyleTokens.FontSizeMini, FontWeights.Normal, ColorRole.OnSurfaceVariant,
            new Thickness(0)));

        var box = new CheckBox
        {
            IsChecked = on,
            Content = text,
            Foreground = Ink(ColorRole.OnSurface),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.Template = ToggleTemplate();
        return box;
    }

    /// The crescent tile. Two circles, one taken out of the other, in the palette's mint over a
    /// charcoal-to-dark-green field — drawn here rather than shipped as an image, so there is no
    /// asset to license and no blurry scale.
    public FrameworkElement Tile(double size)
    {
        var geometry = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new EllipseGeometry(new Point(size * 0.5, size * 0.5), size * 0.34, size * 0.34),
            new EllipseGeometry(new Point(size * 0.62, size * 0.40), size * 0.30, size * 0.30));

        var moon = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Fill = Ink(ColorRole.Tertiary),
        };

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.24),
            Background = new LinearGradientBrush(
                Color.FromRgb(0x11, 0x11, 0x2d), Color.FromRgb(0x07, 0x07, 0x22), 90.0),
            BorderBrush = Ink(ColorRole.Outline, 0.6),
            BorderThickness = new Thickness(StyleTokens.BorderWidth),
            Child = new Grid { Children = { moon } },
            Margin = new Thickness(0, 0, 0, StyleTokens.SpaceLg),
        };
    }

    private TextBlock Text(string text, double size, FontWeight weight, ColorRole role, Thickness margin) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Ink(role),
        TextWrapping = TextWrapping.Wrap,
        Margin = margin,
    };

    private ControlTemplate ButtonTemplate(double radius, Brush hover)
    {
        var border = new FrameworkElementFactory(typeof(Border), "root");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };

        var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, hover) { TargetName = "root" });
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85));

        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(new Setter(Border.BackgroundProperty, hover) { TargetName = "root" });

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, (double)StyleTokens.DisabledOutlineAlpha));

        // Order matters: a pressed or disabled button should not be re-lit by the hover trigger.
        template.Triggers.Add(pressed);
        template.Triggers.Add(over);
        template.Triggers.Add(disabled);
        return template;
    }

    private ControlTemplate ToggleTemplate()
    {
        double width = 44, height = 26, thumb = 20, travel = width - thumb - 6;

        return (ControlTemplate)XamlReader.Parse($$"""
            <ControlTemplate TargetType="CheckBox"
                             xmlns="{{PresentationNs}}"
                             xmlns:x="{{XamlNs}}">
              <Grid Background="Transparent">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <Border Grid.Column="0" VerticalAlignment="Center" Width="{{Num(width)}}" Height="{{Num(height)}}">
                  <Grid>
                    <Border x:Name="track"
                            CornerRadius="{{Num(height / 2)}}"
                            Background="{{Hex(ColorRole.SurfaceVariant, 0.95)}}"
                            BorderBrush="{{Hex(ColorRole.Outline, 0.8)}}"
                            BorderThickness="1"/>
                    <Canvas Width="{{Num(width)}}" Height="{{Num(height)}}">
                      <Ellipse x:Name="thumb"
                               Canvas.Left="3"
                               Canvas.Top="{{Num((height - thumb) / 2)}}"
                               Width="{{Num(thumb)}}" Height="{{Num(thumb)}}"
                               Fill="{{Hex(ColorRole.OnSurfaceVariant)}}"/>
                    </Canvas>
                  </Grid>
                </Border>

                <ContentPresenter Grid.Column="1"
                                  VerticalAlignment="Center"
                                  Margin="{{Num(StyleTokens.SpaceLg)}},0,0,0"
                                  RecognizesAccessKey="True"/>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True">
                  <Setter TargetName="track" Property="Background" Value="{{Hex(ColorRole.Tertiary)}}"/>
                  <Setter TargetName="track" Property="BorderBrush" Value="{{Hex(ColorRole.Tertiary)}}"/>
                  <Setter TargetName="thumb" Property="Canvas.Left" Value="{{Num(3 + travel)}}"/>
                  <Setter TargetName="thumb" Property="Fill" Value="{{Hex(ColorRole.OnTertiary)}}"/>
                </Trigger>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="track" Property="Opacity" Value="0.88"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
    }

    private const string PresentationNs = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
