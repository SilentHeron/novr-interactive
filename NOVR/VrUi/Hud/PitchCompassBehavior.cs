using HarmonyLib;
using NOVR.Compatibility;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(60)]
public class PitchCompassBehavior : MonoBehaviour
{
    private const int FullPitchStepCount = 36;
    private const float PitchRangeDegrees = 180f;
    private const float PitchStepDegrees = PitchRangeDegrees / FullPitchStepCount;
    private const string SliceRootName = "NOVR_PitchCompassSlices";

    private static readonly AccessTools.FieldRef<global::FlightHud, RawImage>? PitchCompass =
        GameField.Optional<global::FlightHud, RawImage>("pitchCompass");
    private static readonly AccessTools.FieldRef<global::FlightHud, Transform>? CockpitTransform =
        GameField.Optional<global::FlightHud, Transform>("cockpitTransform");

    private RawImage? _sourcePitchCompass;
    private Texture? _sourceTexture;
    private RectTransform? _sliceRoot;
    private FlightHud? _flightHud;
    private bool _suppressedSource, _sourceWasEnabled;

    private void OnEnable() => BuildSlices();

    private void LateUpdate()
    {
        // Native SetAircraft can happen after OnEnable/Start. Retry until its
        // serialized source is ready, and rebuild only if that source changes.
        BuildSlices();
        var root = _sliceRoot;
        var source = _sourcePitchCompass;
        var cockpit = _flightHud != null && CockpitTransform != null ? CockpitTransform(_flightHud) : null;
        var camera = APIBus.CockpitHudCamera;
        if (root == null || source == null || cockpit == null || camera == null ||
            !source.gameObject.activeInHierarchy || (!_suppressedSource && !source.enabled))
        {
            SuspendSlices();
            return;
        }

        root.gameObject.SetActive(true);
        root.position = camera.transform.position;
        var inverseCockpitRotation = Quaternion.Inverse(cockpit.rotation);
        var targetUp = inverseCockpitRotation * Vector3.up;
        var targetRight = Vector3.Cross(targetUp, Vector3.forward).normalized;
        // Avoid a zero LookRotation vector at exactly vertical pitch.
        if (targetRight.sqrMagnitude < .000001f)
            targetRight = Vector3.right;
        var targetForward = Vector3.Cross(targetUp, targetRight).normalized;
        root.rotation = Quaternion.LookRotation(targetForward, targetUp);

        var focused = ModConfiguration.Instance.FocusedFlightHud.Value;
        var size = focused ? ModConfiguration.Instance.FlightHudScale.Value : 1f;
        for (var i = 0; i < root.childCount; i++)
        {
            var slice = root.GetChild(i);
            slice.localScale = Vector3.one * (.8f * size);
            var direction = slice.position - camera.transform.position;
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            // Clip the ladder to the aircraft's forward instrument window, not
            // the user's entire view. Line positions retain their real angles.
            slice.gameObject.SetActive(!focused || direction.z > 0 &&
                Mathf.Abs(yaw) <= 16f * size / .38f && Mathf.Abs(pitch) <= 10f * size / .38f);
        }
        if (!_suppressedSource)
        {
            _sourceWasEnabled = source.enabled;
            _suppressedSource = true;
        }
        source.enabled = false;
    }

    private void BuildSlices()
    {
        if (_flightHud == null)
            _flightHud = GetComponent<global::FlightHud>();
        var source = _flightHud != null && PitchCompass != null ? PitchCompass(_flightHud) : null;
        var texture = source != null ? source.texture : null;
        if (_sliceRoot != null && source != null && texture != null &&
            source == _sourcePitchCompass && texture == _sourceTexture)
            return;
        ReleaseSlices();
        if (_flightHud == null || source == null || texture == null)
            return;
        var sourceRect = source.rectTransform;
        var height = sourceRect.rect.height / source.uvRect.height;
        if (source.uvRect.height <= Mathf.Epsilon || sourceRect.rect.width <= 0 ||
            height <= 0 || float.IsNaN(height) || float.IsInfinity(height))
            return;

        _sourcePitchCompass = source;
        _sourceTexture = texture;
        var root = CreateSliceRoot(_flightHud, sourceRect, height);
        _sliceRoot = root;
        InstrumentBrightness.Add(root);
        for (var sliceIndex = 0; sliceIndex < FullPitchStepCount; sliceIndex++)
        {
            var slice = CreateSliceImage(source, texture, sliceIndex, root, height);
            var pitchDegrees = 90f - sliceIndex * PitchStepDegrees;
            var opposite = Instantiate(slice);
            slice.transform.Rotate(Vector3.right, pitchDegrees);
            slice.transform.Rotate(Vector3.forward, 180, Space.Self);
            slice.transform.position = slice.transform.forward * 1000f;
            opposite.transform.Rotate(Vector3.right, pitchDegrees + 180);
            opposite.transform.Rotate(Vector3.forward, 180, Space.Self);
            opposite.transform.position = opposite.transform.forward * 1000f;
            slice.transform.SetParent(root, true);
            opposite.transform.SetParent(root, true);
            slice.transform.localScale = Vector3.one * .8f;
            opposite.transform.localScale = Vector3.one * .8f;
        }
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
        // Wait for a current cockpit and HUD camera before replacing the source.
        root.gameObject.SetActive(false);
    }

    private void SuspendSlices()
    {
        if (_sliceRoot != null)
            _sliceRoot.gameObject.SetActive(false);
        if (_suppressedSource && _sourcePitchCompass != null)
            _sourcePitchCompass.enabled = _sourceWasEnabled;
        _suppressedSource = false;
    }

