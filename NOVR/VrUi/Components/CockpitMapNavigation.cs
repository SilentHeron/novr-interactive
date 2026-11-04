using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// The live native map still creates and updates terrain, sensors and icons.
// While selected, only its view transform is held/panned in the cockpit frame.
internal sealed class CockpitMapNavigation : IDisposable
{
    private readonly struct Pose
    {
        private readonly Transform? _transform, _parent;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        internal Pose(Transform? transform)
        {
            _transform = transform; _parent = transform != null ? transform.parent : null;
            _position = transform != null ? transform.localPosition : Vector3.zero;
            _scale = transform != null ? transform.localScale : Vector3.one;
            _rotation = transform != null ? transform.localRotation : Quaternion.identity;
        }
        internal void Restore()
        {
            if (_transform == null || _transform.parent != _parent) return;
            _transform.localPosition = _position; _transform.localScale = _scale; _transform.localRotation = _rotation;
        }
    }

    private static CockpitMapNavigation? _owner;
    private readonly DynamicMap _map;
    private readonly RectTransform _viewport;
    private readonly Transform _image;
    private readonly Pose _imagePose, _zoomPose, _proxyPose;
    private Vector3 _center;
    private Quaternion _heading;
    private bool _selected, _disposed;

    internal CockpitMapNavigation(DynamicMap map, RectTransform viewport)
    {
        _owner?.Dispose();
        _map = map; _viewport = viewport; _image = map.mapImage.transform;
        _imagePose = new Pose(_image); _zoomPose = new Pose(map.mapScaleCenter); _proxyPose = new Pose(map.mapScaleProxy);
        _owner = this;
    }

    internal bool Valid => !_disposed && _map != null && _image != null && _viewport != null &&
        _map.mapScaleCenter != null && _map.transform.parent == _viewport && !DynamicMap.mapMaximized;
    internal bool Selected => _selected && CanNavigate;
    private bool CanNavigate => Valid && _map.gameObject.activeInHierarchy &&
        CockpitHudWidgets.IsVisible("map") && !CockpitHudWidgets.Editing;
    private Vector3 CenterWorld => _viewport.TransformPoint(_viewport.rect.center);

    internal bool Activate()
    {
        if (!CanNavigate) return false;
        if (_selected) return true;
        _center = _image.InverseTransformPoint(CenterWorld);
        _heading = _image.localRotation;
        _selected = true;
        return true;
    }

    // The game's normal ownship-follow resumes on its next map update.
    internal void Deactivate() => _selected = false;

    internal bool Zoom(int direction)
    {
        if (!CanNavigate || direction == 0) return false;
        var previous = _map.GetZoomLevel();
        var zoom = Mathf.Clamp(previous * (direction > 0 ? 1.3f : 1f / 1.3f), 1f, 12f);
        if (!Finite(previous) || previous <= 0 || Mathf.Approximately(previous, zoom)) return false;
        // The existing SetZoomLevel prefix delegates back to SetZoom below and
        // still invokes the native change event. Never use its desktop pivot.
        _map.SetZoomLevel(zoom);
        return true;
    }

    internal void Pan(Vector2 axis, float deltaTime)
    {
        if (!Selected || !Finite(axis.x) || !Finite(axis.y) || !Finite(deltaTime)) return;
        var magnitude = axis.magnitude;
        if (magnitude <= .0001f || deltaTime <= 0) return;
        // The controlling hand's input gate already applies its deadzone.
        if (magnitude > 1) axis /= magnitude;
        var pixels = Mathf.Min(_viewport.rect.width, _viewport.rect.height) * .65f * Mathf.Min(deltaTime, .05f);
        var worldDelta = _viewport.TransformVector((Vector3)(axis * pixels));
        _center += _image.InverseTransformVector(worldDelta);
        if (_image is RectTransform terrain)
        {
            _center.x = Mathf.Clamp(_center.x, terrain.rect.xMin, terrain.rect.xMax);
            _center.y = Mathf.Clamp(_center.y, terrain.rect.yMin, terrain.rect.yMax);
        }
        _center.z = 0;
        Tick();
    }

