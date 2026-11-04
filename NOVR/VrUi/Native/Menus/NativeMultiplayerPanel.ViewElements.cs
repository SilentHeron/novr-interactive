using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.92f);

    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);

    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);

    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);

    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);

    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);

    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);

    private static readonly Color JoinButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);

    private static readonly Color FilterDisabledColor = new(0.12f, 0.14f, 0.15f, 0.92f);

    private Font? _font;

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private InputField CreateInputField(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, string placeholder, InputField.ContentType contentType = InputField.ContentType.Standard)
    {
        var rectTransform = CreateImage(name, parent, new Color(0.12f, 0.15f, 0.16f, 0.96f), anchoredPosition, size);
        var inputField = rectTransform.gameObject.AddComponent<InputField>();
        inputField.targetGraphic = rectTransform.GetComponent<Image>();
        inputField.contentType = contentType;
        inputField.lineType = InputField.LineType.SingleLine;

        var text = CreateText($"{name} Text", rectTransform, "", Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var placeholderText = CreateText($"{name} Placeholder", rectTransform, placeholder, Vector2.zero, new Vector2(size.x - 18f, size.y - 6f), 14, TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.80f, 0.65f));
        placeholderText.fontStyle = FontStyle.Italic;

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var button = CreateButton(label, parent, anchoredPosition, size, color, onClick);
        CreateText($"{label} Text", (RectTransform)button.transform, label, Vector2.zero, size, fontSize, alignment, Color.white);
        return button;
    }

    private Button CreateButton(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rectTransform = CreateImage(name, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        NativeButtonFeedback.Configure(button, color);
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        var image = button.targetGraphic;
        if (image != null)
        {
            image.color = color;
        }

        NativeButtonFeedback.SetNormalColor(button, color);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }
}
