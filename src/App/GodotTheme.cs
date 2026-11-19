using Godot;

namespace Backrooms.Infinity.App;

public static class GodotTheme
{
    public static readonly Color Ink = new(0.906f, 0.882f, 0.769f);
    public static readonly Color Accent = new(0.784f, 0.749f, 0.447f);
    public static readonly Color Good = new(0.451f, 0.851f, 0.545f);
    public static readonly Color Warning = new(0.902f, 0.741f, 0.384f);
    public static readonly Color Error = new(0.937f, 0.451f, 0.451f);
    public static readonly Color Panel = new(0.035f, 0.035f, 0.028f, 0.90f);
    public static readonly Color PanelSoft = new(0.07f, 0.07f, 0.055f, 0.88f);

    private static FontFile? _font;
    public static FontFile? VhsFont => _font ??= GD.Load<FontFile>("res://assets/common/vcrosdmonorus_vhsicons.ttf");

    public static void Apply(Control control, int size = 22)
    {
        if (VhsFont is not null)
            control.AddThemeFontOverride("font", VhsFont);
        control.AddThemeFontSizeOverride("font_size", size);
    }

    public static StyleBoxFlat PanelStyle(Color? color = null, int radius = 8, int border = 1)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color ?? Panel,
            BorderColor = new Color(0.68f, 0.64f, 0.40f, 0.42f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            BorderWidthLeft = border,
            BorderWidthTop = border,
            BorderWidthRight = border,
            BorderWidthBottom = border,
            ContentMarginLeft = 16,
            ContentMarginTop = 12,
            ContentMarginRight = 16,
            ContentMarginBottom = 12
        };
        return style;
    }

    public static Button Button(string text, int width = 360)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 52),
            FocusMode = Control.FocusModeEnum.All
        };
        Apply(button, 24);
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.96f, 0.72f));
        button.AddThemeStyleboxOverride("normal", PanelStyle(new Color(0.035f, 0.035f, 0.028f, 0.82f), 5));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color(0.15f, 0.14f, 0.08f, 0.94f), 5));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color(0.22f, 0.20f, 0.10f, 0.98f), 5));
        return button;
    }

    public static Label Label(string text, int size = 22, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align };
        Apply(label, size);
        label.AddThemeColorOverride("font_color", Ink);
        return label;
    }

    public static void FullRect(Control c)
    {
        c.AnchorLeft = 0;
        c.AnchorTop = 0;
        c.AnchorRight = 1;
        c.AnchorBottom = 1;
        c.OffsetLeft = c.OffsetTop = c.OffsetRight = c.OffsetBottom = 0;
    }
}
