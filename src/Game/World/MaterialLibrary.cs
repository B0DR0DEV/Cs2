using System.Collections.Generic;
using Godot;
using StrikeZone.Core;

namespace StrikeZone.Game
{
    /// <summary>
    /// Creates and caches materials. Surfaces use world-space triplanar mapping at one tile
    /// per metre, so boxes of any size keep the same texel density and share one material.
    /// </summary>
    public sealed class MaterialLibrary
    {
        private readonly Dictionary<string, StandardMaterial3D> _cache = new Dictionary<string, StandardMaterial3D>();
        private readonly GameDatabase _db;

        public MaterialLibrary(GameDatabase db)
        {
            _db = db;
        }

        public StandardMaterial3D Surface(string surfaceId)
        {
            string key = "surface_" + surfaceId;
            StandardMaterial3D m;
            if (_cache.TryGetValue(key, out m)) return m;
            SurfaceDef def = _db.GetSurface(surfaceId);
            m = new StandardMaterial3D
            {
                AlbedoColor = ParseColor(def.color),
                AlbedoTexture = ProceduralTextures.ForSurface(def.id ?? surfaceId),
                Roughness = 1f - def.smoothness,
                Metallic = def.metallic,
                Uv1Triplanar = true,
                Uv1WorldTriplanar = true,
                Uv1Scale = Vector3.One,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            };
            _cache[key] = m;
            return m;
        }

        public StandardMaterial3D Lit(Color color, float roughness = 0.75f, float metallic = 0f)
        {
            string key = "lit_" + color.ToHtml() + "_" + roughness.ToString("0.00") + "_" + metallic.ToString("0.00");
            StandardMaterial3D m;
            if (_cache.TryGetValue(key, out m)) return m;
            m = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness, Metallic = metallic };
            _cache[key] = m;
            return m;
        }

        /// <summary>Unlit, optionally transparent material (tracers, flashes, health bars).</summary>
        public StandardMaterial3D Unlit(string key, Color color, Texture2D texture = null, bool billboard = false, bool additive = false)
        {
            StandardMaterial3D m;
            if (_cache.TryGetValue("unlit_" + key, out m)) return m;
            m = NewUnlit(color, texture, billboard, additive);
            _cache["unlit_" + key] = m;
            return m;
        }

        /// <summary>Uncached unlit material for objects that change colour individually.</summary>
        public static StandardMaterial3D NewUnlit(Color color, Texture2D texture, bool billboard, bool additive)
        {
            var m = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = color,
                AlbedoTexture = texture,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            if (billboard) m.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            if (additive) m.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
            return m;
        }

        public static Color ParseColor(string html)
        {
            if (string.IsNullOrEmpty(html)) return new Color(0.5f, 0.5f, 0.5f);
            return Color.FromHtml(html);
        }
    }
}
