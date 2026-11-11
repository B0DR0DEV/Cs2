using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Multi-touch game controls drawn and hit-tested by hand (Godot GUI buttons only see
    /// one emulated pointer). Left side: floating move stick (light deflection = walk);
    /// right side: swipe to aim; fire buttons on both sides that also aim while held;
    /// jump, crouch, scope, reload, inspect and weapon slots. Phase 4 makes the layout
    /// user-editable on top of this widget list.
    /// </summary>
    public partial class TouchControls : Control
    {
        public sealed class TouchButtonState
        {
            public string Id;
            public string Label;
            public Vector2 Anchor;   // 0..1 of the screen
            public Vector2 Offset;   // pixels from the anchor (canvas units)
            public float Radius;
            public bool AimsWhileHeld;
            public int Finger = -1;
            public int Presses;

            public bool Held
            {
                get { return Finger >= 0; }
            }
        }

        public const float WalkThreshold = 0.55f;
        private const float StickRadius = 110f;

        private readonly List<TouchButtonState> _buttons = new List<TouchButtonState>();
        private readonly Dictionary<string, TouchButtonState> _byId = new Dictionary<string, TouchButtonState>();
        private int _stickFinger = -1;
        private Vector2 _stickBase;
        private Vector2 _stickKnob;
        private Vector2 _stickValue;
        private int _lookFinger = -1;
        private Vector2 _lookAccum;
        private Font _font;

        public bool Enabled = true;

        public Vector2 StickValue
        {
            get { return _stickValue; }
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            _font = ThemeDB.FallbackFont;

            Add("fireR", "ОГОНЬ", new Vector2(1f, 1f), new Vector2(-310f, -270f), 105f, true);
            Add("jump", "ПРЫЖОК", new Vector2(1f, 1f), new Vector2(-115f, -430f), 70f, false);
            Add("crouch", "ПРИСЕД", new Vector2(1f, 1f), new Vector2(-115f, -130f), 70f, false);
            Add("scope", "ПРИЦЕЛ", new Vector2(1f, 1f), new Vector2(-330f, -520f), 60f, true);
            Add("reload", "ПЕРЕЗ.", new Vector2(1f, 1f), new Vector2(-520f, -120f), 55f, false);
            Add("inspect", "ОСМОТР", new Vector2(1f, 1f), new Vector2(-520f, -300f), 45f, false);
            Add("walk", "ШАГ", new Vector2(0f, 1f), new Vector2(110f, -560f), 50f, false);
            Add("fireL", "ОГОНЬ", new Vector2(0f, 1f), new Vector2(200f, -760f), 70f, true);
            Add("slot1", "1", new Vector2(1f, 0f), new Vector2(-480f, 70f), 45f, false);
            Add("slot2", "2", new Vector2(1f, 0f), new Vector2(-370f, 70f), 45f, false);
            Add("slot3", "НОЖ", new Vector2(1f, 0f), new Vector2(-260f, 70f), 45f, false);
            ResetStick();
        }

        private void Add(string id, string label, Vector2 anchor, Vector2 offset, float radius, bool aims)
        {
            var b = new TouchButtonState { Id = id, Label = label, Anchor = anchor, Offset = offset, Radius = radius, AimsWhileHeld = aims };
            _buttons.Add(b);
            _byId[id] = b;
        }

        public bool Held(string id)
        {
            TouchButtonState b;
            return _byId.TryGetValue(id, out b) && b.Held;
        }

        /// <summary>True once per press since the last call.</summary>
        public bool Consume(string id)
        {
            TouchButtonState b;
            if (!_byId.TryGetValue(id, out b) || b.Presses == 0) return false;
            b.Presses = 0;
            return true;
        }

        public Vector2 ConsumeLook()
        {
            Vector2 d = _lookAccum;
            _lookAccum = Vector2.Zero;
            return d;
        }

        private Vector2 Center(TouchButtonState b)
        {
            return Size * b.Anchor + b.Offset;
        }

        private void ResetStick()
        {
            _stickBase = new Vector2(270f, Size.Y - 250f);
            _stickKnob = _stickBase;
            _stickValue = Vector2.Zero;
        }

        public void ReleaseAll()
        {
            foreach (var b in _buttons)
            {
                b.Finger = -1;
                b.Presses = 0;
            }
            _stickFinger = -1;
            _lookFinger = -1;
            _lookAccum = Vector2.Zero;
            ResetStick();
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            if (!Visible || !Enabled) return;

            var touch = @event as InputEventScreenTouch;
            if (touch != null)
            {
                var local = (InputEventScreenTouch)MakeInputLocal(touch);
                if (local.Pressed) OnPress(local.Index, local.Position);
                else OnRelease(local.Index);
                QueueRedraw();
                return;
            }

            var drag = @event as InputEventScreenDrag;
            if (drag != null)
            {
                var local = (InputEventScreenDrag)MakeInputLocal(drag);
                OnDrag(local.Index, local.Position, local.Relative);
                QueueRedraw();
            }
        }

        private void OnPress(int finger, Vector2 pos)
        {
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                var b = _buttons[i];
                if (b.Held || pos.DistanceTo(Center(b)) > b.Radius * 1.1f) continue;
                b.Finger = finger;
                b.Presses++;
                if (GameContext.Settings.Vibration) Input.VibrateHandheld(12, 0.25f);
                return;
            }
            if (_stickFinger < 0 && pos.X < Size.X * 0.42f && pos.Y > Size.Y * 0.2f)
            {
                _stickFinger = finger;
                _stickBase = pos;
                _stickKnob = pos;
                _stickValue = Vector2.Zero;
                return;
            }
            if (_lookFinger < 0 && pos.X >= Size.X * 0.42f) _lookFinger = finger;
        }

        private void OnDrag(int finger, Vector2 pos, Vector2 relative)
        {
            if (finger == _stickFinger)
            {
                Vector2 offset = pos - _stickBase;
                if (offset.Length() > StickRadius)
                {
                    // Drag the base along so the stick never sticks at the rim.
                    _stickBase += offset - offset.Normalized() * StickRadius;
                    offset = offset.Normalized() * StickRadius;
                }
                _stickKnob = _stickBase + offset;
                Vector2 v = offset / StickRadius;
                _stickValue = v.Length() < 0.08f ? Vector2.Zero : new Vector2(v.X, -v.Y);
                return;
            }
            if (finger == _lookFinger)
            {
                _lookAccum += relative;
                return;
            }
            foreach (var b in _buttons)
            {
                if (b.Finger == finger && b.AimsWhileHeld)
                {
                    _lookAccum += relative;
                    return;
                }
            }
        }

        private void OnRelease(int finger)
        {
            if (finger == _stickFinger)
            {
                _stickFinger = -1;
                ResetStick();
            }
            if (finger == _lookFinger) _lookFinger = -1;
            foreach (var b in _buttons)
                if (b.Finger == finger) b.Finger = -1;
        }

        public override void _Draw()
        {
            if (_stickFinger < 0) _stickBase = new Vector2(270f, Size.Y - 250f);
            Vector2 knob = _stickFinger >= 0 ? _stickKnob : _stickBase;
            DrawCircle(_stickBase, StickRadius + 10f, new Color(1f, 1f, 1f, 0.08f));
            DrawArc(_stickBase, StickRadius + 10f, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.35f), 3f, true);
            DrawArc(_stickBase, (StickRadius + 10f) * WalkThreshold, 0f, Mathf.Tau, 40, new Color(1f, 1f, 1f, 0.12f), 2f, true);
            DrawCircle(knob, 52f, new Color(1f, 1f, 1f, 0.45f));

            foreach (var b in _buttons)
            {
                Vector2 c = Center(b);
                bool fire = b.Id.StartsWith("fire");
                Color fill = b.Held ? new Color(1f, 1f, 1f, 0.42f) : (fire ? new Color(0.95f, 0.35f, 0.25f, 0.28f) : new Color(1f, 1f, 1f, 0.18f));
                DrawCircle(c, b.Radius, fill);
                DrawArc(c, b.Radius, 0f, Mathf.Tau, 40, new Color(1f, 1f, 1f, 0.55f), 2.5f, true);
                int size = Mathf.Clamp((int)(b.Radius * 0.36f), 16, 34);
                Vector2 textSize = _font.GetStringSize(b.Label, HorizontalAlignment.Left, -1, size);
                DrawString(_font, c + new Vector2(-textSize.X * 0.5f, size * 0.35f), b.Label, HorizontalAlignment.Left, -1, size, new Color(1f, 1f, 1f, 0.92f));
            }
        }
    }
}
