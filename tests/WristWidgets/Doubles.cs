using N = System.Numerics;
namespace UnityEngine
{
    internal struct Vector2(float x, float y)
    {
        internal float x = x, y = y;
        public static Vector2 operator *(Vector2 value, float scale) => new(value.x * scale, value.y * scale);
    }
    internal struct Vector3(float x, float y, float z)
    {
        internal float x = x, y = y, z = z;
        internal N.Vector3 N => new(x, y, z);
        private static Vector3 V(N.Vector3 v) => new(v.X, v.Y, v.Z);
        internal static Vector3 zero => default;
        internal static Vector3 forward => new(0, 0, 1);
        internal static Vector3 back => new(0, 0, -1);
        internal static Vector3 left => new(-1, 0, 0);
        internal static Vector3 right => new(1, 0, 0);
        internal float magnitude => N.Length();
        internal Vector3 normalized => magnitude > .00001f ? V(N / magnitude) : zero;
        internal static float Dot(Vector3 a, Vector3 b) => System.Numerics.Vector3.Dot(a.N, b.N);
        public static Vector3 operator +(Vector3 a, Vector3 b) => V(a.N + b.N);
        public static Vector3 operator -(Vector3 a, Vector3 b) => V(a.N - b.N);
        public static Vector3 operator -(Vector3 v) => V(-v.N);
        public static Vector3 operator *(Vector3 v, float a) => V(v.N * a);
    }
    internal struct Quaternion
    {
        private N.Quaternion _n;
        private Quaternion(N.Quaternion n) => _n = n;
        internal static Quaternion Slerp(Quaternion a, Quaternion b, float t) => new(N.Quaternion.Slerp(a._n, b._n, t));
        internal static Quaternion Euler(float x, float y, float z) => Euler(new Vector3(x, y, z));
        internal static Quaternion identity => new(N.Quaternion.Identity);
        internal static Quaternion Inverse(Quaternion q) => new(N.Quaternion.Inverse(q._n));
        internal static Quaternion Euler(Vector3 e) => new(N.Quaternion.CreateFromYawPitchRoll(e.y * MathF.PI / 180, e.x * MathF.PI / 180, e.z * MathF.PI / 180));
        internal Vector3 eulerAngles
        {
            get
            {
                var q = _n;
                var pitch = MathF.Asin(Math.Clamp(2 * (q.W * q.X - q.Y * q.Z), -1, 1));
                var yaw = MathF.Atan2(2 * (q.W * q.Y + q.X * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
                var roll = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.X * q.X + q.Z * q.Z));
                return new(pitch * 180 / MathF.PI, yaw * 180 / MathF.PI, roll * 180 / MathF.PI);
            }
        }
        internal static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var z = N.Vector3.Normalize(forward.N);
            var x = N.Vector3.Normalize(N.Vector3.Cross(up.N, z));
            var y = N.Vector3.Cross(z, x);
            return new(N.Quaternion.CreateFromRotationMatrix(new N.Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1)));
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(a._n * b._n);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var n = N.Vector3.Transform(v.N, q._n);
            return new(n.X, n.Y, n.Z);
        }
    }
    internal sealed class Transform
    {
        internal Vector3 position = Vector3.zero;
        internal Quaternion rotation = Quaternion.identity;
        internal Vector3 forward => rotation * Vector3.forward;
        internal Vector3 TransformPoint(Vector3 point) => position + rotation * point;
    }
    internal sealed class Camera
    {
        internal Transform transform = new();
    }
    internal static class Time
    {
        internal static float unscaledTime;
    }
    internal static class Mathf
    {
        internal static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
    }
}
namespace NOVR.MotionControls
{
    internal struct ControllerSample
    {
        internal bool Valid; internal UnityEngine.Vector3 Position; internal UnityEngine.Quaternion Rotation;
        internal float Grip;
        internal int Device;
    }
    internal static class InteractionHaptics
    {
        internal static int Pulses;
        internal static void PulseOnly(int device, float strength, float seconds) => Pulses++;
    }
    internal static class CockpitLayout
    {
        internal enum Element
        {
            None, Stick
        }
        internal static Element Left, Right;
        internal static Element HeldByHand(bool left) => left ? Left : Right;
    }
    internal static class TrackedControllers
    {
        internal static ControllerSample Left, Right;
        internal static ControllerSample Read(bool left) => left ? Left : Right;
    }
    internal static class PilotHandVisuals
    {
        internal static UnityEngine.Quaternion FreeHandRotation(bool left, ControllerSample sample, UnityEngine.Transform tracking) => tracking.rotation * sample.Rotation;
    }
    internal static class NativeHandOptionWheel
    {
        internal static bool Left, Right;
        internal static bool OwnsHand(bool left) => left ? Left : Right;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal static class APIBus
    {
        internal static UnityEngine.Camera? MainCamera = new();
    }
    internal static class WristMenu
    {
        internal static bool IsOpen;
    }
    internal sealed partial class CockpitHudWidgets
    {
        private sealed class Appearance
        {
            internal bool IsHandBusy(int side) => false; internal void ReleaseHands()
            {
            }
        }
        private sealed class Widget
        {
            internal readonly HudWidgetWristVisibility WristVisibility = new();
            internal readonly Appearance Appearance = new();
            internal bool WristFrozen;
            internal UnityEngine.Vector3 WristOrigin;
            internal UnityEngine.Quaternion WristRotation;
            internal float WristLastTime;
        }
        private sealed class Hand
        {
            internal string? Id;
            internal readonly WristDockGesture Dock = new();
            internal UnityEngine.Vector3 StartEuler, LocalAnchor;
            internal UnityEngine.Quaternion GripRotation = UnityEngine.Quaternion.identity;
            internal readonly HandGate Gate = new();
        }
        private sealed class HandGate
        {
            internal bool Held;
        }
        private const float PixelSize = HudWidgetCatalog.MetresPerPixel;
        private Dictionary<string, HudWidgetPose> _poses = HudWidgetProfiles.FactoryDefaults;
        private readonly Dictionary<string, Widget> _widgets = HudWidgetProfiles.Ids.ToDictionary(x => x, _ => new Widget());
        private readonly Hand[] _hands = { new(), new() };
        private readonly NOVR.MotionControls.ControllerSample[] _samples = new NOVR.MotionControls.ControllerSample[2];
        private readonly UnityEngine.Vector3[] _points = new UnityEngine.Vector3[2];
        private readonly UnityEngine.Quaternion[] _handRotations = { UnityEngine.Quaternion.identity, UnityEngine.Quaternion.identity };
        private readonly bool[] _appearanceBusy = new bool[2];
        private UnityEngine.Vector2 Size(string id) => HudWidgetShape.Size(HudWidgetCatalog.Get(id).Size, _poses[id].Aspect);
        internal bool _editing = true;
        internal readonly UnityEngine.Transform Tracking = new(), Eye = new();
        private bool Space(out UnityEngine.Transform gameRoot, out UnityEngine.Transform uiRoot, out UnityEngine.Transform eye)
        {
            gameRoot = uiRoot = Tracking;
            eye = Eye;
            return true;
        }
        private void ReleaseHands()
        {
            foreach (var hand in _hands)
            {
                hand.Dock.Reset();
                hand.Gate.Held = false;
                hand.Id = null;
            }
            ReleaseWristFrames();
        }
        private void Select(string id)
        {
        }
        private void Place()
        {
        }
        internal HudWidgetPose Stored(string id) => _poses[id];
        internal HudWidgetPose Active(string id) => ActivePose(id);
        internal void Edit(string id, HudWidgetPose pose) => StoreActivePose(id, pose);
        internal (UnityEngine.Vector3 position, UnityEngine.Quaternion rotation) Frame(string id)
        {
            TryWidgetFrame(id, out var p, out var q);
            return (p, q);
        }
        internal void Freeze(string id)
        {
            FreezeWrist(id);
            _hands[1].Id = id;
        }
        internal void CancelUntracked() => CancelUntrackedWristEdits();
        internal bool HasContact => _hands.Any(h => h.Id != null);
        internal bool FrameReady(string id) => TryWidgetFrame(id, out _, out _);
        internal void Release() => ReleaseHands();
        internal UnityEngine.Vector3 Point(string id, UnityEngine.Vector3 point) => WidgetPoint(id, point);
        internal UnityEngine.Quaternion Rotation(string id, UnityEngine.Quaternion rotation) => WidgetRotation(id, rotation);
        internal float Alpha(string id)
        {
            var frame = Frame(id);
            var pose = ActivePose(id);
            return WristAlpha(id, _widgets[id], frame.position + frame.rotation * pose.Position, frame.rotation * UnityEngine.Quaternion.Euler(pose.Euler));
        }
    }
}
