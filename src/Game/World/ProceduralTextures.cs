using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// All textures are generated in code — nothing to license. Surface textures are
    /// greyscale detail maps (tinted by the material colour) with a 1 m grid baked in, like
    /// developer blockout textures, so distances and speeds are easy to judge while tuning.
    /// </summary>
    public static class ProceduralTextures
    {
        private const int Size = 128;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D ForSurface(string id)
        {
            string key = "surface_" + id;
            Texture2D tex;
            if (Cache.TryGetValue(key, out tex)) return tex;
            var bytes = new byte[Size * Size * 4];
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
                    if (x == 0 || y == 0) v *= 0.72f;
                    else if (x == Size / 2 || y == Size / 2) v *= 0.9f;
                    byte b = (byte)(Mathf.Clamp(v, 0f, 1f) * 255f);
                    int i = (y * Size + x) * 4;
                    bytes[i] = b;
                    bytes[i + 1] = b;
                    bytes[i + 2] = b;
                    bytes[i + 3] = 255;
                }
            }
            tex = Create(Size, bytes, true);
            Cache[key] = tex;
            return tex;
        }

        public static Texture2D BulletHole()
        {
            return Radial("bullethole", 32, (d, x, y) =>
            {
                float jag = Noise(x * 0.9f, y * 0.9f, 7, 1 << 20) * 0.25f;
                float core = 1f - Edge(0.18f, 0.3f + jag * 0.5f, d);
                float ring = (1f - Edge(0.3f, 0.75f + jag, d)) * 0.55f;
                float a = Mathf.Max(core, ring);
                float shade = core > 0.5f ? 0.03f : 0.16f;
                return new Color(shade, shade, shade, a);
            });
        }

        /// <summary>Soft radial glow for muzzle flashes, sparks and puffs.</summary>
        public static Texture2D Glow()
        {
            return Radial("glow", 64, (d, x, y) => new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp(1f - d, 0f, 1f), 2.2f)));
        }

        /// <summary>Vertical gradient used for map preview cards and backdrops.</summary>
        public static Texture2D Gradient(string key, Color top, Color bottom, int height = 128)
        {
            Texture2D tex;
            if (Cache.TryGetValue("grad_" + key, out tex)) return tex;
            var bytes = new byte[4 * height * 4];
            for (int y = 0; y < height; y++)
            {
                Color c = top.Lerp(bottom, y / (float)(height - 1));
                for (int x = 0; x < 4; x++)
                {
                    int i = (y * 4 + x) * 4;
                    bytes[i] = (byte)(c.R * 255f);
                    bytes[i + 1] = (byte)(c.G * 255f);
                    bytes[i + 2] = (byte)(c.B * 255f);
                    bytes[i + 3] = (byte)(c.A * 255f);
                }
            }
            var img = Image.CreateFromData(4, height, false, Image.Format.Rgba8, bytes);
            tex = ImageTexture.CreateFromImage(img);
            Cache["grad_" + key] = tex;
            return tex;
        }

        private delegate Color RadialFn(float distance, int x, int y);

        private static Texture2D Radial(string key, int size, RadialFn fn)
        {
            Texture2D tex;
            if (Cache.TryGetValue(key, out tex)) return tex;
            var bytes = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    Color c = fn(Mathf.Sqrt(dx * dx + dy * dy), x, y);
                    int i = (y * size + x) * 4;
                    bytes[i] = (byte)(Mathf.Clamp(c.R, 0f, 1f) * 255f);
                    bytes[i + 1] = (byte)(Mathf.Clamp(c.G, 0f, 1f) * 255f);
                    bytes[i + 2] = (byte)(Mathf.Clamp(c.B, 0f, 1f) * 255f);
                    bytes[i + 3] = (byte)(Mathf.Clamp(c.A, 0f, 1f) * 255f);
                }
            }
            tex = Create(size, bytes, true);
            Cache[key] = tex;
            return tex;
        }

        private static Texture2D Create(int size, byte[] bytes, bool mipmaps)
        {
            var img = Image.CreateFromData(size, size, false, Image.Format.Rgba8, bytes);
            if (mipmaps) img.GenerateMipmaps();
            return ImageTexture.CreateFromImage(img);
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
            return 0.65f + (Fbm(x, y, 8) - 0.5f) * 0.35f + (Hash(x, y, 4) - 0.5f) * 0.1f;
        }

        // ---------------------------------------------------------------- noise

        /// <summary>GLSL-style smoothstep.</summary>
        public static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
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

        private static float Noise(float x, float y, int seed, int period)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = x0 + 1, y1 = y0 + 1;
            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y1, period), seed);
            float d = Hash(Wrap(x1, period), Wrap(y1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static int Wrap(int v, int period)
        {
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
