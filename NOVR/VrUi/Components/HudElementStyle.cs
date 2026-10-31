using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

internal enum HudElementKind { Marker, Label, Reticle, Weapon, HorizontalLine, VerticalLine, WorldGeometry }

// Change drawn vertices, not transforms used by the game's aim/lead calculations.
// TMP generates its own meshes, so handle it at its pre-render callback instead
// of scaling its RectTransform or compounding changes to last frame's vertices.
internal sealed class HudElementStyle : BaseMeshEffect
{
    internal static readonly HashSet<HudElementStyle> Elements = new();
    internal HudElementKind Kind;
    internal Transform? Group;
    internal Graphic Source => graphic;
    private TMP_Text? _text;
    private float _lastScale = -1;
    private Vector3 _lastAnchor;
    private bool _flightScaleInherited;
    private bool _aircraftTargetLabel;
    private AircraftLabelAffiliation _aircraftLabelAffiliation;
    internal float Scale => Kind switch
    {
        HudElementKind.Marker => ModConfiguration.Instance.MarkerScale.Value,
        HudElementKind.Label => _aircraftTargetLabel
            ? HudPresentationMath.AircraftLabelScale(ModConfiguration.Instance.MarkerLabelScale.Value, _aircraftLabelAffiliation, XRSettings.enabled)
            : ModConfiguration.Instance.MarkerLabelScale.Value,
        // Range/guidance widgets follow the same master scale as the native
        // instrument layout. Keep their existing size control as a relative
        // trim (the default .5 is neutral), without touching aim projections.
        HudElementKind.Weapon => ModConfiguration.Instance.WeaponHudScale.Value / .5f *
            (ModConfiguration.Instance.FocusedFlightHud.Value && !_flightScaleInherited ? ModConfiguration.Instance.FlightHudScale.Value : 1f),
        HudElementKind.WorldGeometry => 1,
        _ => ModConfiguration.Instance.ReticleScale.Value
    };
    internal Vector3 Anchor => Group == null ? Vector3.zero : transform.InverseTransformPoint(Group.position);
    internal static HudElementStyle? Add(Graphic? graphic, HudElementKind kind, Transform? group = null)
    {
        if (graphic == null) return null;
        var style = graphic.GetComponent<HudElementStyle>();
        if (style == null) style = graphic.gameObject.AddComponent<HudElementStyle>();
        style.Kind = kind; style.Group = group;
        // Cargo readouts live under the already-scaled HMD; missile/laser
        // widgets live directly under HUDCenter and need the master factor here.
        style._flightScaleInherited = kind == HudElementKind.Weapon &&
            (graphic.GetComponentInParent<NOVRHMDBehavior>() != null || graphic.GetComponentInParent<HUDAppManager>() != null);
        return style;
    }
    internal void SetAircraftLabelAffiliation(AircraftLabelAffiliation affiliation)
    {
        if (_aircraftTargetLabel && _aircraftLabelAffiliation == affiliation) return;
        _aircraftTargetLabel = true;
        _aircraftLabelAffiliation = affiliation;
        // Selection can change after this component's LateUpdate. Invalidate
        // immediately so the shared target text uses the new faction this frame.
        if (_text != null) _text.havePropertiesChanged = true;
        else graphic.SetVerticesDirty();
    }
    protected override void OnEnable()
    {
        base.OnEnable(); Elements.Add(this);
        _text = GetComponent<TMP_Text>();
        if (_text != null) _text.OnPreRenderText += StyleText;
        _lastScale = -1;
    }
    protected override void OnDisable()
    {
        if (_text != null) { _text.OnPreRenderText -= StyleText; _text.havePropertiesChanged = true; }
        Elements.Remove(this); base.OnDisable();
    }
    private void LateUpdate()
    {
        var anchor = Anchor; var scale = Scale;
        if (scale == _lastScale && (anchor - _lastAnchor).sqrMagnitude < .0001f) return;
        _lastScale = scale; _lastAnchor = anchor;
        if (_text != null) _text.havePropertiesChanged = true;
        else graphic.SetVerticesDirty();
    }
    private Vector3 Style(Vector3 vertex)
    {
        var anchor = Anchor; var delta = vertex - anchor; var scale = Scale;
        if (Kind != HudElementKind.HorizontalLine) delta.x = HudPresentationMath.ScaleCoordinate(vertex.x, anchor.x, scale) - anchor.x;
        if (Kind != HudElementKind.VerticalLine) delta.y = HudPresentationMath.ScaleCoordinate(vertex.y, anchor.y, scale) - anchor.y;
        return anchor + delta;
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || _text != null) return;
        var v = new UIVertex();
        for (var i = 0; i < vertices.currentVertCount; i++)
        { vertices.PopulateUIVertex(ref v, i); v.position = Style(v.position); vertices.SetUIVertex(v, i); }
    }
    private void StyleText(TMP_TextInfo info)
    {
        if (!IsActive()) return;
        for (var m = 0; m < info.meshInfo.Length; m++)
        {
            var mesh = info.meshInfo[m];
            for (var i = 0; i < mesh.vertexCount; i++) mesh.vertices[i] = Style(mesh.vertices[i]);
        }
    }
}
