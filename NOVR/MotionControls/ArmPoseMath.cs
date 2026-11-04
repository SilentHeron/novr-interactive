using System;

namespace NOVR.MotionControls;

// Render-only geometry in seat coordinates. No time constant, controller pose,
// flight input or native skeleton is changed by this solver.
internal sealed class ArmPoseMath
{
    internal readonly struct Vector
    {
        internal readonly float X, Y, Z;
        internal Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal float Length => (float)Math.Sqrt(Dot(this, this));
        internal Vector Unit => Length > .00001f ? this * (1 / Length) : default;
        internal bool Finite => FlightControlMath.IsFinite(X) && FlightControlMath.IsFinite(Y) && FlightControlMath.IsFinite(Z);

        public static Vector operator +(Vector a, Vector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector operator -(Vector a, Vector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector operator *(Vector a, float b) => new(a.X * b, a.Y * b, a.Z * b);
        internal static float Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        internal static Vector Cross(Vector a, Vector b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        internal Vector OnPlane(Vector normal) => this - normal * Dot(this, normal);
    }

    private Vector _bend;
    private Vector _transitionFrom;
    private float _roll;
    private float _transitionProgress = 1, _transitionAngle;
    private bool _hasBend, _hasRoll, _hasTransition;
    internal void Reset()
    {
        _hasBend = _hasRoll = _hasTransition = false;
        _transitionProgress = 1;
    }

    internal Vector Bend(bool left, Vector direction, Vector cuff, bool followCuff, float transitionProgress = 1)
    {
        if (!direction.Finite || direction.Length < .001f)
        {
            Reset();
            return default;
        }

        direction = direction.Unit;
        var hint = new Vector(left ? -.35f : .35f, -.5f, -.2f).Unit.OnPlane(direction);
        var previous = _hasBend ? _bend.OnPlane(direction).Unit : default;
        // The anatomical pole can become parallel to an overhead/backward reach.
        // Retain its last usable plane there; this does not delay the wrist.
        if (hint.Length < .15f && previous.Length > .5f)
            hint = Arc(previous, hint.Unit, Smooth(hint.Length / .15f), direction, left);
        if (hint.Length < .001f)
            hint = new Vector(0, 0, -1).OnPlane(direction);
        if (hint.Length < .001f)
            hint = new Vector(left ? -1 : 1, 0, 0).OnPlane(direction);
        hint = hint.Unit;
        var projected = cuff.Finite ? cuff.Unit.OnPlane(direction) : default;
        var confidence = Smooth((projected.Length - .08f) / .17f);
        Vector bend;
        if (followCuff)
        {
            // Collective cuffs point upward toward the shoulder. Keep their
            // aligned elbow, including while the authored glove pose moves.
            // Only an ambiguous cuff/arm axis uses the previous bend plane.
            var fallback = previous.Length > .5f ? previous : hint;
            bend = Arc(fallback, projected.Unit, confidence, direction, left);
        }
        else
        {
            // Wrist flexion must not drag the elbow through the chest. The cuff
            // can guide a compatible bend but cannot reverse the anatomical
            // hemisphere. Weight vanishes smoothly for an opposing cuff.
            var compatible = Smooth((Vector.Dot(hint, projected.Unit) + .2f) / .65f);
            // The shoulder/upper arm primarily determines the elbow plane.
            // An 80% cuff weight let a wrist-only gesture swing the elbow
            // through more than fifty degrees; keep that guidance modest.
            var cuffWeight = .35f * confidence * compatible;
            bend = (hint * (1 - cuffWeight) + projected.Unit * cuffWeight).Unit;
        }

        transitionProgress = FlightControlMath.IsFinite(transitionProgress) ? FlightControlMath.Clamp(transitionProgress, 0, 1) : 1;
        if (transitionProgress < 1 && _hasBend)
        {
            // Use the glove's existing finite attachment/release blend so a
            // collective/free mode change cannot abruptly swap elbow poles.
            // Capturing the starting plane avoids accumulating another filter.
            if (!_hasTransition || transitionProgress < _transitionProgress)
            {
                _transitionFrom = previous.Length > .5f ? previous : hint;
                _transitionAngle = 0;
                _hasTransition = true;
            }

            var from = _transitionFrom.OnPlane(direction).Unit;
            if (from.Length < .5f)
                from = previous.Length > .5f ? previous : hint;
            var angle = (float)(Math.Atan2(Vector.Dot(direction, Vector.Cross(from, bend)), Vector.Dot(from, bend)) * 180 / Math.PI);
            _transitionAngle += DeltaDegrees(_transitionAngle, angle);
            var radians = _transitionAngle * transitionProgress * (float)(Math.PI / 180);
            bend = (from * (float)Math.Cos(radians) + Vector.Cross(direction, from) * (float)Math.Sin(radians)).Unit;
        }
        else
            _hasTransition = false;
        _transitionProgress = transitionProgress;
        _bend = bend;
        _hasBend = true;
        return bend;
    }

    internal float ForearmRoll(Vector axis, Vector sourceDorsal, Vector wristDorsal)
    {
        if (!axis.Finite || axis.Length < .001f || !sourceDorsal.Finite || !wristDorsal.Finite)
            return _hasRoll ? SharedRoll(_roll) : 0;
        axis = axis.Unit;
        var from = sourceDorsal.Unit.OnPlane(axis);
        var to = wristDorsal.Unit.OnPlane(axis);
        var confidence = Smooth((Math.Min(from.Length, to.Length) - .05f) / .2f);
        if (confidence <= 0)
            return _hasRoll ? SharedRoll(_roll) : 0;
        from = from.Unit;
        to = to.Unit;
        var angle = (float)(Math.Atan2(Vector.Dot(axis, Vector.Cross(from, to)), Vector.Dot(from, to)) * 180 / Math.PI);
        // Resolve the nearest angle before blending an ambiguous projection,
        // then wrap history so repeated controller turns cannot wind it up.
        if (_hasRoll)
            angle = _roll + DeltaDegrees(_roll, angle);
        _roll = DeltaDegrees(0, _hasRoll ? _roll + (angle - _roll) * confidence : angle);
        _hasRoll = true;
        return SharedRoll(_roll);
    }

    private static float SharedRoll(float angle)
    {
        // Ordinary pronation shares 80% with the sleeve. At a fully inverted
        // wrist, smoothly reach the whole turn: +180 and -180 are then the
        // same rotation, not the discontinuous +/-144 partial-twist pair.
        return angle * (.8f + .2f * Smooth((Math.Abs(angle) - 90) / 90));
    }

    internal static float DeltaDegrees(float from, float to)
    {
        var delta = (to - from) % 360;
        if (delta > 180)
            delta -= 360;
        if (delta < -180)
            delta += 360;
        return delta;
    }

    private static float Smooth(float value)
    {
        value = FlightControlMath.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    private static Vector Arc(Vector from, Vector to, float amount, Vector axis, bool left)
    {
        if (amount <= 0 || to.Length < .5f)
            return from;
        if (amount >= 1)
            return to;
        var sine = Vector.Dot(axis, Vector.Cross(from, to));
        var cosine = Vector.Dot(from, to);
        // Antipodal vectors need a definite anatomical side, not a zero-vector
        // lerp. This branch only resolves the otherwise undefined bend plane.
        if (cosine < -.9999f && Math.Abs(sine) < .00001f)
            sine = left ? -.00001f : .00001f;
        var angle = (float)Math.Atan2(sine, cosine) * amount;
        return (from * (float)Math.Cos(angle) + Vector.Cross(axis, from) * (float)Math.Sin(angle)).Unit;
    }
}
