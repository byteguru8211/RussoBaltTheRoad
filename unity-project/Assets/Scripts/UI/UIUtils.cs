using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class UIUtils
{
    public static Canvas CreateCanvas(string name, int sortingOrder = 10)
    {
        Canvas existing = UnityEngine.Object.FindObjectsByType<Canvas>().FirstOrDefault(c => c.name == name);
        if (existing != null)
            return existing;
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(eventSystem);
        }
        return canvas;
    }

    public static Sprite LoadSprite(string resource)
    {
        Sprite sprite = Resources.Load<Sprite>(resource);
        if (sprite != null)
            return sprite;
        Texture2D texture = Resources.Load<Texture2D>(resource);
        if (texture == null)
            return null;
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = resource;
        return sprite;
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

    public static Image Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool raycastTarget = true)
    {
        Image image = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static Image Card(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image image = Panel(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax);
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.77f, 0.63f, 0.36f, 0.42f);
        outline.effectDistance = new Vector2(2f, 2f);
        return image;
    }

    public static Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Font font = null)
    {
        Text text = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Text>();
        text.font = font != null ? font : UITheme.Font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Action action, int fontSize = 28, Font font = null)
    {
        Button button = Card(parent, name, UITheme.Panel, anchorMin, anchorMax, offsetMin, offsetMax).gameObject.AddComponent<Button>();
        button.targetGraphic = button.image;
        ColorBlock colors = button.colors;
        colors.normalColor = UITheme.Panel;
        colors.highlightedColor = UITheme.Raised;
        colors.pressedColor = UITheme.Brass;
        colors.selectedColor = UITheme.Raised;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        Text(button.transform, "Label", label, fontSize, UITheme.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(18, 8), new Vector2(-18, -8), font);
        button.onClick.AddListener(() => action?.Invoke());
        return button;
    }

    public static Image Background(Transform parent, string textureResource, Color tint)
    {
        Sprite sprite = LoadSprite(textureResource);
        if (sprite == null)
            return Panel(parent, "BackgroundFallback", UITheme.Dark, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        Image image = Rect(parent, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static void Scrim(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Panel(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax, false);
    }

    public static void Divider(Transform parent, string name, Vector2 position, float width)
    {
        Panel(parent, name, new Color(0.77f, 0.63f, 0.36f, 0.35f), new Vector2(0f, 1f), new Vector2(0f, 1f), position, position + new Vector2(width, 2f), false);
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
