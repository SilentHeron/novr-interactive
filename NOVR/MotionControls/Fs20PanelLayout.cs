using UnityEngine;

namespace NOVR.MotionControls;

// Measured native FS-20 switch rows; coordinates are fixed to cockpit_int.
internal static class Fs20PanelLayout
{
    internal enum Kind { Page, Switch, Hold, Setup, MapSlave, Softkey }
    internal sealed class Control
    {
        internal readonly string Id, Label;
        internal readonly Vector3 Point;
        internal readonly Vector2 Size;
        internal readonly Kind Type;
        internal readonly Quaternion Rotation;
        internal Control(string id, string label, Vector3 point, Vector2 size, Kind type, bool side = false)
        { Id = id; Label = label; Point = point; Size = size; Type = type; Rotation = side ? Quaternion.Euler(90,0,0) : Face; }
    }
    internal static readonly Quaternion Face = Quaternion.Euler(12, 0, 0);
    internal static readonly Vector3 Normal = Face * Vector3.back;
    // Keys 1/2 flank the left edge, 3/4 the right edge of each display.
    internal static readonly Control[][] MfdKeys =
    {
        new[] {
            new Control("mfd_left_0", "1", new(-0.270000f,0.510000f,0.573528f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_left_1", "2", new(-0.270000f,0.415000f,0.553638f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_left_2", "3", new(-0.020000f,0.510000f,0.570657f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_left_3", "4", new(-0.020000f,0.415000f,0.550767f), new(.032f,.030f), Kind.Softkey)
        },
        new[] {
            new Control("mfd_right_0", "1", new(0.020000f,0.510000f,0.570657f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_right_1", "2", new(0.020000f,0.415000f,0.550767f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_right_2", "3", new(0.270000f,0.510000f,0.573528f), new(.032f,.030f), Kind.Softkey),
            new Control("mfd_right_3", "4", new(0.270000f,0.415000f,0.553638f), new(.032f,.030f), Kind.Softkey)
        }
    };
    internal static bool TrySoftkey(string? id, out bool left, out int key)
    {
        left = id?.StartsWith("mfd_left_") == true;
        key = id != null && (left || id.StartsWith("mfd_right_")) ? id[id.Length - 1] - '0' : -1;
        return key >= 0 && key < MfdContextControls.KeyCount && id == (left ? "mfd_left_" : "mfd_right_") + key;
    }
    internal static readonly Control[] Controls =
    {
        new("gear", "GEAR", Fs20NativeGear.NativePoint, new(.040f,.055f), Kind.Switch),
        new("brake", "BRAKE", new(-.051f,.196f,.498f), new(.074f,.049f), Kind.Hold),
        new("radar", "RADAR", new(.051f,.196f,.498f), new(.074f,.049f), Kind.Switch),
        new("aots", "EOTS HUD", new(.153f,.196f,.498f), new(.074f,.049f), Kind.Switch),
        new("left_lock", "THROTTLE POS", new(-.48f,.08f,.09f), new(.108f,.045f), Kind.Setup, true),
        new("right_lock", "STICK POS", new(.48f,.08f,.09f), new(.108f,.045f), Kind.Setup, true),
        new("seat_up", "▲ UP", new(-.507f,.08f,.025f), new(.048f,.045f), Kind.Setup, true),
        new("seat_down", "▼ DOWN", new(-.453f,.08f,.025f), new(.048f,.045f), Kind.Setup, true),
        new("seat_left", "◀ LEFT", new(-.507f,.08f,-.027f), new(.048f,.045f), Kind.Setup, true),
        new("seat_right", "RIGHT ▶", new(-.453f,.08f,-.027f), new(.048f,.045f), Kind.Setup, true),
        new("seat_forward", "▲ FWD", new(-.507f,.08f,-.079f), new(.048f,.045f), Kind.Setup, true),
        new("seat_back", "▼ BACK", new(-.453f,.08f,-.079f), new(.048f,.045f), Kind.Setup, true),
    };
}
