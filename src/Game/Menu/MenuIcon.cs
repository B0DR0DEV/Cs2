using Godot;

namespace StrikeZone.Game
{
    public enum IconKind
    {
        Power,
        Gear,
        Play,
        People,
        Plus,
        Lock,
        Check,
        Globe,
        Crate,
        Coin,
        Gem
    }

    /// <summary>Vector icons drawn in code (no icon fonts or image assets needed).</summary>
    public partial class MenuIcon : Control
    {
        public IconKind Kind;
        public Color Tint = Colors.White;

        public static MenuIcon Create(IconKind kind, float size, Color tint)
        {
            return new MenuIcon
            {
                Kind = kind,
                Tint = tint,
                CustomMinimumSize = new Vector2(size, size),
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
        }

        public override void _Draw()
        {
            float s = Mathf.Min(Size.X, Size.Y);
            Vector2 c = Size * 0.5f;
            float r = s * 0.42f;
            float w = Mathf.Max(2f, s * 0.09f);
            switch (Kind)
            {
                case IconKind.Power:
                    DrawArc(c, r * 0.85f, Mathf.DegToRad(-60f), Mathf.DegToRad(240f), 32, Tint, w, true);
                    DrawLine(c + new Vector2(0f, -r), c + new Vector2(0f, -r * 0.15f), Tint, w, true);
                    break;
                case IconKind.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = Mathf.Tau * i / 8f;
                        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        DrawLine(c + d * r * 0.6f, c + d * r, Tint, w * 1.5f, true);
                    }
                    DrawArc(c, r * 0.62f, 0f, Mathf.Tau, 32, Tint, w, true);
                    DrawArc(c, r * 0.25f, 0f, Mathf.Tau, 20, Tint, w, true);
                    break;
                case IconKind.Play:
                    DrawColoredPolygon(new[] { c + new Vector2(-r * 0.6f, -r * 0.8f), c + new Vector2(r * 0.85f, 0f), c + new Vector2(-r * 0.6f, r * 0.8f) }, Tint);
                    break;
                case IconKind.People:
                    DrawCircle(c + new Vector2(-r * 0.35f, -r * 0.35f), r * 0.3f, Tint);
                    DrawCircle(c + new Vector2(r * 0.4f, -r * 0.3f), r * 0.24f, new Color(Tint, Tint.A * 0.7f));
                    DrawArc(c + new Vector2(-r * 0.35f, r * 0.55f), r * 0.55f, Mathf.Pi, Mathf.Tau, 20, Tint, w, true);
                    DrawArc(c + new Vector2(r * 0.4f, r * 0.55f), r * 0.42f, Mathf.Pi, Mathf.Tau, 16, new Color(Tint, Tint.A * 0.7f), w, true);
                    break;
                case IconKind.Plus:
                    DrawLine(c + new Vector2(-r * 0.7f, 0f), c + new Vector2(r * 0.7f, 0f), Tint, w, true);
                    DrawLine(c + new Vector2(0f, -r * 0.7f), c + new Vector2(0f, r * 0.7f), Tint, w, true);
                    break;
                case IconKind.Lock:
                    DrawRect(new Rect2(c + new Vector2(-r * 0.6f, -r * 0.1f), new Vector2(r * 1.2f, r * 0.95f)), Tint);
                    DrawArc(c + new Vector2(0f, -r * 0.1f), r * 0.4f, Mathf.Pi, Mathf.Tau, 16, Tint, w, true);
                    break;
                case IconKind.Check:
                    DrawPolyline(new[] { c + new Vector2(-r * 0.6f, 0f), c + new Vector2(-r * 0.15f, r * 0.45f), c + new Vector2(r * 0.65f, -r * 0.5f) }, Tint, w * 1.2f, true);
                    break;
                case IconKind.Globe:
                    DrawArc(c, r * 0.85f, 0f, Mathf.Tau, 32, Tint, w, true);
                    DrawLine(c + new Vector2(-r * 0.85f, 0f), c + new Vector2(r * 0.85f, 0f), Tint, w * 0.7f, true);
                    DrawArc(c, r * 0.85f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 16, Tint, w * 0.7f, true);
                    DrawLine(c + new Vector2(0f, -r * 0.85f), c + new Vector2(0f, r * 0.85f), Tint, w * 0.7f, true);
                    break;
                case IconKind.Crate:
                    DrawRect(new Rect2(c - new Vector2(r * 0.8f, r * 0.6f), new Vector2(r * 1.6f, r * 1.2f)), Tint, false, w);
                    DrawLine(c + new Vector2(-r * 0.8f, -r * 0.15f), c + new Vector2(r * 0.8f, -r * 0.15f), Tint, w, true);
                    DrawRect(new Rect2(c + new Vector2(-r * 0.18f, -r * 0.3f), new Vector2(r * 0.36f, r * 0.3f)), Tint);
                    break;
                case IconKind.Coin:
                    DrawCircle(c, r * 0.8f, Tint);
                    DrawArc(c, r * 0.5f, 0f, Mathf.Tau, 20, new Color(0f, 0f, 0f, 0.35f), w * 0.8f, true);
                    break;
                case IconKind.Gem:
                    DrawColoredPolygon(new[] { c + new Vector2(0f, -r * 0.85f), c + new Vector2(r * 0.8f, -r * 0.1f), c + new Vector2(0f, r * 0.85f), c + new Vector2(-r * 0.8f, -r * 0.1f) }, Tint);
                    break;
            }
        }
    }
}
