using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StrikeZone.Game
{
    /// <summary>
    /// Graphics presets applied at runtime: render scale, shadows, MSAA. On a mid-range
    /// Android phone "Средние" (medium) is the 60 FPS target. Values are per preset:
    /// low, medium, high, ultra.
    /// </summary>
    public static class QualityPresets
    {
        public static readonly string[] Names = { "Низкие", "Средние", "Высокие", "Ультра" };
        private static readonly float[] RenderScale = { 0.7f, 0.85f, 1f, 1f };
        private static readonly float[] ShadowDistance = { 0f, 30f, 45f, 70f };
        private static readonly int[] Msaa = { 1, 1, 2, 4 };

        public static void Apply(int preset, Light sun)
        {
            preset = Mathf.Clamp(preset, 0, Names.Length - 1);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = RenderScale[preset];
                urp.shadowDistance = Mathf.Max(ShadowDistance[preset], 0.01f);
                urp.msaaSampleCount = Msaa[preset];
            }
            else
            {
                QualitySettings.shadowDistance = ShadowDistance[preset];
                QualitySettings.antiAliasing = Msaa[preset] > 1 ? Msaa[preset] : 0;
            }

            if (sun != null)
            {
                if (preset == 0) sun.shadows = LightShadows.None;
                else if (preset == 1) sun.shadows = LightShadows.Hard;
                else sun.shadows = LightShadows.Soft;
            }
        }
    }
}
