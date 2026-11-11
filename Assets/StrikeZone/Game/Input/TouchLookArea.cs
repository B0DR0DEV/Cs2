using UnityEngine;
using UnityEngine.EventSystems;

namespace StrikeZone.Game
{
    /// <summary>
    /// Right-half swipe area for aiming. Accumulates raw pixel deltas; the player
    /// controller converts them to degrees with the sensitivity settings.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private int _pointerId = int.MinValue;
        private Vector2 _accumulated;

        public void AddDelta(Vector2 pixels)
        {
            _accumulated += pixels;
        }

        /// <summary>Returns the pixel delta since the last call and resets it.</summary>
        public Vector2 Consume()
        {
            Vector2 d = _accumulated;
            _accumulated = Vector2.zero;
            return d;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = eventData.pointerId;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId) _pointerId = int.MinValue;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _accumulated += eventData.delta;
        }

        private void OnDisable()
        {
            _pointerId = int.MinValue;
            _accumulated = Vector2.zero;
        }
    }
}
