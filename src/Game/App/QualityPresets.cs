using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Graphics presets: 3D render scale, anti-aliasing and sun shadows.
    /// "Средние" (medium) is the 60 FPS target for a mid-range Android phone.
    /// </summary>
    public static class QualityPresets
    {
        public static readonly string[] Names = { "Низкие", "Средние", "Высокие", "Ультра" };
        private static readonly float[] RenderScale = { 0.65f, 0.8f, 1f, 1f };
        private static readonly float[] ShadowDistance = { 0f, 30f, 50f, 80f };

        public static void Apply(Viewport viewport, int preset)
        {
            preset = Mathf.Clamp(preset, 0, Names.Length - 1);
            viewport.Scaling3DScale = RenderScale[preset];
            viewport.Msaa3D = preset >= 3 ? Viewport.Msaa.Msaa4X : (preset == 2 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled);
            viewport.ScreenSpaceAA = preset == 1 ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        }

        public static void ApplyToSun(DirectionalLight3D sun, int preset)
        {
            if (sun == null) return;
            preset = Mathf.Clamp(preset, 0, Names.Length - 1);
            sun.ShadowEnabled = preset > 0;
            sun.DirectionalShadowMaxDistance = Mathf.Max(ShadowDistance[preset], 1f);
            sun.ShadowBlur = preset >= 2 ? 1.5f : 0.5f;
        }
    }
}
