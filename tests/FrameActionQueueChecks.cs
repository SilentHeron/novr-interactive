using NOVR.MotionControls;

internal static class FrameActionQueueChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("A new cockpit request cannot erase an eject due in the current frame", () =>
        {
            var queue = new FrameActionQueue<object>(); var aircraft = new object();
            queue.Enqueue("Eject", aircraft, 100);
            truth(!queue.Contains("Eject", aircraft, 100));
            queue.Enqueue("Gear", aircraft, 101);
            truth(queue.Contains("Eject", aircraft, 101));
            truth(!queue.Contains("Gear", aircraft, 101));
            truth(queue.Contains("Gear", aircraft, 102));
            truth(!queue.Contains("Eject", aircraft, 102));
        });
        check("Independent and repeated cockpit actions survive adjacent input frames", () =>
        {
            var queue = new FrameActionQueue<object>(); var aircraft = new object();
            queue.Enqueue("Gear", aircraft, 8); queue.Enqueue("Radar", aircraft, 8);
            queue.Enqueue("Gear", aircraft, 8); queue.Enqueue("Gear", aircraft, 9);
            truth(queue.Contains("Gear", aircraft, 9) && queue.Contains("Radar", aircraft, 9));
            truth(queue.Contains("Gear", aircraft, 10) && !queue.Contains("Radar", aircraft, 10));
            queue.Enqueue("Flight Assist", aircraft, 10);
            truth(queue.Contains("Gear", aircraft, 10));
            truth(queue.Contains("Flight Assist", aircraft, 11));
            truth(!queue.Contains("Gear", aircraft, 11));
        });
        check("Queued controls cannot cross aircraft ownership", () =>
        {
            var queue = new FrameActionQueue<object>(); var oldAircraft = new object(); var newAircraft = new object();
            queue.Enqueue("Eject", oldAircraft, 3);
            truth(!queue.Contains("Eject", newAircraft, 4));
            queue.Enqueue("Gear", newAircraft, 3);
            truth(!queue.Contains("Eject", oldAircraft, 4));
            truth(!queue.Contains("Gear", oldAircraft, 4));
            truth(queue.Contains("Gear", newAircraft, 4));
        });
        check("Binding resets clear both due and future cockpit actions", () =>
        {
            var queue = new FrameActionQueue<object>(); var aircraft = new object();
            queue.Enqueue("Eject", aircraft, 20); queue.Enqueue("Gear", aircraft, 21);
            queue.Clear();
            truth(!queue.Contains("Eject", aircraft, 21));
            truth(!queue.Contains("Gear", aircraft, 22));
            queue.Enqueue("Radar", aircraft, 21);
            truth(queue.Contains("Radar", aircraft, 22));
        });
        check("Expired cockpit actions cannot replay after missed frames or bucket reuse", () =>
        {
            var queue = new FrameActionQueue<object>(); var aircraft = new object();
            queue.Enqueue("Eject", aircraft, 0);
            truth(!queue.Contains("Eject", aircraft, 3));
            queue.Enqueue("Gear", aircraft, 4);
            truth(!queue.Contains("Eject", aircraft, 5));
            truth(queue.Contains("Gear", aircraft, 5));
        });
    }
}
