using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class UIUtils
{
    public static Canvas CreateCanvas(string name, int sortingOrder = 10)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(eventSystem);
        }
        return canvas;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image image = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Text text = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Text>();
        text.font = UITheme.Font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Action action, int fontSize = 28)
    {
        Button button = Panel(parent, name, UITheme.Panel, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Button>();
        button.targetGraphic = button.image;
        ColorBlock colors = button.colors;
        colors.normalColor = UITheme.Panel;
        colors.highlightedColor = new Color(0.10f, 0.18f, 0.14f, 1f);
        colors.pressedColor = UITheme.Brass;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        Text child = Text(button.transform, "Label", label, fontSize, UITheme.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6));
        button.onClick.AddListener(() => action?.Invoke());
        return button;
    }

    public static Image Background(Transform parent, string textureResource, Color tint)
    {
        Texture2D texture = Resources.Load<Texture2D>(textureResource);
        if (texture == null)
            return Panel(parent, "BackgroundFallback", UITheme.Dark, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        Image image = Rect(parent, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    public static void Slider(Transform parent, string name, string label, float value, Vector2 position, Action<float> changed, float min = 0f, float max = 1f)
    {
        Text(parent, name + "Label", label, 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), position + new Vector2(0, -10), position + new Vector2(300, 24));
        Slider slider = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), position + new Vector2(310, -8), position + new Vector2(650, 18)).gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.fillRect = Rect(slider.transform, "Fill", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0)).gameObject.AddComponent<Image>().rectTransform;
        slider.handleRect = Rect(slider.transform, "Handle", new Vector2(0, 0), new Vector2(0, 1), new Vector2(-10, -10), new Vector2(10, 10)).gameObject.AddComponent<Image>().rectTransform;
        slider.fillRect.GetComponent<Image>().color = UITheme.Brass;
        slider.handleRect.GetComponent<Image>().color = UITheme.Cream;
        slider.onValueChanged.AddListener(v => changed?.Invoke(v));
    }

    public static InputField InputField(Transform parent, string name, string value, Vector2 position, Action<string> changed)
    {
        InputField field = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), position, position + new Vector2(650, 34)).gameObject.AddComponent<InputField>();
        Image background = Rect(field.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        background.color = UITheme.Panel;
        Text text = Text(field.transform, "Text", value, 22, UITheme.Cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        Text placeholder = Text(field.transform, "Placeholder", "Имя гонщика", 22, UITheme.Muted, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        field.targetGraphic = background;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.text = value;
        field.onEndEdit.AddListener(v => changed?.Invoke(v));
        return field;
    }

    public static void Toggle(Transform parent, string name, string label, bool value, Vector2 position, Action<bool> changed)
    {
        Toggle toggle = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), position, position + new Vector2(420, 32)).gameObject.AddComponent<Toggle>();
        Image background = Rect(toggle.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        background.color = UITheme.Panel;
        Image check = Rect(toggle.transform, "Checkmark", new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 4), new Vector2(30, -4)).gameObject.AddComponent<Image>();
        check.color = UITheme.Brass;
        Text checkText = Text(toggle.transform, "Label", label, 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(44, 0), new Vector2(0, 0));
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(v => changed?.Invoke(v));
    }
}
