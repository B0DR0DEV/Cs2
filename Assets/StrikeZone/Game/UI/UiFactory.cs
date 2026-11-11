using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StrikeZone.Game
{
    /// <summary>Builds uGUI elements in code (no prefabs needed for the phase-1 sandbox).</summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _circle;
        private static Sprite _ring;

        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.88f);
        public static readonly Color AccentColor = new Color(0.98f, 0.66f, 0.18f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color MutedColor = new Color(0.62f, 0.65f, 0.7f, 1f);

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null) _circle = ProceduralTextures.Circle(false);
                return _circle;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                if (_ring == null) _ring = ProceduralTextures.Circle(true);
                return _ring;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretches to fill the parent with the given insets.</summary>
        public static RectTransform Stretch(string name, Transform parent, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image AddImage(RectTransform rt, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 boxSize)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, position, boxSize);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>Text filling its parent rect.</summary>
        public static Text FillLabel(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = Stretch("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Round on-screen control (fire, jump, crouch...).</summary>
        public static TouchButton RoundButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position, float diameter, TouchLookArea lookPassthrough)
        {
            var rt = Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(diameter, diameter));
            var img = AddImage(rt, new Color(1f, 1f, 1f, 0.22f), CircleSprite, true);
            var ring = Rect("Ring", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(ring, new Color(1f, 1f, 1f, 0.55f), RingSprite);
            FillLabel(rt, label, Mathf.RoundToInt(diameter * 0.2f), TextAnchor.MiddleCenter, TextColor);
            var b = rt.gameObject.AddComponent<TouchButton>();
            b.Background = img;
            b.LookPassthrough = lookPassthrough;
            return b;
        }

        /// <summary>Rectangular menu button with a click callback.</summary>
        public static Button MenuButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Action onClick, int fontSize = 30)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, position, size);
            var img = AddImage(rt, new Color(1f, 1f, 1f, 0.12f), null, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(1f, 0.75f, 0.35f, 1f);
            colors.selectedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = colors;
            FillLabel(rt, label, fontSize, TextAnchor.MiddleCenter, TextColor);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static void SetButtonTint(Button button, Color color)
        {
            var img = button.targetGraphic as Image;
            if (img != null) img.color = color;
        }
    }
}
