namespace NOVR.MotionControls;

// One physical touch produces one seat step. Coordinates exclude SeatOffset,
// so moving the virtual seat cannot count as withdrawing the real controller.
internal sealed class SeatTouchGate
{
    private bool _blocked, _rebase;
    private float _x, _y, _z, _nx, _ny, _nz;
    private float _nextPress = float.NegativeInfinity;

    internal void Observe(bool tracked, float x, float y, float z)
    {
        if (!tracked || !Finite(x,y,z)) { Suspend(); return; }
        if (!_blocked) return;
        if (_rebase) { _x=x; _y=y; _z=z; _rebase=false; return; }
        var dx=x-_x; var dy=y-_y; var dz=z-_z;
        var outward=dx*_nx+dy*_ny+dz*_nz;
        var sideways=dx*dx+dy*dy+dz*dz-outward*outward;
        if (outward>=.04f || sideways>=.06f*.06f) _blocked=false;
    }

    internal bool TryPress(float x, float y, float z, float nx, float ny, float nz, float time)
    {
        if (_blocked || !Finite(x,y,z) || !Finite(nx,ny,nz) || !FlightControlMath.IsFinite(time) || time<_nextPress) return false;
        var length=nx*nx+ny*ny+nz*nz;
        if (length<.99f || length>1.01f) return false;
        _x=x; _y=y; _z=z; _nx=nx; _ny=ny; _nz=nz;
        _blocked=true; _rebase=false; _nextPress=time+.30f;
        return true;
    }

    // Focus/recenter/tracking changes cannot masquerade as a physical retreat.
    internal void Suspend() { if (_blocked) _rebase=true; }
    internal void Reset() { _blocked=_rebase=false; _nextPress=float.NegativeInfinity; }
    private static bool Finite(float x,float y,float z) =>
        FlightControlMath.IsFinite(x)&&FlightControlMath.IsFinite(y)&&FlightControlMath.IsFinite(z);
}
