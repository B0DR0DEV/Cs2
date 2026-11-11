using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Default touch layout (landscape, reference 1920×1080, height-matched):
    /// left half — floating move stick; right half — swipe to aim; fire buttons on both
    /// sides so either thumb can shoot; jump / crouch / scope / reload clustered on the right.
    /// </summary>
    public static class TouchControlsBuilder
    {
        public static InputRouter Build(Transform parent, Canvas canvas)
        {
            var routerGo = new GameObject("InputRouter");
            routerGo.transform.SetParent(parent, false);
            var router = routerGo.AddComponent<InputRouter>();

            var root = UiFactory.Stretch("TouchControls", canvas.transform);
            router.TouchRoot = root.gameObject;

            // Look area: right 58 % of the screen, behind the buttons.
            var lookRt = UiFactory.Rect("LookArea", root, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.AddImage(lookRt, new Color(0f, 0f, 0f, 0f), null, true);
            var look = lookRt.gameObject.AddComponent<TouchLookArea>();
            router.LookArea = look;

            // Joystick zone: left 42 %, lower 80 %.
            var zone = UiFactory.Rect("StickZone", root, new Vector2(0f, 0f), new Vector2(0.42f, 0.8f), Vector2.zero, Vector2.zero, Vector2.zero);
            UiFactory.AddImage(zone, new Color(0f, 0f, 0f, 0f), null, true);
            var stickBase = UiFactory.Rect("StickBase", zone, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(270f, 250f), new Vector2(240f, 240f));
            UiFactory.AddImage(stickBase, new Color(1f, 1f, 1f, 0.12f), UiFactory.CircleSprite);
            var ring = UiFactory.Stretch("Ring", stickBase);
            UiFactory.AddImage(ring, new Color(1f, 1f, 1f, 0.35f), UiFactory.RingSprite);
            var knob = UiFactory.Rect("Knob", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
            UiFactory.AddImage(knob, new Color(1f, 1f, 1f, 0.5f), UiFactory.CircleSprite);
            var stick = zone.gameObject.AddComponent<VirtualJoystick>();
            stick.Zone = zone;
            stick.Base = stickBase;
            stick.Knob = knob;
            stick.Radius = 110f;
            router.Joystick = stick;

            var br = new Vector2(1f, 0f);
            var bl = new Vector2(0f, 0f);
            var tr = new Vector2(1f, 1f);

            router.FireRight = UiFactory.RoundButton(root, "FireR", "ОГОНЬ", br, new Vector2(-310f, 270f), 210f, look);
            router.Jump = UiFactory.RoundButton(root, "Jump", "ПРЫЖОК", br, new Vector2(-115f, 430f), 140f, null);
            router.Crouch = UiFactory.RoundButton(root, "Crouch", "ПРИСЕД", br, new Vector2(-115f, 130f), 140f, null);
            router.Scope = UiFactory.RoundButton(root, "Scope", "ПРИЦЕЛ", br, new Vector2(-330f, 520f), 120f, look);
            router.Reload = UiFactory.RoundButton(root, "Reload", "ПЕРЕЗ.", br, new Vector2(-520f, 120f), 110f, null);
            router.Inspect = UiFactory.RoundButton(root, "Inspect", "ОСМОТР", br, new Vector2(-520f, 300f), 90f, null);
            router.Walk = UiFactory.RoundButton(root, "Walk", "ШАГ", bl, new Vector2(110f, 560f), 100f, null);
            router.FireLeft = UiFactory.RoundButton(root, "FireL", "ОГОНЬ", bl, new Vector2(200f, 760f), 140f, look);

            router.Slot1 = UiFactory.RoundButton(root, "Slot1", "1", tr, new Vector2(-480f, -70f), 90f, null);
            router.Slot2 = UiFactory.RoundButton(root, "Slot2", "2", tr, new Vector2(-370f, -70f), 90f, null);
            router.Slot3 = UiFactory.RoundButton(root, "Slot3", "НОЖ", tr, new Vector2(-260f, -70f), 90f, null);

            return router;
        }
    }
}