    internal void Tick()
    {
        if (!CanNavigate) { Deactivate(); return; }
        if (!_selected) return;
        _image.localRotation = _heading;
        _image.position += CenterWorld - _image.TransformPoint(_center);
        UpdateViewIndicator();
    }

    private void UpdateViewIndicator()
    {
        var camera = SceneSingleton<CameraStateManager>.i;
        if (camera == null || _map.viewIndicator == null) return;
        var position = camera.transform.position.ToGlobalPosition().AsVector3() * _map.mapDisplayFactor;
        var forward = APIBus.MainCamera != null ? APIBus.MainCamera.transform.forward : camera.transform.forward;
        forward.y = 0;
        var indicator = _map.viewIndicator.transform;
        indicator.localPosition = new Vector3(position.x, position.z, 0);
        if (forward.sqrMagnitude > .000001f)
        {
            var yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
            indicator.rotation = _image.rotation * Quaternion.Euler(0, 0, -yaw);
        }
    }

    internal bool SelectCenter()
    {
        if (!Selected) return false;
        var hud = SceneSingleton<CombatHUD>.i;
        var selector = SceneSingleton<TargetListSelector>.i;
        if (hud == null || hud.aircraft == null || hud.aircraft.disabled || hud.aircraft.weaponManager == null ||
            selector == null || _map.mapIcons == null) return false;
        UnitMapIcon? nearest = null;
        var radius = Mathf.Min(_viewport.rect.width, _viewport.rect.height) * .075f;
        var distance = radius * radius;
        foreach (var icon in _map.mapIcons)
        {
            if (!(icon is UnitMapIcon unitIcon) || unitIcon == null || unitIcon.unit == null || unitIcon.unit == hud.aircraft ||
                !unitIcon.gameObject.activeInHierarchy || unitIcon.iconImage == null || !unitIcon.iconImage.enabled ||
                !unitIcon.iconImage.gameObject.activeInHierarchy || !unitIcon.iconImage.raycastTarget || unitIcon.iconImage.color.a <= .01f ||
                selector.CheckExclusions(unitIcon.unit) || !hud.MarkerExists(unitIcon.unit)) continue;
            // Use the rendered sensor/jamming position, never a hidden unit's
            // true world position. The native marker must already exist.
            var point = (Vector2)_viewport.InverseTransformPoint(unitIcon.iconImage.transform.position);
            if (!_viewport.rect.Contains(point)) continue;
            var squared = (point - _viewport.rect.center).sqrMagnitude;
            if (squared > distance) continue;
            nearest = unitIcon; distance = squared;
        }
        if (nearest == null) return false;
        if (!_map.selectedIcons.Contains(nearest)) hud.SelectUnit(nearest.unit);
        return hud.aircraft.weaponManager.GetTargetList().Contains(nearest.unit);
    }

    internal static bool TryCenter(DynamicMap map)
    {
        if (_owner == null || _owner._map != map || !_owner.Selected) return false;
        _owner.Tick(); return true;
    }

    internal static bool TrySetZoom(DynamicMap map, float zoom)
    {
        if (_owner == null || _owner._map != map || !_owner.Valid) return false;
        _owner.SetZoom(zoom); return true;
    }

    private void SetZoom(float zoom)
    {
        var previous = _map.GetZoomLevel();
        if (!Finite(zoom) || !Finite(previous) || previous <= 0) return;
        var anchor = CenterWorld;
        var local = _image.InverseTransformPoint(anchor);
        _image.localScale *= Mathf.Clamp(zoom, 1, 12) / previous;
        _map.mapScaleCenter.localScale = Vector3.one * Mathf.Clamp(zoom, 1, 12);
        _image.position += anchor - _image.TransformPoint(local);
        if (_selected) Tick();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _selected = false;
        if (_owner == this) _owner = null;
        _imagePose.Restore(); _zoomPose.Restore(); _proxyPose.Restore();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
