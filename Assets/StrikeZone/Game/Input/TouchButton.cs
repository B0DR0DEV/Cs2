using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StrikeZone.Game
{
    /// <summary>
    /// On-screen button that reports held state and press edges. Fire buttons also pass
    /// finger drags through to the look area, so you can aim while holding fire — the
    /// standard control scheme of mobile shooters.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public TouchLookArea LookPassthrough;
        public Image Background;
        public Color IdleColor = new Color(1f, 1f, 1f, 0.22f);
        public Color PressedColor = new Color(1f, 1f, 1f, 0.45f);

        private int _pointerId = int.MinValue;
        private int _presses;

        public bool Held
        {
            get { return _pointerId != int.MinValue; }
        }

        /// <summary>Returns true once for every press since the last call.</summary>
        public bool ConsumePress()
        {
            if (_presses <= 0) return false;
            _presses = 0;
            return true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Held) return;
            _pointerId = eventData.pointerId;
            _presses++;
            if (Background != null) Background.color = PressedColor;
            if (GameContext.Settings != null && GameContext.Settings.Vibration) Haptics.Tap();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId || LookPassthrough == null) return;
            LookPassthrough.AddDelta(eventData.delta);
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            _pointerId = int.MinValue;
            if (Background != null) Background.color = IdleColor;
        }
    }
}
