using System.Collections.Concurrent;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Tests for <see cref="StaDispatcher"/>. No COM required: work items are plain delegates. Validates
/// STA affinity (on Windows), single-threaded serialization, result/exception propagation, and clean
/// disposal.
/// </summary>
public sealed class StaDispatcherTests
{
    [Fact]
    public async Task InvokeAsync_RunsOnStaThread_OnWindows()
    {
        using var dispatcher = new StaDispatcher();

        var state = await dispatcher.InvokeAsync(() => Thread.CurrentThread.GetApartmentState());

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(ApartmentState.STA, state);
        }
    }

    [Fact]
    public async Task InvokeAsync_RunsAllWorkOnSameSingleThread()
    {
        using var dispatcher = new StaDispatcher();
        var threadIds = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, 50).Select(_ => dispatcher.InvokeAsync(() =>
        {
            threadIds.Add(Environment.CurrentManagedThreadId);
            return 0;
        }));
        await Task.WhenAll(tasks);

        Assert.Single(threadIds.Distinct());
    }

    [Fact]
    public async Task InvokeAsync_SerializesWork_MaxConcurrencyIsOne()
    {
        using var dispatcher = new StaDispatcher();
        var active = 0;
        var maxConcurrent = 0;
        var sync = new object();

        var tasks = Enumerable.Range(0, 30).Select(_ => dispatcher.InvokeAsync(() =>
        {
            lock (sync)
            {
                active++;
                if (active > maxConcurrent)
                {
                    maxConcurrent = active;
                }
            }

            Thread.SpinWait(5_000);

            lock (sync)
            {
                active--;
            }

            return 0;
        }));
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    [Fact]
    public async Task InvokeAsync_PropagatesResult()
    {
        using var dispatcher = new StaDispatcher();

        var result = await dispatcher.InvokeAsync(() => 6 * 7);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeAsync_PropagatesException()
    {
        using var dispatcher = new StaDispatcher();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.InvokeAsync<int>(() => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public void InvokeAsync_AfterDispose_Throws()
    {
        var dispatcher = new StaDispatcher();
        dispatcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = dispatcher.InvokeAsync(() => 0); });
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var dispatcher = new StaDispatcher();
        dispatcher.Dispose();
        dispatcher.Dispose();
    }
}
