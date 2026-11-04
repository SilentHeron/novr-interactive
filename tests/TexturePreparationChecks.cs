using NOVR.MotionControls;

internal static class TexturePreparationChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> assert)
    {
        check("Pilot palettes identify actual factions and leave unfamiliar teams unchanged", () =>
        {
            assert(PilotUniformPalette.Resolve("BDF", null) == PilotUniformTeam.Bdf);
            assert(PilotUniformPalette.Resolve(null, "Boscali") == PilotUniformTeam.Bdf);
            assert(PilotUniformPalette.Resolve(" pala ", null) == PilotUniformTeam.Pala);
            assert(PilotUniformPalette.Resolve(null, "Primeva") == PilotUniformTeam.Pala);
            assert(PilotUniformPalette.Resolve("custom", "Custom faction") == PilotUniformTeam.Native);
            assert(PilotUniformPalette.Resolve(null, null) == PilotUniformTeam.Native);
        });
        check("Team gloves and sleeves retain folds, alpha and black stitches without mutating source atlases", () =>
        {
            var input = new byte[] { 0, 0, 0, 255, 32, 32, 32, 128, 96, 96, 96, 255, 160, 160, 160, 255, 40, 80, 120, 0 };
            var original = input.ToArray();
            foreach (var team in new[] { PilotUniformTeam.Bdf, PilotUniformTeam.Pala })
            foreach (var glove in new[] { false, true })
            {
                var output = PilotTextureEnhancement.Recolor(input, 5, 1, team, glove, CancellationToken.None);
                var target = PilotUniformPalette.Color(team, glove);
                assert(output[8] == target.red && output[9] == target.green && output[10] == target.blue);
                assert(output[0] == 0 && output[1] == 0 && output[2] == 0);
                for (var c = 0; c < 3; c++) assert(output[4+c] < output[8+c] && output[8+c] < output[12+c]);
                for (var i = 3; i < output.Length; i += 4) assert(output[i] == input[i]);
                assert(output.Skip(16).SequenceEqual(input.Skip(16)) && input.SequenceEqual(original));
                if (glove) assert(team == PilotUniformTeam.Bdf ? output[9] > output[8] && output[9] > output[10] : output[8] > output[9] * 2);
                else assert(team == PilotUniformTeam.Bdf ? output[8] > 140 && output[10] > output[8] : output[10] < 40);
            }
            assert(PilotTextureEnhancement.Recolor(input, 5, 1, PilotUniformTeam.Native, false, CancellationToken.None).SequenceEqual(input));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { PilotTextureEnhancement.Recolor(input, 5, 1, PilotUniformTeam.Bdf, true, cancel.Token); assert(false); }
            catch (OperationCanceledException) { }
        });
        check("Sleeve enhancement preserves uniform colors, alpha and source bytes", () =>
        {
            var input = Enumerable.Range(0, 64).SelectMany(i => new byte[] { 43, 104, 183, (byte)(i * 4) }).ToArray();
            var original = input.ToArray();
            var fabric = new byte[] { 128, 128, 128, 128, 128, 128 };
            var output = PilotTextureEnhancement.Apply(input, 8, fabric, 3, 2, CancellationToken.None);
            assert(output.SequenceEqual(original) && input.SequenceEqual(original));
            assert(!ReferenceEquals(input, output) && fabric.All(value => value == 128));
        });
        check("Sleeve local contrast leaves printed edges and isolated highlights free of halos", () =>
        {
            var input = new byte[16 * 16 * 4];
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
            {
                var at = (y * 16 + x) * 4;
                input[at] = input[at + 1] = input[at + 2] = (byte)(x < 8 ? 32 : 220);
                input[at + 3] = 255;
            }
            var output = PilotTextureEnhancement.Apply(input, 16, new byte[] { 128 }, 1, 1, CancellationToken.None);
            assert(output.SequenceEqual(input));
            input[(5 * 16 + 4) * 4] = input[(5 * 16 + 4) * 4 + 1] = input[(5 * 16 + 4) * 4 + 2] = 48;
            output = PilotTextureEnhancement.Apply(input, 16, new byte[] { 128 }, 1, 1, CancellationToken.None);
            assert(output.SequenceEqual(input));
        });
        check("Sleeve cloth preserves hue, black markings and average palette", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 40, 100, 180, 255 }, 1024).SelectMany(x => x).ToArray();
            var fabric = new byte[] { 80, 100, 120, 140 };
            var original = fabric.ToArray();
            var output = PilotTextureEnhancement.Apply(input, 32, fabric, 2, 2, CancellationToken.None);
            assert(fabric.SequenceEqual(original) && !output.SequenceEqual(input));
            var totals = new int[3];
            for (var i = 0; i < output.Length; i += 4)
            {
                assert(Math.Abs(output[i] / 40f - output[i + 2] / 180f) < .02f);
                assert(Math.Abs(output[i + 1] / 100f - output[i + 2] / 180f) < .01f);
                for (var c = 0; c < 3; c++) totals[c] += output[i + c];
                assert(output[i + 3] == 255);
            }
            for (var c = 0; c < 3; c++) assert(Math.Abs(totals[c] / 1024f - input[c]) <= .5f);
            var black = Enumerable.Repeat(new byte[] { 0, 0, 0, 255 }, 1024).SelectMany(x => x).ToArray();
            assert(PilotTextureEnhancement.Apply(black, 32, fabric, 2, 2, CancellationToken.None).SequenceEqual(black));
        });
        check("Sleeve fabric keeps four filtered repeats on rectangular atlases", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 128, 128, 128, 255 }, 32 * 16).SelectMany(x => x).ToArray();
            var fabric = Enumerable.Range(0, 15).Select(i => (byte)(i * 10)).ToArray();
            var output = PilotTextureEnhancement.Apply(input, 32, 16, fabric, 5, 3, CancellationToken.None);
            assert(output.Length == input.Length && output.Where((_, i) => i % 4 == 0).Distinct().Count() > 2);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 32; x++)
                assert(output[(y * 32 + x) * 4] == output[((y % 4) * 32 + x % 8) * 4]);
        });
        check("Subpixel weave is averaged before repetition instead of aliasing", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 128, 128, 128, 255 }, 16 * 16).SelectMany(x => x).ToArray();
            var fabric = Enumerable.Range(0, 64 * 64).Select(i => (byte)(((i / 64 + i % 64) % 2) * 255)).ToArray();
            var output = PilotTextureEnhancement.Apply(input, 16, fabric, 64, 64, CancellationToken.None);
            assert(output.SequenceEqual(input));
            // Every texel participates when an odd-size source shrinks to one sample.
            assert(PilotTextureEnhancement.Apply(input, 16, new byte[] { 0, 0, 255 }, 3, 1,
                CancellationToken.None).Where((_, i) => i % 4 == 0).Distinct().Count() > 1);
            var one = new byte[] { 43, 104, 183, 77 };
            assert(PilotTextureEnhancement.Apply(one, 1, new byte[] { 0, 0, 255 }, 3, 1,
                CancellationToken.None).SequenceEqual(one));
        });
        check("Magnified weave interpolates smoothly through its repeat boundary", () =>
        {
            var input = Enumerable.Repeat(new byte[] { 128, 128, 128, 255 }, 64 * 64).SelectMany(x => x).ToArray();
            var output = PilotTextureEnhancement.Apply(input, 64, new byte[] { 0, 255 }, 2, 1, CancellationToken.None);
            for (var x = 0; x < 64; x++) assert(Math.Abs(output[x * 4] - output[((x + 1) % 64) * 4]) <= 2);
            assert(output.Where((_, i) => i % 4 == 0).Distinct().Count() > 4);
        });
        check("Transparent atlas padding and opaque neighbors cannot bleed into each other", () =>
        {
            var input = new byte[] { 100, 80, 50, 255, 255, 255, 255, 0, 0, 0, 0, 0, 100, 80, 50, 255 };
            var output = PilotTextureEnhancement.Apply(input, 2, new byte[] { 128 }, 1, 1, CancellationToken.None);
            assert(output.SequenceEqual(input));
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
            foreach (var dimensions in new[] { (0, 8), (8, 0), (2049, 1), (1, 2049), (2, 3) })
            {
                var rejected = false;
                try { PilotTextureEnhancement.Apply(new byte[4], dimensions.Item1, dimensions.Item2,
                    new byte[1], 1, 1, CancellationToken.None); }
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
