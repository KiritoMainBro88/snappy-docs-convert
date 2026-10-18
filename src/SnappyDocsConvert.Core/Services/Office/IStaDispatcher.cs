namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Runs work items on a single, long-lived STA thread. COM objects created by Office automation have
/// thread affinity (STA), so every call touching a given Office instance must run on the same STA
/// thread for the lifetime of that instance. This dispatcher provides that guarantee without spinning
/// up a fresh thread per call.
/// </summary>
public interface IStaDispatcher : IDisposable
{
    /// <summary>Queues <paramref name="work"/> onto the STA thread and returns its result.</summary>
    Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellationToken = default);

    /// <summary>Queues an action onto the STA thread.</summary>
    Task InvokeAsync(Action work, CancellationToken cancellationToken = default);
}
