using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Tiny helpers for building the uGUI screens (used by the editor level builder).</summary>
public static class UIFactory
{
    static Font cachedFont;

    public static Font BuiltinFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return cachedFont;
        }
    }

    public static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    public static EventSystem EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null) return existing;

        var go = new GameObject("EventSystem");
        var es = go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        return es;
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image FullScreen(Transform parent, string name, Color color)
    {
        var rt = Rect(parent, name);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    public static Image Box(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        var rt = Rect(parent, name);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(Transform parent, string name, string text, int fontSize, Color color,
        TextAnchor alignment, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        var rt = Rect(parent, name);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;

        var t = rt.gameObject.AddComponent<Text>();
        t.font = BuiltinFont;
        t.fontSize = fontSize;
        t.text = text;
        t.color = color;
        t.alignment = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 size,
        Color color, int fontSize, Color textColor)
    {
        var img = Box(parent, name, anchor, size, color);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        Label(img.transform, "Label", text, fontSize, textColor, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            size, Vector2.zero);
        return btn;
    }
}
