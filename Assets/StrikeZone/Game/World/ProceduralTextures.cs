using System.Collections.Generic;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// All textures in phase 1 are generated in code: no third-party assets, nothing to
    /// license. Surface textures are greyscale detail maps (tinted by the material colour)
    /// with a 1 m grid baked in, like developer blockout textures, so distances and
    /// movement speed are easy to judge while tuning.
    /// </summary>
    public static class ProceduralTextures
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D ForSurface(string id)
        {
            Texture2D tex;
            if (Cache.TryGetValue("surface_" + id, out tex)) return tex;

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float v;
                    switch (id)
                    {
                        case "wood": v = Wood(x, y); break;
                        case "metal": v = Metal(x, y); break;
                        case "brick": v = Brick(x, y); break;
                        case "dirt":
                        case "sand": v = Dirt(x, y); break;
                        case "glass": v = 0.95f; break;
                        default: v = Concrete(x, y); break;
                    }
                    // 1 m grid line on the tile edge (the box mesh maps 1 UV unit to 1 m).
                    if (x == 0 || y == 0) v *= 0.72f;
                    else if (x == Size / 2 || y == Size / 2) v *= 0.9f;
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    pixels[y * Size + x] = new Color32(b, b, b, 255);
                }
            }
            tex = Create("surface_" + id, pixels, true);
            Cache["surface_" + id] = tex;
            return tex;
        }

        public static Texture2D BulletHole()
        {
            Texture2D tex;
            if (Cache.TryGetValue("bullethole", out tex)) return tex;
            const int s = 32;
            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f;
                    float dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float jag = Noise(x * 0.9f, y * 0.9f, 7) * 0.25f;
                    float core = 1f - Edge(0.18f, 0.3f + jag * 0.5f, d);
                    float ring = (1f - Edge(0.3f, 0.75f + jag, d)) * 0.55f;
                    float a = Mathf.Max(core, ring);
                    byte shade = (byte)(core > 0.5f ? 8 : 40);
                    pixels[y * s + x] = new Color32(shade, shade, shade, (byte)(a * 255f));
                }
            }
            tex = Create("bullethole", pixels, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Cache["bullethole"] = tex;
            return tex;
        }

        /// <summary>Soft radial glow, used for muzzle flash, sparks and hit puffs.</summary>
        public static Texture2D Glow()
        {
            Texture2D tex;
            if (Cache.TryGetValue("glow", out tex)) return tex;
            const int s = 64;
            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f;
                    float dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                    pixels[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex = Create("glow", pixels, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Cache["glow"] = tex;
            return tex;
        }

        /// <summary>Plain white texture for UI shapes and tracers.</summary>
        public static Texture2D White()
        {
            Texture2D tex;
            if (Cache.TryGetValue("white", out tex)) return tex;
            var pixels = new Color32[4 * 4];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "white" };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Cache["white"] = tex;
            return tex;
        }

        /// <summary>Circle sprite for UI (joystick, buttons).</summary>
        public static Sprite Circle(bool ring)
        {
            string key = ring ? "ui_ring" : "ui_circle";
            Texture2D tex;
            if (!Cache.TryGetValue(key, out tex))
            {
                const int s = 128;
                var pixels = new Color32[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s * 2f - 1f;
                        float dy = (y + 0.5f) / s * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float outer = 1f - Edge(0.96f, 1f, d);
                        float a = ring ? outer * Edge(0.84f, 0.88f, d) : outer;
                        pixels[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                tex = Create(key, pixels, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                Cache[key] = tex;
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Black mask with a soft-edged transparent circle — the sniper scope view.</summary>
        public static Sprite ScopeMask()
        {
            Texture2D tex;
            if (!Cache.TryGetValue("scope", out tex))
            {
                const int s = 256;
                var pixels = new Color32[s * s];
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s * 2f - 1f;
                        float dy = (y + 0.5f) / s * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Edge(0.9f, 0.97f, d);
                        pixels[y * s + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                    }
                }
                tex = Create("scope", pixels, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                Cache["scope"] = tex;
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D Create(string name, Color32[] pixels, bool repeat)
        {
            int s = (int)Mathf.Sqrt(pixels.Length);
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        // ---------------------------------------------------------------- patterns

        private static float Concrete(int x, int y)
        {
            float n = Fbm(x, y, 1);
            float speck = Hash(x, y, 3) > 0.985f ? -0.18f : 0f;
            return 0.78f + (n - 0.5f) * 0.22f + speck;
        }

        private static float Wood(int x, int y)
        {
            const int plank = Size / 4;
            int row = y / plank;
            float grain = Mathf.Sin((x + Hash(row, 0, 5) * 300f) * 0.09f + Fbm(x * 0.3f, y * 2f, row + 10) * 6f) * 0.5f + 0.5f;
            float v = 0.62f + grain * 0.18f + (Hash(row, 1, 9) - 0.5f) * 0.12f;
            if (y % plank == 0) v *= 0.55f;
            return v;
        }

        private static float Metal(int x, int y)
        {
            const int cell = 16;
            int cx = x % cell;
            int cy = y % cell;
            bool odd = ((x / cell) + (y / cell)) % 2 == 0;
            float diamond;
            if (odd) diamond = Mathf.Abs(cx - cy) < 2 ? 0.12f : 0f;
            else diamond = Mathf.Abs(cx + cy - cell) < 2 ? 0.12f : 0f;
            return 0.7f + diamond + (Fbm(x, y, 4) - 0.5f) * 0.1f;
        }

        private static float Brick(int x, int y)
        {
            const int bh = 16;
            const int bw = 32;
            int row = y / bh;
            int offset = row % 2 == 0 ? 0 : bw / 2;
            bool mortar = y % bh < 2 || (x + offset) % bw < 2;
            if (mortar) return 0.85f;
            int brick = (x + offset) / bw + row * 7;
            return 0.6f + (Hash(brick, row, 2) - 0.5f) * 0.18f + (Fbm(x, y, 6) - 0.5f) * 0.1f;
        }

        private static float Dirt(int x, int y)
        {
            return 0.65f + (Fbm(x * 1.5f, y * 1.5f, 8) - 0.5f) * 0.35f + (Hash(x, y, 4) - 0.5f) * 0.1f;
        }

        // ---------------------------------------------------------------- noise

        /// <summary>GLSL-style smoothstep (Unity's Mathf.SmoothStep interpolates between values instead).</summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Noise(float x, float y, int seed)
        {
            return Noise(x, y, seed, 1 << 20);
        }

        private static float Noise(float x, float y, int seed, int period)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            // Wrap at the texture size so tiles repeat seamlessly.
            int x1 = x0 + 1, y1 = y0 + 1;
            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y1, period), seed);
            float d = Hash(Wrap(x1, period), Wrap(y1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static int Wrap(int v, int period)
        {
            if (period <= 0) return v;
            int m = v % period;
            return m < 0 ? m + period : m;
        }

        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f / 8f;
            for (int i = 0; i < 4; i++)
            {
                int period = Mathf.Max(1, Mathf.RoundToInt(Size * freq));
                sum += Noise(x * freq, y * freq, seed + i * 31, period) * amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / 0.9375f;
        }
    }
}
