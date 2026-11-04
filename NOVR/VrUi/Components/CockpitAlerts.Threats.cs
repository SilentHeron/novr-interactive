using NOVR.VrUi.HarmonyPatches;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed partial class CockpitAlerts
{
    private void RefreshThreats()
    {
        if (_aircraft == null) return;
        _missiles.Clear();
        if (_list != null)
            foreach (var entry in Items(_list))
                if (entry.Value != null && MissileOf(entry.Value) != null) _missiles.Add(entry.Value);
        _missiles.Sort(CompareThreats);
        var now = Time.timeSinceLevelLoad;
        _signals.RemoveAll(s => s.Source == null || s.Expires <= now);
        _threatRoot!.SetActive(_missiles.Count > 0 || _signals.Count > 0);
        _heading!.text = _missiles.Count > 0 ? "THREAT BEARINGS" : "RADAR / EW";
        _heading.color = RadarColor;
        _missileHeading!.text = $"INCOMING  {_missiles.Count}" + (_missiles.Count > 3 ? $"  ·  +{_missiles.Count - 3} MORE" : "");
        _missileHeading.color = MissileColor;
        var pinIndex = 0;
        for (var i = 0; i < _missiles.Count; i++)
        {
            var item = _missiles[i]; var missile = MissileOf(item);
            var direction = _aircraft.transform.InverseTransformDirection(missile.transform.position - _aircraft.transform.position);
            Pin(ref pinIndex, direction, "M", MissileColor, 70);
            if (i >= 3) continue;
            var height = direction.y > direction.magnitude * .2f ? " HIGH" : direction.y < -direction.magnitude * .2f ? " LOW" : "";
            var text = $"{missile.GetSeekerType()}  {CockpitAlertMath.Clock(direction.x, direction.z)}h{height}  {UnitConverter.DistanceReading(direction.magnitude)}";
            if (ThreatItemNotchDirectionPatch.TryGetNotchDirection(item, out var notch) && missile.GetSeekerType().Contains("RH"))
            {
                notch = _aircraft.transform.InverseTransformDirection(notch);
                text += $"\nNOTCH {CockpitAlertMath.Clock(notch.x, notch.z)}h";
            }
            _rows[i].text = text; _rows[i].color = MissileColor;
        }
        // Retain used rows in place; blanking them first rebuilds unchanged text.
        for (var i = _missiles.Count; i < 3; i++) _rows[i].text = "";
        var radar = 0; var jamming = 0;
        foreach (var signal in _signals)
        {
            var direction = _aircraft.transform.InverseTransformDirection(signal.Direction);
            var color = signal.Jam ? JamColor : RadarColor;
            color.a = Mathf.Clamp01((signal.Expires - now) / (signal.Jam ? 1f : 4f));
            Pin(ref pinIndex, direction, signal.Jam ? "J" : "R", color, signal.Jam ? 88 : 52);
            if (signal.Jam) jamming++; else radar++;
        }
        // Only disappearing pins need disabling. Cycling all used pins through
        // OnDisable/OnEnable clears their meshes and dirties the canvas.
        for (var i = pinIndex; i < _pins.Count; i++)
            if (_pins[i].enabled) _pins[i].enabled = false;
        _rows[3].text = $"RADAR {radar}  ·  JAM {jamming}";
        _rows[3].color = RadarColor;
    }

    private int CompareThreats(ThreatItem a, ThreatItem b)
    {
        var first = MissileOf(a); var second = MissileOf(b);
        var priority = (second.seekerMode == Missile.SeekerMode.activeLock).CompareTo(first.seekerMode == Missile.SeekerMode.activeLock);
        return priority != 0 ? priority : (first.transform.position - _aircraft!.transform.position).sqrMagnitude.CompareTo((second.transform.position - _aircraft!.transform.position).sqrMagnitude);
    }
    private void Pin(ref int index, Vector3 direction, string symbol, Color color, float radius)
    {
        if (index >= _pins.Count) return;
        var pin = _pins[index++]; var p = CockpitAlertMath.Scope(direction.x, direction.z, radius);
        pin.rectTransform.anchoredPosition = new Vector2(p.x, p.y) * .7f;
        pin.text = symbol; pin.color = color;
        if (!pin.enabled) pin.enabled = true;
    }
}
