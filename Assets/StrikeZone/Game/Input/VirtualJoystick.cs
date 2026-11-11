using UnityEngine;
using UnityEngine.EventSystems;

namespace StrikeZone.Game
{
    /// <summary>
    /// Floating joystick: appears where the thumb lands inside its zone. Deflection below
    /// <see cref="WalkThreshold"/> means walking (52 % speed, silent footsteps), above it
    /// means running — the mobile equivalent of the walk key.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public RectTransform Zone;
        public RectTransform Base;
        public RectTransform Knob;
        public float Radius = 110f;
        public float DeadZone = 0.08f;
        public float WalkThreshold = 0.55f;

        private int _pointerId = int.MinValue;
        private Vector2 _value;
        private Vector2 _restPosition;

        public Vector2 Value
        {
            get { return _value; }
        }

        public bool IsWalking
        {
            get
            {
                float m = _value.magnitude;
                return m > DeadZone && m < WalkThreshold;
            }
        }

        private void Start()
        {
            if (Base != null) _restPosition = Base.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != int.MinValue) return;
            _pointerId = eventData.pointerId;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Zone, eventData.position, eventData.pressEventCamera, out local))
            {
                Base.anchoredPosition = local;
            }
            Knob.anchoredPosition = Vector2.zero;
            _value = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Zone, eventData.position, eventData.pressEventCamera, out local))
                return;
            Vector2 offset = local - Base.anchoredPosition;
            if (offset.magnitude > Radius)
            {
                // Drag the base along so the stick never "sticks" at the rim.
                Base.anchoredPosition += offset - offset.normalized * Radius;
                offset = offset.normalized * Radius;
            }
            Knob.anchoredPosition = offset;
            Vector2 v = offset / Radius;
            _value = v.magnitude < DeadZone ? Vector2.zero : v;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            _pointerId = int.MinValue;
            _value = Vector2.zero;
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            if (Base != null) Base.anchoredPosition = _restPosition;
        }
    }
}
