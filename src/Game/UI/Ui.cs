using System;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Small UI kit: palette, fonts and factory helpers. Visual language: dark slate
    /// translucent panels, flat blocks, uppercase headings, a single warm accent and a
    /// green primary action — functional layout in the spirit of modern tactical-shooter
    /// menus, with original styling.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Bg = new Color(0.035f, 0.045f, 0.06f, 1f);
        public static readonly Color Bar = new Color(0.06f, 0.075f, 0.095f, 0.92f);
        public static readonly Color Panel = new Color(0.075f, 0.09f, 0.115f, 0.9f);
        public static readonly Color PanelLight = new Color(0.12f, 0.145f, 0.18f, 0.92f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Text = new Color(0.9f, 0.93f, 0.96f);
        public static readonly Color Muted = new Color(0.55f, 0.6f, 0.67f);
        public static readonly Color Accent = new Color(0.95f, 0.66f, 0.23f);
        public static readonly Color Go = new Color(0.33f, 0.69f, 0.31f);
        public static readonly Color GoHover = new Color(0.4f, 0.78f, 0.37f);
        public static readonly Color Selected = new Color(0.2f, 0.25f, 0.31f, 0.95f);
        public static readonly Color Danger = new Color(0.9f, 0.32f, 0.28f);

        private static FontVariation _bold;
        private static FontVariation _wide;

        public static Font Regular
        {
            get { return ThemeDB.FallbackFont; }
        }

        public static Font Bold
        {
            get
            {
                if (_bold == null) _bold = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.7f };
                return _bold;
            }
        }

        /// <summary>Bold with extra letter spacing — for uppercase headings and tabs.</summary>
        public static Font Wide
        {
            get
            {
                if (_wide == null)
                {
                    _wide = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.8f };
                    _wide.SetSpacing(TextServer.SpacingType.Glyph, 2);
                }
                return _wide;
            }
        }

        public static StyleBoxFlat Box(Color bg, int radius = 4, Color? border = null, int borderWidth = 0, float padH = 14f, float padV = 8f)
        {
            var sb = new StyleBoxFlat { BgColor = bg };
            sb.SetCornerRadiusAll(radius);
            if (border.HasValue && borderWidth > 0)
            {
                sb.BorderColor = border.Value;
                sb.SetBorderWidthAll(borderWidth);
            }
            sb.ContentMarginLeft = padH;
            sb.ContentMarginRight = padH;
            sb.ContentMarginTop = padV;
            sb.ContentMarginBottom = padV;
            return sb;
        }

        public static Label Label(string text, int size, Color color, Font font = null, HorizontalAlignment align = HorizontalAlignment.Left)
        {
            var l = new Label
            {
                Text = text,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            l.LabelSettings = new LabelSettings
            {
                Font = font ?? Regular,
                FontSize = size,
                FontColor = color,
                OutlineSize = 0,
                ShadowColor = new Color(0f, 0f, 0f, 0.5f),
                ShadowSize = 2,
                ShadowOffset = new Vector2(1f, 1f)
            };
            return l;
        }

        public static void SetLabelColor(Label label, Color color)
        {
            if (label.LabelSettings != null) label.LabelSettings.FontColor = color;
        }

        /// <summary>Flat button. <paramref name="bg"/> idle, highlighted on hover/press.</summary>
        public static Button Button(string text, int size, Color bg, Color fg, Action onPressed, Font font = null, int radius = 3)
        {
            var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, ClipText = false };
            b.AddThemeFontOverride("font", font ?? Bold);
            b.AddThemeFontSizeOverride("font_size", size);
            b.AddThemeColorOverride("font_color", fg);
            b.AddThemeColorOverride("font_hover_color", Colors.White);
            b.AddThemeColorOverride("font_pressed_color", Colors.White);
            b.AddThemeStyleboxOverride("normal", Box(bg, radius));
            b.AddThemeStyleboxOverride("hover", Box(bg.Lightened(0.12f), radius));
            b.AddThemeStyleboxOverride("pressed", Box(bg.Lightened(0.22f), radius));
            b.AddThemeStyleboxOverride("disabled", Box(new Color(bg.R, bg.G, bg.B, bg.A * 0.4f), radius));
            b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            b.AddThemeColorOverride("font_disabled_color", new Color(fg.R, fg.G, fg.B, 0.4f));
            if (onPressed != null)
            {
                b.Pressed += () =>
                {
                    GameContext.App?.PlayUi("ui_click");
                    onPressed();
                };
            }
            return b;
        }

        public static void SetButtonColors(Button b, Color bg, Color fg, int radius = 3)
        {
            b.AddThemeColorOverride("font_color", fg);
            b.AddThemeStyleboxOverride("normal", Box(bg, radius));
            b.AddThemeStyleboxOverride("hover", Box(bg.Lightened(0.12f), radius));
            b.AddThemeStyleboxOverride("pressed", Box(bg.Lightened(0.22f), radius));
        }

        public static ColorRect Rect(Color color)
        {
            return new ColorRect { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };
        }

        public static PanelContainer PanelBox(Color color, int radius = 4, float pad = 16f)
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Box(color, radius, null, 0, pad, pad));
            return p;
        }

        /// <summary>Anchors a control to fractions of its parent with pixel offsets.</summary>
        public static T Place<T>(T c, float left, float top, float right, float bottom,
            float offLeft = 0f, float offTop = 0f, float offRight = 0f, float offBottom = 0f) where T : Control
        {
            c.AnchorLeft = left;
            c.AnchorTop = top;
            c.AnchorRight = right;
            c.AnchorBottom = bottom;
            c.OffsetLeft = offLeft;
            c.OffsetTop = offTop;
            c.OffsetRight = offRight;
            c.OffsetBottom = offBottom;
            return c;
        }

        public static T Fill<T>(T c) where T : Control
        {
            return Place(c, 0f, 0f, 1f, 1f);
        }

        public static HBoxContainer HBox(int separation)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", separation);
            return h;
        }

        public static VBoxContainer VBox(int separation)
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", separation);
            return v;
        }

        public static Control Spacer(float w = 0f, float h = 0f, bool expand = false)
        {
            var c = new Control { CustomMinimumSize = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
            if (expand)
            {
                c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            }
            return c;
        }
    }
}
