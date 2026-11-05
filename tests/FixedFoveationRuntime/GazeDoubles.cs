using Numerics = System.Numerics;

namespace UnityEngine
{
    public sealed class Transform
    {
        public Transform? parent;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 position;
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 forward => new(0, 0, 1);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        private Numerics.Quaternion Value => new(x, y, z, w);
        private static Quaternion From(Numerics.Quaternion value) => new(value.X, value.Y, value.Z, value.W);
        public static Quaternion Inverse(Quaternion value) => From(Numerics.Quaternion.Inverse(value.Value));
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.Value * b.Value);
        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            var value = Numerics.Vector3.Transform(new Numerics.Vector3(point.x, point.y, point.z), rotation.Value);
            return new Vector3(value.X, value.Y, value.Z);
        }
        internal static Quaternion Angles(float yaw, float pitch, float roll) =>
            From(Numerics.Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll));
    }
}

namespace UnityEngine.XR.OpenXR
{
    public enum NovrEyeGazeReadState
    {
        Unavailable, Unsupported, Unfocused, Untracked, Stale, Invalid, Tracked
    }
    public readonly struct NovrEyeGazePose(Vector3 position, Quaternion rotation, Vector3 headPosition, Quaternion headRotation, double ageSeconds = 0)
    {
        public readonly Vector3 Position = position, HeadPosition = headPosition;
        public readonly Quaternion Rotation = rotation, HeadRotation = headRotation;
        public readonly double AgeSeconds = ageSeconds;
    }
    // Input boundary double: preserves the production reader's same-frame
    // contract. EyeGazeChecks separately exercises the actual input reader.
    public static class NovrEyeGaze
    {
        internal static int Calls, Samples;
        internal static bool ThrowRead;
        internal static NovrEyeGazeReadState State;
        internal static NovrEyeGazePose Pose;
        private static int _frame = -1;
        private static NovrEyeGazeReadState _sampledState;
        private static NovrEyeGazePose _sampledPose;
        public static NovrEyeGazeReadState Read(out NovrEyeGazePose pose)
        {
            Calls++;
            if (ThrowRead)
                throw new InvalidOperationException("fixture eye input failure");
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                Samples++;
                _sampledState = State;
                _sampledPose = State == NovrEyeGazeReadState.Tracked ? Pose : default;
            }
            pose = _sampledPose;
            return _sampledState;
        }
        internal static void Reset()
        {
            Calls = Samples = 0;
            ThrowRead = false;
            _frame = -1;
            State = NovrEyeGazeReadState.Tracked;
            Pose = new NovrEyeGazePose(default, Quaternion.identity, default, Quaternion.identity);
            _sampledPose = default;
        }
    }
}
