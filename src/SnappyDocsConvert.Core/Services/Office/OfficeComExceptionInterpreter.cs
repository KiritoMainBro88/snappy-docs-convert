using System.Globalization;

namespace SnappyDocsConvert.Core.Services.Office;

/// <summary>
/// Resolves the most informative failure message from a thrown exception chain produced by
/// reflection-based Office COM automation.
/// </summary>
/// <remarks>
/// Every Office operation runs through <c>Type.InvokeMember</c>, so a failing COM call surfaces
/// as a <c>TargetInvocationException</c> whose <c>Message</c> is the generic wrapper text
/// "Exception has been thrown by the target of an invocation." while the real cause sits in
/// <c>InnerException</c>. This helper walks the <c>InnerException</c> chain to the innermost
/// exception with a non-whitespace message so the surfaced failure reason is actionable.
///
/// The helper is pure and side-effect free; it can be unit-tested without a live COM/Office
/// install by constructing ordinary exceptions with nested <c>InnerException</c> chains.
/// </remarks>
public static class OfficeComExceptionInterpreter
{
    /// <summary>
    /// Bounded traversal depth so a pathological/cyclic <c>InnerException</c> chain always
    /// terminates. Real exception chains are shallow; this cap is generous.
    /// </summary>
    private const int MaxChainDepth = 64;

    /// <summary>
    /// The generic, non-actionable text the CLR puts on a <c>TargetInvocationException</c> raised by
    /// reflection-based COM automation. When this is the only message available (e.g. the inner
    /// COMException carries an HRESULT but an empty message) it must never be surfaced verbatim.
    /// </summary>
    private const string ReflectionWrapperMessage =
        "Exception has been thrown by the target of an invocation.";

    /// <summary>
    /// Returns the message of the innermost meaningful exception in the chain.
    /// </summary>
    /// <remarks>
    /// Implements <c>innermostMeaningful(X).Message</c>: walks the <c>InnerException</c> chain,
    /// tracking the deepest exception with a non-whitespace message, and falls back to the outer
    /// exception when the chain has no usable message. When <c>exception.InnerException == null</c>
    /// the outer exception's own <c>Message</c> is returned unchanged (preservation).
    /// </remarks>
    public static string ResolveFailureMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var meaningful = InnermostMeaningful(exception);
        var message = meaningful.Message;

        // When the only message available is the generic reflection wrapper (the inner COM error
        // carried an HRESULT but no message), the bare wrapper text is useless to the user. Replace
        // it with a type + HRESULT diagnostic so the surfaced reason is actionable.
        if (IsUselessMessage(message))
        {
            return TryDescribeDiagnostics(exception, out var diagnostic) && diagnostic is not null
                ? diagnostic
                : message;
        }

        return message;
    }

    /// <summary>
    /// Builds a short, non-secret diagnostic such as "<c>COMException (HRESULT 0x80004005)</c>"
    /// from the innermost meaningful exception. Intended for the <c>Warnings</c> channel only,
    /// never the primary <c>ErrorMessage</c>.
    /// </summary>
    /// <returns><c>true</c> when a useful diagnostic was produced; otherwise <c>false</c>.</returns>
    public static bool TryDescribeDiagnostics(Exception exception, out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innermost = Deepest(exception);
        var typeName = innermost.GetType().Name;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            diagnostic = null;
            return false;
        }

        var hresult = innermost.HResult;

        // HResult is 0 (S_OK) when the runtime did not assign a COM error code; in that case a
        // bare type name is still useful context for the activity log.
        diagnostic = hresult != 0
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0} (HRESULT 0x{1:X8})",
                typeName,
                hresult)
            : typeName;

        return true;
    }

    private static bool IsUselessMessage(string message)
        => string.Equals(message, ReflectionWrapperMessage, StringComparison.Ordinal);

    /// <summary>
    /// Walks to the deepest exception in the chain (regardless of message), so a COMException whose
    /// message is empty but whose HRESULT carries the real error code is still used for diagnostics.
    /// </summary>
    private static Exception Deepest(Exception exception)
    {
        var current = exception;
        var hops = 0;

        while (current.InnerException is not null && hops < MaxChainDepth)
        {
            current = current.InnerException;
            hops++;
        }

        return current;
    }

    private static Exception InnermostMeaningful(Exception exception)
    {
        var best = exception;
        var current = exception;
        var hops = 0;

        while (current.InnerException is not null && hops < MaxChainDepth)
        {
            current = current.InnerException;
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                best = current;
            }

            hops++;
        }

        return best;
    }
}