    private void ReleaseSlices()
    {
        SuspendSlices();
        if (_sliceRoot != null)
            Destroy(_sliceRoot.gameObject);
        _sliceRoot = null;
        _sourcePitchCompass = null;
        _sourceTexture = null;
    }

    private void OnDisable() => SuspendSlices();
    private void OnDestroy() => ReleaseSlices();

    private static RectTransform CreateSliceRoot(FlightHud hud, RectTransform sourceRectTransform, float height)
    {
        var root = new GameObject(SliceRootName, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(hud.transform, false);
        root.anchorMin = sourceRectTransform.anchorMin;
        root.anchorMax = sourceRectTransform.anchorMax;
        root.pivot = sourceRectTransform.pivot;
        root.anchoredPosition = sourceRectTransform.anchoredPosition;
        root.sizeDelta = new Vector2(sourceRectTransform.rect.width, height);
        root.localScale = sourceRectTransform.localScale;
        root.localRotation = sourceRectTransform.localRotation;
        root.SetSiblingIndex(sourceRectTransform.GetSiblingIndex() + 1);
        root.position = Vector3.zero;
        return root;
    }

    private static GameObject CreateSliceImage(RawImage source, Texture sourceTexture, int sliceIndex, RectTransform root, float height)
    {
        if (sliceIndex == 0)
        {
            return CreateWrappedEndCapSliceImage(source, sourceTexture, root, height);
        }

        var normalizedPitchCenter = sliceIndex / (float)FullPitchStepCount;
        var normalizedSliceHeight = 1f / FullPitchStepCount;
        var normalizedSliceCenter = Mathf.Lerp(1f, 0f, normalizedPitchCenter);
        var normalizedSliceBottom = Mathf.Clamp01(normalizedSliceCenter - normalizedSliceHeight * 0.5f);
        var pitchDegrees = 90f - sliceIndex * PitchStepDegrees;

        var sliceObject = new GameObject($"PitchCompassSlice_{pitchDegrees:+00;-00;000}", typeof(RectTransform), typeof(RawImage));
        var sliceTransform = sliceObject.GetComponent<RectTransform>();
        sliceTransform.SetParent(root, false);
        sliceTransform.anchorMin = new Vector2(0.5f, 0f);
        sliceTransform.anchorMax = new Vector2(0.5f, 0f);
        sliceTransform.pivot = new Vector2(0.5f, 0.5f);
        sliceTransform.anchoredPosition = new Vector2(0f, normalizedSliceBottom * height);
        sliceTransform.sizeDelta = new Vector2(source.rectTransform.rect.width, normalizedSliceHeight * height);

        var sliceImage = sliceObject.GetComponent<RawImage>();
        sliceImage.texture = sourceTexture;
        sliceImage.color = source.color;
        sliceImage.material = source.material;
        sliceImage.raycastTarget = false;
        sliceImage.uvRect = new Rect(0f, normalizedSliceBottom, 1f, normalizedSliceHeight);
        return sliceObject;

    }

    private static GameObject CreateWrappedEndCapSliceImage(RawImage source, Texture sourceTexture, RectTransform root, float height)
    {
        const int sliceIndex = 0;
        var normalizedSliceHeight = 1f / FullPitchStepCount;
        var normalizedHalfHeight = normalizedSliceHeight * 0.5f;
        var pitchDegrees = 90f - sliceIndex * PitchStepDegrees;

        var sliceObject = new GameObject($"PitchCompassSlice_{pitchDegrees:+00;-00;000}_Wrapped", typeof(RectTransform));
        var sliceTransform = sliceObject.GetComponent<RectTransform>();
        sliceTransform.SetParent(root, false);
        sliceTransform.anchorMin = new Vector2(0.5f, 0f);
        sliceTransform.anchorMax = new Vector2(0.5f, 0f);
        sliceTransform.pivot = new Vector2(0.5f, 0.5f);
        sliceTransform.anchoredPosition = Vector2.zero;
        sliceTransform.sizeDelta = new Vector2(source.rectTransform.rect.width, normalizedSliceHeight * height);

        CreateWrappedEndCapHalf(source, sourceTexture, height, sliceTransform, "Top", 0f, 1f - normalizedHalfHeight, normalizedHalfHeight);
        CreateWrappedEndCapHalf(source, sourceTexture, height, sliceTransform, "Bottom", normalizedHalfHeight, 0f, normalizedHalfHeight);

        return sliceObject;
    }

    private static void CreateWrappedEndCapHalf(
        RawImage source,
        Texture sourceTexture,
        float height,
        RectTransform parent,
        string nameSuffix,
        float normalizedLocalBottom,
        float normalizedUvBottom,
        float normalizedHalfHeight)
    {
        var halfObject = new GameObject($"PitchCompassSlice_Wrapped_{nameSuffix}", typeof(RectTransform), typeof(RawImage));
        var halfTransform = halfObject.GetComponent<RectTransform>();
        halfTransform.SetParent(parent, false);
        halfTransform.anchorMin = new Vector2(0f, 0f);
        halfTransform.anchorMax = new Vector2(1f, 0f);
        halfTransform.pivot = new Vector2(0.5f, 0f);
        halfTransform.anchoredPosition = new Vector2(0f, normalizedLocalBottom * height);
        halfTransform.sizeDelta = new Vector2(0f, normalizedHalfHeight * height);

        var halfImage = halfObject.GetComponent<RawImage>();
        halfImage.texture = sourceTexture;
        halfImage.color = source.color;
        halfImage.material = source.material;
        halfImage.raycastTarget = false;
        halfImage.uvRect = new Rect(0f, normalizedUvBottom, 1f, normalizedHalfHeight);
    }
}
