using NOVR.MotionControls;

internal static class TexturePreparationChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> assert)
    {
        check("Sleeve enhancement preserves uniform colors, alpha and source bytes", () =>
        {
            var input = Enumerable.Range(0, 64).SelectMany(i => new byte[] { 43, 104, 183, (byte)(i * 4) }).ToArray();
            var original = input.ToArray();
            var fabric = new byte[] { 128, 128, 128, 128, 128, 128 };
            var output = PilotTextureEnhancement.Apply(input, 8, fabric, 3, 2, CancellationToken.None);
            assert(output.SequenceEqual(original) && input.SequenceEqual(original));
            assert(!ReferenceEquals(input, output) && fabric.All(value => value == 128));
        });
        check("Sleeve sharpening handles edges and keeps the established fabric strength", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 64, 64, 64, 207 }, 9).SelectMany(x => x).ToArray();
            input[0] = 32;
            input[16] = 128;
            var sharpened = PilotTextureEnhancement.Apply(input, 3, new byte[] { 128 }, 1, 1, CancellationToken.None);
            assert(sharpened[0] == 26 && sharpened[16] == 150 && sharpened[19] == 207);
            var cloth = PilotTextureEnhancement.Apply(new byte[] { 20, 255, 0, 7 }, 1, new byte[] { 188 }, 1, 1, CancellationToken.None);
            assert(cloth.SequenceEqual(new byte[] { 27, 255, 7, 7 }));
            var dark = PilotTextureEnhancement.Apply(new byte[] { 0, 128, 255, 0 }, 1, new byte[] { 0 }, 1, 1, CancellationToken.None);
            assert(dark.SequenceEqual(new byte[] { 0, 113, 240, 0 }));
        });
        check("Sleeve fabric tile preserves four repeats in both image directions", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 128, 128, 128, 255 }, 256).SelectMany(x => x).ToArray();
            var fabric = new byte[] { 128, 178, 228, 78 };
            var output = PilotTextureEnhancement.Apply(input, 16, fabric, 2, 2, CancellationToken.None);
            assert(output[0] == 128 && output[2 * 4] == 134 && output[2 * 16 * 4] == 140 && output[(2 * 16 + 2) * 4] == 122);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
                assert(output[(y * 16 + x) * 4] == output[((y % 4) * 16 + x % 4) * 4]);
        });
        check("Sleeve filtering rejects invalid data and honors cancellation", () =>
        {
            foreach (var size in new[] { -1, 0, 2049, int.MaxValue })
            {
                var rejected = false;
                try { PilotTextureEnhancement.Apply(Array.Empty<byte>(), size, new byte[1], 1, 1, CancellationToken.None); }
                catch (ArgumentException) { rejected = true; }
                assert(rejected);
            }
            var truncated = false;
            try { PilotTextureEnhancement.Apply(new byte[4], 1, new byte[1], 2, 2, CancellationToken.None); }
            catch (ArgumentException) { truncated = true; }
            assert(truncated);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var canceled = false;
            try { PilotTextureEnhancement.Apply(new byte[4], 1, new byte[1], 1, 1, cancel.Token); }
            catch (OperationCanceledException) { canceled = true; }
            assert(canceled);
        });
        check("Background texture jobs serialize and discard an abandoned cockpit's result", () =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var secondStarted = new ManualResetEventSlim();
            var caller = Environment.CurrentManagedThreadId;
            var workerThread = caller;
            using var old = new DeferredTextureJob(_ =>
            {
                workerThread = Environment.CurrentManagedThreadId; entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test release missing");
                return new byte[] { 1 };
            });
            try
            {
                assert(entered.Wait(TimeSpan.FromSeconds(5)) && workerThread != caller);
                assert(!old.TryTake(out _));
                using var current = new DeferredTextureJob(_ => { secondStarted.Set(); return new byte[] { 2 }; });
                assert(!secondStarted.IsSet && !current.TryTake(out _));
                old.Dispose(); // Must return while its worker is still held by the test.
                assert(!old.TryTake(out _));
                release.Set();
                var result = Take(current);
                assert(secondStarted.IsSet && result.Error == null && result.Pixels!.SequenceEqual(new byte[] { 2 }));
                assert(!old.TryTake(out _) && !current.TryTake(out _));
            }
            finally { release.Set(); }
        });
        check("A canceled waiting texture never runs or blocks the next cockpit", () =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var first = new DeferredTextureJob(_ =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test release missing");
                return new byte[] { 1 };
            });
            var canceledRan = 0;
            try
            {
                assert(entered.Wait(TimeSpan.FromSeconds(5)));
                using var canceled = new DeferredTextureJob(_ => { Interlocked.Increment(ref canceledRan); return new byte[] { 2 }; });
                canceled.Dispose();
                using var next = new DeferredTextureJob(_ => new byte[] { 3 });
                release.Set();
                assert(Take(next).Pixels!.SequenceEqual(new byte[] { 3 }));
                assert(canceledRan == 0 && !canceled.TryTake(out _));
            }
            finally { release.Set(); }
        });
        check("Texture filter failures report fallback and release the worker", () =>
        {
            using var failing = new DeferredTextureJob(_ => throw new InvalidOperationException("Bad pixel data"));
            var failure = Take(failing);
            assert(failure.Pixels == null && failure.Error == "Bad pixel data");
            using var next = new DeferredTextureJob(_ => new byte[] { 9 });
            assert(Take(next).Pixels!.SequenceEqual(new byte[] { 9 }));
        });
    }

    private static DeferredTextureJob.Result Take(DeferredTextureJob job)
    {
        DeferredTextureJob.Result result = default;
        if (!SpinWait.SpinUntil(() => job.TryTake(out result), TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Texture job failed to complete");
        return result;
    }
}
