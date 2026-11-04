using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Shared by the native settings page and View. This edits only the visible
// marker's angular offset, never a controller, headset or targeting transform.
internal sealed class GazeCuePositionSlider : MonoBehaviour
{
    private Slider? _slider;
    private Text? _label;
    private float _shown = float.NaN;
    internal static void Create(Transform parent, Vector2 position, float width, Font font, int fontSize)
    {
        var go = new GameObject("Look cue vertical position", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider), typeof(GazeCuePositionSlider));
        var root = (RectTransform)go.transform; root.SetParent(parent, false);
        root.anchoredPosition = position; root.sizeDelta = new Vector2(width, 52);
        go.GetComponent<Image>().color = Color.clear;
        Text Label(string name, string value, float x, float y, float labelWidth)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)child.transform; rect.SetParent(root, false);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(labelWidth, 25);
            var text = child.GetComponent<Text>(); text.font = font; text.fontSize = fontSize;
            text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.text = value;
            return text;
        }
        var trackObject = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var track = (RectTransform)trackObject.transform; track.SetParent(root, false);
        track.anchoredPosition = new Vector2(0, -12); track.sizeDelta = new Vector2(width - 140, 10);
        var trackImage = trackObject.GetComponent<Image>(); trackImage.color = new Color(.15f, .21f, .24f); trackImage.raycastTarget = false;
        var areaObject = new GameObject("Handle area", typeof(RectTransform));
        var area = (RectTransform)areaObject.transform; area.SetParent(root, false);
        area.anchoredPosition = new Vector2(0, -12); area.sizeDelta = new Vector2(width - 140, 30);
        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var handle = (RectTransform)handleObject.transform; handle.SetParent(area, false); handle.sizeDelta = new Vector2(30, 30);
        var handleImage = handleObject.GetComponent<Image>(); handleImage.color = new Color(.8f, .88f, .92f);
        var self = go.GetComponent<GazeCuePositionSlider>(); self._label = Label("Value", "", 0, 18, width);
        Label("Down", "Down", -width * .5f + 30, -12, 60); Label("Up", "Up", width * .5f - 30, -12, 60);
        self._slider = go.GetComponent<Slider>(); self._slider.minValue = GazeCueSettings.MinVerticalDegrees; self._slider.maxValue = GazeCueSettings.MaxVerticalDegrees;
        self._slider.direction = Slider.Direction.LeftToRight; self._slider.handleRect = handle; self._slider.targetGraphic = handleImage;
        self._slider.navigation = new Navigation { mode = Navigation.Mode.None };
        self.Refresh();
        self._slider.onValueChanged.AddListener(value =>
        { ModConfiguration.Instance.LookCue.VerticalDegrees.Value = Mathf.Round(value * 4) * .25f; self.Refresh(); });
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }
    private void Update() => Refresh();
    private void Refresh()
    {
        if (_slider == null || _label == null) return;
        var settings = ModConfiguration.Instance.LookCue;
        if (_shown == settings.VerticalAngle) return;
        _shown = settings.VerticalAngle; _slider.SetValueWithoutNotify(_shown);
        _label.text = "Position: " + settings.PositionLabel;
    }
}
