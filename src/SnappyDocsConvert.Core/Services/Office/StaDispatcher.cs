using System.Collections.Concurrent;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Single-threaded STA dispatcher. A dedicated background thread is set to
/// <see cref="ApartmentState.STA"/> and pulls work items from a blocking queue one at a time, so all
/// queued work runs sequentially on the same STA thread. Disposing completes the queue and joins the
/// thread so no work leaks past disposal.
/// </summary>
public sealed class StaDispatcher : IStaDispatcher
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private bool _disposed;

    public StaDispatcher()
    {
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "OfficeComSta"
        };

        // STA is required for Office COM marshalling. On non-Windows the call is a no-op-ish set that
        // still yields a working single-thread pump (the STA semantics only matter for real COM).
        if (OperatingSystem.IsWindows())
        {
            _thread.SetApartmentState(ApartmentState.STA);
        }

        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                tcs.TrySetResult(work());
            }
            catch (OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, CancellationToken.None);

        return tcs.Task;
    }

    public Task InvokeAsync(Action work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync<object?>(() =>
        {
            work();
            return null;
        }, cancellationToken);
    }

    private void Pump()
    {
        // The pump must NEVER observe an unhandled exception: an unobserved exception on this
        // background thread would tear down the whole process. Every work item already funnels its
        // failure into its TaskCompletionSource, so the loop only fails on shutdown-time queue races
        // (e.g. the collection disposed while a hung item still occupies the thread).
        try
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                action();
            }
        }
        catch
        {
            // Shutdown race: drain is over or the queue was disposed under a stuck item.
        }
    }

    /// <summary>How long dispose waits for the STA thread to finish draining before abandoning it.</summary>
    internal static TimeSpan DisposeJoinTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();

        if (_thread.IsAlive)
        {
            // Bounded join: a stuck COM call can keep the pump alive forever, and app shutdown must
            // not hang on it. The thread is a background thread, so an abandoned pump dies with the
            // process and no queued work leaks past it.
            _thread.Join(DisposeJoinTimeout);
        }

        if (!_thread.IsAlive)
        {
            _queue.Dispose();
        }

        // When the thread is still stuck, leave the queue to the background thread/GC; disposing it
        // under a live pump would race the enumeration loop.
    }
}
