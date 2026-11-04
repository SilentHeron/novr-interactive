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
        check("Sleeve highlights retain bright folds while approved glove ramp stays unchanged", () =>
        {
            var pixels=Enumerable.Range(0,256).SelectMany(v=>new byte[]{(byte)v,(byte)v,(byte)v,255}).ToArray();
            foreach(var team in new[]{PilotUniformTeam.Bdf,PilotUniformTeam.Pala})
            {
                var sleeve=PilotTextureEnhancement.Recolor(pixels,256,1,team,false,default);
                var glove=PilotTextureEnhancement.Recolor(pixels,256,1,team,true,default);
                var palette=PilotUniformPalette.Color(team,true);
                for(var i=1;i<256;i++)
                {
                    assert(sleeve[i*4]>=sleeve[(i-1)*4] && sleeve[i*4+2]>=sleeve[(i-1)*4+2]);
                    var legacy=Math.Min(1.25,Math.Pow(i/96.0,.75));
                    assert(glove[i*4]==(byte)Math.Min(255,Math.Round(palette.red*legacy)));
                }
                assert(sleeve[160*4+2]<sleeve[200*4+2] && sleeve[200*4+2]<sleeve[255*4+2]);
                assert(sleeve[255*4+2]<255);
            }
        });
        check("Normal detail preserves native folds exactly when the weave is flat or subpixel", () =>
        {
            var input=Enumerable.Range(0,256).SelectMany(i=>new byte[]{255,(byte)(100+i%40),255,(byte)(110+i%30)}).ToArray();
            var original=input.ToArray();
            var flat=PilotTextureEnhancement.ApplyNormal(input,16,16,new byte[]{128},1,1,default);
            assert(flat.SequenceEqual(input) && !ReferenceEquals(flat,input));
            var checker=Enumerable.Range(0,64*64).Select(i=>(byte)(((i/64+i%64)%2)*255)).ToArray();
            assert(PilotTextureEnhancement.ApplyNormal(input,16,16,checker,64,64,default).SequenceEqual(input));
            assert(input.SequenceEqual(original));
        });
        check("Weave normals follow the native fold frame and stay small, normalized and correctly packed", () =>
        {
            var fabric=Enumerable.Range(0,81).Select(i=>(byte)(128+90*Math.Sin((i%9)*Math.PI*2/9)+25*Math.Sin((i/9)*Math.PI*2/9))).ToArray();
            static (double x,double y,double z) Decode(byte[] p,int at)
            { var x=p[at]*p[at+3]/65025.0*2-1;var y=p[at+1]/255.0*2-1;return(x,y,Math.Sqrt(Math.Max(0,1-x*x-y*y))); }
            foreach(var source in new[]{new byte[]{255,128,255,128},new byte[]{255,155,255,190},new byte[]{190,155,255,255}})
            {
                var input=Enumerable.Range(0,64*32).SelectMany(_=>source).ToArray();var original=input.ToArray();
                var output=PilotTextureEnhancement.ApplyNormal(input,64,32,fabric,9,9,default);
                assert(!output.SequenceEqual(input) && input.SequenceEqual(original));
                var n=Decode(source,0);
                for(var i=0;i<output.Length;i+=4)
                {
                    var v=Decode(output,i);assert(output[i]==255 && output[i+2]==255);
                    assert(double.IsFinite(v.z) && v.z>0 && Math.Abs(v.x*v.x+v.y*v.y+v.z*v.z-1)<.000001);
                    assert(n.x*v.x+n.y*v.y+n.z*v.z>Math.Cos(10*Math.PI/180));
                }
                // Four repeats preserve the rectangular source atlas shape.
                for(var y=0;y<32;y++)for(var x=0;x<64;x++)
                { var a=(y*64+x)*4;var b=((y%8)*64+x%16)*4;assert(output[a+1]==output[b+1] && output[a+3]==output[b+3]); }
            }
        });
        check("Normal filtering rejects malformed atlases and honors cancellation", () =>
        {
            foreach(var size in new[]{0,-1,2049})
            { try{PilotTextureEnhancement.ApplyNormal(new byte[4],size,1,new byte[]{128},1,1,default);assert(false);}catch(ArgumentException){} }
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try{PilotTextureEnhancement.ApplyNormal(new byte[4],1,1,new byte[]{128},1,1,cancel.Token);assert(false);}catch(OperationCanceledException){}
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
