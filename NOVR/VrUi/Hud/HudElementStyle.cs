using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

internal enum HudElementKind
{
    Marker, Label, Reticle, Weapon, HorizontalLine, VerticalLine, WorldGeometry
}

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
    // The size settings every scale is derived from. They are the same for all
    // elements, and each unit marker carries a styled graphic, so the per-frame
    // refresh reads them once (RefreshAll) instead of once per element.
    private readonly struct Factors
    {
        internal readonly float Marker, Label, Reticle, Weapon, FlightHud, Follow;
        internal readonly bool Vr;
        private Factors(ModConfiguration config, bool vr)
        {
            // Symbols default to their own sliders; with the follow option they also
            // track Flight HUD Scale relative to its default, so one slider resizes
            // instruments and world-aligned symbols together (unlike the native
            // per-distance marker scale, which stays untouched underneath).
            Follow = HudPresentationMath.SymbolFollowFactor(
                config.SymbolsFollowFlightHud.Value, config.FocusedFlightHud.Value, config.FlightHudScale.Value);
            Marker = config.MarkerScale.Value;
            Label = config.MarkerLabelScale.Value;
            Reticle = config.ReticleScale.Value;
            // Range/guidance widgets follow the same master scale as the native
            // instrument layout. Keep their existing size control as a relative
            // trim (the default .5 is neutral), without touching aim projections.
            Weapon = config.WeaponHudScale.Value / .5f;
            FlightHud = config.FocusedFlightHud.Value ? config.FlightHudScale.Value : 1f;
            Vr = vr;
        }
        internal static Factors Read() => new(ModConfiguration.Instance, XRSettings.enabled);
    }
    private static Factors _frame;

    // One formula for both uses: the live value when a mesh is built, and the
    // per-frame change test against the factors RefreshAll read.
    private float ScaleFrom(in Factors factors) => Kind switch
    {
        HudElementKind.Marker => factors.Marker * factors.Follow,
        HudElementKind.Label => (_aircraftTargetLabel
            ? HudPresentationMath.AircraftLabelScale(factors.Label, _aircraftLabelAffiliation, factors.Vr)
            : factors.Label) * factors.Follow,
        HudElementKind.Weapon => factors.Weapon * (_flightScaleInherited ? 1f : factors.FlightHud),
        HudElementKind.WorldGeometry => 1,
        _ => factors.Reticle * factors.Follow
    };
    internal float Scale => ScaleFrom(Factors.Read());
    internal Vector3 Anchor => Group == null ? Vector3.zero : transform.InverseTransformPoint(Group.position);
    internal static HudElementStyle? Add(Graphic? graphic, HudElementKind kind, Transform? group = null)
    {
        if (graphic == null)
            return null;
        var style = graphic.GetComponent<HudElementStyle>();
        if (style == null)
            style = graphic.gameObject.AddComponent<HudElementStyle>();
        style.Kind = kind;
        style.Group = group;
        // Cargo readouts live under the already-scaled HMD; missile/laser
        // widgets live directly under HUDCenter and need the master factor here.
        style._flightScaleInherited = kind == HudElementKind.Weapon &&
            (graphic.GetComponentInParent<NOVRHMDBehavior>() != null || graphic.GetComponentInParent<HUDAppManager>() != null);
        return style;
    }
    internal void SetAircraftLabelAffiliation(AircraftLabelAffiliation affiliation)
    {
        if (_aircraftTargetLabel && _aircraftLabelAffiliation == affiliation)
            return;
        _aircraftTargetLabel = true;
        _aircraftLabelAffiliation = affiliation;
        // Selection can change after this component's LateUpdate. Invalidate
        // immediately so the shared target text uses the new faction this frame.
        if (_text != null)
            _text.havePropertiesChanged = true;
        else
            graphic.SetVerticesDirty();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        Elements.Add(this);
        _text = GetComponent<TMP_Text>();
        if (_text != null)
            _text.OnPreRenderText += StyleText;
        _lastScale = -1;
    }
    protected override void OnDisable()
    {
        if (_text != null)
        {
            _text.OnPreRenderText -= StyleText;
            _text.havePropertiesChanged = true;
        }
        Elements.Remove(this);
        base.OnDisable();
    }
    // Driven from one place (HudElementStyleDriver) rather than a LateUpdate
    // on every styled graphic: each unit marker carries one of these, and a
    // per-component engine callback for hundreds of them adds up.
    internal static void RefreshAll()
    {
        _frame = Factors.Read();
        foreach (var element in Elements)
            element.Refresh();
    }
    private void Refresh()
    {
        var anchor = Anchor;
        var scale = ScaleFrom(_frame);
        if (scale == _lastScale && (anchor - _lastAnchor).sqrMagnitude < .0001f)
            return;
        _lastScale = scale;
        _lastAnchor = anchor;
        if (_text != null)
            _text.havePropertiesChanged = true;
        else
            graphic.SetVerticesDirty();
    }
    private Vector3 Style(Vector3 vertex, Vector3 anchor, float scale)
    {
        var delta = vertex - anchor;
        if (Kind != HudElementKind.HorizontalLine)
            delta.x = HudPresentationMath.ScaleCoordinate(vertex.x, anchor.x, scale) - anchor.x;
        if (Kind != HudElementKind.VerticalLine)
            delta.y = HudPresentationMath.ScaleCoordinate(vertex.y, anchor.y, scale) - anchor.y;
        return anchor + delta;
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || _text != null)
            return;
        var anchor = Anchor;
        var scale = Scale;
        var v = new UIVertex();
        for (var i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref v, i);
            v.position = Style(v.position, anchor, scale);
            vertices.SetUIVertex(v, i);
        }
    }
    private void StyleText(TMP_TextInfo info)
    {
        if (!IsActive())
            return;
        var anchor = Anchor;
        var scale = Scale;
        for (var m = 0; m < info.meshInfo.Length; m++)
        {
            var mesh = info.meshInfo[m];
            for (var i = 0; i < mesh.vertexCount; i++)
                mesh.vertices[i] = Style(mesh.vertices[i], anchor, scale);
        }
    }
}

// Keeps every styled HUD graphic in step with the size settings once a frame.
internal sealed class HudElementStyleDriver : MonoBehaviour
{
    private void LateUpdate() => HudElementStyle.RefreshAll();
}
