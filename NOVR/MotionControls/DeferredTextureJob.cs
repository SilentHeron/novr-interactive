using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NOVR.MotionControls;

// One CPU filter at a time, including across aircraft changes. Owners poll without
// waiting, and abandoned results can never be applied to a subsequent cockpit.
internal sealed class DeferredTextureJob : IDisposable
{
    internal readonly struct Result
    {
        internal readonly byte[]? Pixels;
        internal readonly string? Error;
        internal readonly double Milliseconds;
        internal Result(byte[]? pixels, string? error, double milliseconds = 0)
        { Pixels = pixels; Error = error; Milliseconds = milliseconds; }
    }

    private static readonly SemaphoreSlim Worker = new(1, 1);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task<Result> _task;
    private bool _disposed, _taken;

    // The delegate must capture managed data only, never Unity objects or APIs.
    internal DeferredTextureJob(Func<CancellationToken, byte[]> work) =>
        _task = Run(work, _cancellation.Token);

    private static async Task<Result> Run(Func<CancellationToken, byte[]> work, CancellationToken token)
    {
        var acquired = false;
        try
        {
            await Worker.WaitAsync(token).ConfigureAwait(false);
            acquired = true;
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var timer = Stopwatch.StartNew();
                var pixels = work(token);
                token.ThrowIfCancellationRequested();
                return new Result(pixels, null, timer.Elapsed.TotalMilliseconds);
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return new Result(null, null); }
        catch (Exception e) { return new Result(null, e.Message); }
        finally { if (acquired) Worker.Release(); }
    }

    internal bool TryTake(out Result result)
    {
        result = default;
        if (_disposed || _taken || !_task.IsCompleted) return false;
        _taken = true;
        result = _task.GetAwaiter().GetResult();
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        // Let a pending semaphore wait/worker finish using its token before disposal.
        _ = _task.ContinueWith(_ => _cancellation.Dispose(), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
