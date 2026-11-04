using NOVR.MotionControls;

internal static class FrameActionQueueChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("A new cockpit request cannot erase an eject due in the current frame", () =>
        {
            var queue = new FrameActionQueue<NativeControllerAction, object>();
            var aircraft = new object();
            queue.Enqueue(NativeControllerAction.Eject, aircraft, 100);
            truth(!queue.Contains(NativeControllerAction.Eject, aircraft, 100));
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 101);
            truth(queue.Contains(NativeControllerAction.Eject, aircraft, 101));
            truth(!queue.Contains(NativeControllerAction.Gear, aircraft, 101));
            truth(queue.Contains(NativeControllerAction.Gear, aircraft, 102));
            truth(!queue.Contains(NativeControllerAction.Eject, aircraft, 102));
        });
        check("Independent and repeated cockpit actions survive adjacent input frames", () =>
        {
            var queue = new FrameActionQueue<NativeControllerAction, object>();
            var aircraft = new object();
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 8);
            queue.Enqueue(NativeControllerAction.Radar, aircraft, 8);
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 8);
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 9);
            truth(queue.Contains(NativeControllerAction.Gear, aircraft, 9) && queue.Contains(NativeControllerAction.Radar, aircraft, 9));
            truth(queue.Contains(NativeControllerAction.Gear, aircraft, 10) && !queue.Contains(NativeControllerAction.Radar, aircraft, 10));
            queue.Enqueue(NativeControllerAction.FlightAssist, aircraft, 10);
            truth(queue.Contains(NativeControllerAction.Gear, aircraft, 10));
            truth(queue.Contains(NativeControllerAction.FlightAssist, aircraft, 11));
            truth(!queue.Contains(NativeControllerAction.Gear, aircraft, 11));
        });
        check("Queued controls cannot cross aircraft ownership", () =>
        {
            var queue = new FrameActionQueue<NativeControllerAction, object>();
            var oldAircraft = new object();
            var newAircraft = new object();
            queue.Enqueue(NativeControllerAction.Eject, oldAircraft, 3);
            truth(!queue.Contains(NativeControllerAction.Eject, newAircraft, 4));
            queue.Enqueue(NativeControllerAction.Gear, newAircraft, 3);
            truth(!queue.Contains(NativeControllerAction.Eject, oldAircraft, 4));
            truth(!queue.Contains(NativeControllerAction.Gear, oldAircraft, 4));
            truth(queue.Contains(NativeControllerAction.Gear, newAircraft, 4));
        });
        check("Binding resets clear both due and future cockpit actions", () =>
        {
            var queue = new FrameActionQueue<NativeControllerAction, object>();
            var aircraft = new object();
            queue.Enqueue(NativeControllerAction.Eject, aircraft, 20);
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 21);
            queue.Clear();
            truth(!queue.Contains(NativeControllerAction.Eject, aircraft, 21));
            truth(!queue.Contains(NativeControllerAction.Gear, aircraft, 22));
            queue.Enqueue(NativeControllerAction.Radar, aircraft, 21);
            truth(queue.Contains(NativeControllerAction.Radar, aircraft, 22));
        });
        check("Expired cockpit actions cannot replay after missed frames or bucket reuse", () =>
        {
            var queue = new FrameActionQueue<NativeControllerAction, object>();
            var aircraft = new object();
            queue.Enqueue(NativeControllerAction.Eject, aircraft, 0);
            truth(!queue.Contains(NativeControllerAction.Eject, aircraft, 3));
            queue.Enqueue(NativeControllerAction.Gear, aircraft, 4);
            truth(!queue.Contains(NativeControllerAction.Eject, aircraft, 5));
            truth(queue.Contains(NativeControllerAction.Gear, aircraft, 5));
        });
    }
}
