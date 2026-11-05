#pragma once
namespace novr::foveation
{
struct ScopeLease
{
    enum State { Free, Queued, Begun } state = Free;
    bool Acquire()
    {
        if (state != Free) return false;
        state = Queued; return true;
    }
    bool ConsumeBegin()
    {
        if (state != Queued) return false;
        state = Begun; return true;
    }
    bool Retire(bool neverSubmitted)
    {
        if (state != (neverSubmitted ? Queued : Begun)) return false;
        state = Free; return true;
    }
};
}
