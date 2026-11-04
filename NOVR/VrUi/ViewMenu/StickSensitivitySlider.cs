using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Both options surfaces edit the existing angle setting. No second sensitivity
// multiplier can disagree with saved setups or affect twist/nosewheel yaw.
internal sealed class StickSensitivitySlider : MonoBehaviour
{
    private MotionControlSettings? _settings;
    private Slider? _slider;
    private Text? _label;
    private float _shownAngle = float.NaN;

    internal static void Create(Transform parent, Vector2 position, float width, Font font, int fontSize)
    {
        var go = new GameObject("Motion stick sensitivity", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider), typeof(StickSensitivitySlider));
        var root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        root.anchoredPosition = position;
        root.sizeDelta = new Vector2(width, 49);
        go.GetComponent<Image>().color = Color.clear; // Generous drag/click target across the row.

        Text Label(string name, string text, Vector2 point, Vector2 size, int textSize)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(root, false);
            rect.anchoredPosition = point;
            rect.sizeDelta = size;
            var label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = textSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        var trackObject = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var track = (RectTransform)trackObject.transform;
        track.SetParent(root, false);
        track.anchoredPosition = new Vector2(0, -10);
        track.sizeDelta = new Vector2(width - 140, 12);
        var trackImage = trackObject.GetComponent<Image>();
        trackImage.color = new Color(.10f, .24f, .29f);
        trackImage.raycastTarget = false;
        var areaObject = new GameObject("Handle area", typeof(RectTransform));
        var area = (RectTransform)areaObject.transform;
        area.SetParent(root, false);
        area.anchoredPosition = new Vector2(0, -10);
        area.sizeDelta = new Vector2(width - 140, 30);
        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var handle = (RectTransform)handleObject.transform;
        handle.SetParent(area, false);
        handle.sizeDelta = new Vector2(30, 30);
        var handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(.65f, .88f, .96f);

        var self = go.GetComponent<StickSensitivitySlider>();
        self._settings = ModConfiguration.Instance.MotionControls;
        self._label = Label("Value", "", new Vector2(0, 18), new Vector2(width, 24), fontSize);
        Label("Lower", "Lower", new Vector2(-width * .5f + 31, -10), new Vector2(62, 26), fontSize - 2);
        Label("Higher", "Higher", new Vector2(width * .5f - 31, -10), new Vector2(62, 26), fontSize - 2);
        self._slider = go.GetComponent<Slider>();
        self._slider.minValue = 0;
        self._slider.maxValue = 1;
        self._slider.direction = Slider.Direction.LeftToRight;
        self._slider.handleRect = handle;
        self._slider.targetGraphic = handleImage;
        self._slider.navigation = new Navigation { mode = Navigation.Mode.None };
        self.Refresh(); // Initialize without saving or rounding an existing setup.
        self._slider.onValueChanged.AddListener(value =>
        {
            var angle = Mathf.Lerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, value);
            self._settings.StickAngle.Value = Mathf.Round(angle * 2) * .5f;
            self.Refresh();
        });
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (_settings == null || _slider == null || _label == null)
            return;
        var angle = _settings.StickAngle.Value;
        if (_shownAngle == angle)
            return;
        _shownAngle = angle;
        _slider.SetValueWithoutNotify(Mathf.InverseLerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, angle));
        _label.text = $"Stick sensitivity {MotionControlSettings.DefaultStickAngle / angle:0.00}× · {angle:0.#}° full tilt";
    }
}
