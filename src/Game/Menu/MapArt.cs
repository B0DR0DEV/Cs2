using Godot;

namespace StrikeZone.Game
{
    /// <summary>Stylised map preview: sky-to-ground gradient with a skyline silhouette.</summary>
    public partial class MapArt : Control
    {
        public MapInfo Map;

        public override void _Draw()
        {
            if (Map == null) return;
            int bands = 24;
            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands;
                Color c = Map.Top.Lerp(Map.Bottom, t0);
                DrawRect(new Rect2(0f, Size.Y * t0, Size.X, Size.Y / bands + 1f), c);
            }
            Color sil = new Color(0.04f, 0.05f, 0.06f, 0.85f);
            float ground = Size.Y * 0.72f;
            DrawRect(new Rect2(0f, ground, Size.X, Size.Y - ground), new Color(0.03f, 0.035f, 0.045f, 0.9f));
            uint h = 2166136261u;
            foreach (char ch in Map.Id) h = (h ^ (uint)ch) * 16777619u;
            float x = 0f;
            while (x < Size.X)
            {
                h = h * 1664525u + 1013904223u;
                float w = 20f + (h % 50u);
                h = h * 1664525u + 1013904223u;
                float height = 18f + (h % 70u);
                DrawRect(new Rect2(x, ground - height, w, height + 1f), sil);
                if ((h & 3u) == 0u) DrawRect(new Rect2(x + w * 0.4f, ground - height - 18f, 4f, 18f), sil);
                x += w + (h % 12u);
            }
        }
    }
}
