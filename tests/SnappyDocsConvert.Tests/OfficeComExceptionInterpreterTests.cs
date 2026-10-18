using System.Reflection;
using System.Runtime.InteropServices;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Tests for <see cref="OfficeComExceptionInterpreter"/>.
///
/// Property 1 (Bug Condition - Fix Checking): for any exception X where isBugCondition(X) holds
/// (X.InnerException != null), ResolveFailureMessage(X) == innermostMeaningful(X).Message AND
/// != the generic reflection wrapper text.
///
/// Property 2 (Preservation): for any exception X where isBugCondition(X) is false
/// (X.InnerException == null), ResolveFailureMessage(X) == X.Message (i.e. F(X) == F'(X)).
/// </summary>
public sealed class OfficeComExceptionInterpreterTests
{
    private const string GenericWrapper = "Exception has been thrown by the target of an invocation.";

    // ---- Property 1: Bug Condition exploration (MUST FAIL on unfixed code) ----------------

    /// <summary>
    /// innermostMeaningful(X) per the design: the deepest exception reachable through the
    /// InnerException chain that has a non-whitespace message, falling back to X itself.
    /// </summary>
    private static Exception InnermostMeaningful(Exception exception)
    {
        var best = exception;
        var current = exception;
        var hops = 0;
        while (current.InnerException is not null && hops < 64)
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

    public static IEnumerable<object[]> BugConditionShapes()
    {
        // 1. Wrapper over COMException (the reported .ppt scenario shape).
        yield return new object[]
        {
            new TargetInvocationException(
                GenericWrapper,
                new COMException("PowerPoint could not export the file.")),
            "PowerPoint could not export the file."
        };

        // 2. Wrapper over InvalidOperationException (meaningful inner message masked).
        yield return new object[]
        {
            new TargetInvocationException(
                GenericWrapper,
                new InvalidOperationException("Word could not open the document.")),
            "Word could not open the document."
        };

        // 3. Nested chain: wrapper -> InvalidOperationException -> COMException("source error").
        yield return new object[]
        {
            new TargetInvocationException(
                GenericWrapper,
                new InvalidOperationException(
                    "intermediate failure",
                    new COMException("source error"))),
            "source error"
        };
    }

    [Theory]
    [MemberData(nameof(BugConditionShapes))]
    public void ResolveFailureMessage_SurfacesInnermostMeaningfulCause_NotGenericWrapper(
        Exception thrown,
        string expectedInnerMessage)
    {
        // Bug condition holds for these inputs.
        Assert.NotNull(thrown.InnerException);

        var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(thrown);

        // Expected (fixed) behavior: innermost meaningful message, never the generic wrapper.
        Assert.Equal(expectedInnerMessage, resolved);
        Assert.Equal(InnermostMeaningful(thrown).Message, resolved);
        Assert.NotEqual(GenericWrapper, resolved);
    }

    [Fact]
    public void ResolveFailureMessage_ReturnsTypeAndHResult_WhenWrapperOverEmptyComException()
    {
        // Reported .ppt shape where the inner COMException carries an HRESULT but an EMPTY message:
        // the resolver must NOT surface the bare reflection wrapper text.
        var inner = new COMException("") { HResult = unchecked((int)0x80004005) };
        var thrown = new TargetInvocationException(GenericWrapper, inner);

        var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(thrown);

        Assert.NotEqual(GenericWrapper, resolved);
        Assert.Contains("COMException", resolved);
        Assert.Contains("HRESULT 0x80004005", resolved);
    }

    [Fact]
    public void ResolveFailureMessage_ReplacesBareWrapper_WhenNoInnerException()
    {
        // Degenerate case: a lone TargetInvocationException with the generic wrapper message and no
        // inner cause must still avoid surfacing the useless wrapper verbatim.
        var thrown = new TargetInvocationException(GenericWrapper, null);

        var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(thrown);

        Assert.NotEqual(GenericWrapper, resolved);
        Assert.Contains("TargetInvocationException", resolved);
    }

    [Fact]
    public void ResolveFailureMessage_ReportedPptScenario_SurfacesRealComError()
    {
        // Concrete reproduction of the reported .ppt -> PDF defect shape.
        var thrown = new TargetInvocationException(
            GenericWrapper,
            new COMException("PowerPoint could not export the file."));

        var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(thrown);

        Assert.Equal("PowerPoint could not export the file.", resolved);
        Assert.NotEqual(GenericWrapper, resolved);
    }

    // ---- Helper unit tests (task 3.3) -----------------------------------------------------

    [Fact]
    public void ResolveFailureMessage_ReturnsInnerComMessage_ForWrappedComException()
    {
        var thrown = new TargetInvocationException(
            GenericWrapper,
            new COMException("The document could not be opened."));

        Assert.Equal(
            "The document could not be opened.",
            OfficeComExceptionInterpreter.ResolveFailureMessage(thrown));
    }

    [Fact]
    public void ResolveFailureMessage_ReturnsDeepestMeaningfulMessage_ForMultiLevelChain()
    {
        var thrown = new TargetInvocationException(
            GenericWrapper,
            new InvalidOperationException(
                "intermediate failure",
                new COMException("deepest source error")));

        Assert.Equal(
            "deepest source error",
            OfficeComExceptionInterpreter.ResolveFailureMessage(thrown));
    }

    [Fact]
    public void ResolveFailureMessage_ReturnsOwnMessage_WhenInnerExceptionNull()
    {
        var thrown = new InvalidOperationException("Word returned no document instance.");

        Assert.Null(thrown.InnerException);
        Assert.Equal(
            "Word returned no document instance.",
            OfficeComExceptionInterpreter.ResolveFailureMessage(thrown));
    }

    [Fact]
    public void ResolveFailureMessage_FallsBackToNearestNonEmpty_WhenDeepestMessageIsWhitespace()
    {
        // Chain: meaningful outer-inner -> deepest with whitespace message.
        // The deepest exception's message is whitespace, so the resolver keeps the nearest
        // non-empty message found while walking the chain.
        var thrown = new TargetInvocationException(
            GenericWrapper,
            new InvalidOperationException(
                "PowerPoint could not export the file.",
                new COMException("   ")));

        Assert.Equal(
            "PowerPoint could not export the file.",
            OfficeComExceptionInterpreter.ResolveFailureMessage(thrown));
    }

    [Fact]
    public void ResolveFailureMessage_Terminates_OnCyclicChain()
    {
        // Build a cyclic InnerException chain via reflection on the private _innerException field.
        var first = new InvalidOperationException("first meaningful message");
        var second = new InvalidOperationException("second meaningful message");
        SetInnerException(first, second);
        SetInnerException(second, first);

        // Must terminate (bounded traversal) and return a meaningful message, never the wrapper.
        var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(first);

        Assert.Contains("meaningful message", resolved);
        Assert.NotEqual(GenericWrapper, resolved);
    }

    [Fact]
    public void TryDescribeDiagnostics_EmitsTypeAndHResult_ForComException()
    {
        var thrown = new TargetInvocationException(
            GenericWrapper,
            new COMException("PowerPoint could not export the file."));

        var produced = OfficeComExceptionInterpreter.TryDescribeDiagnostics(thrown, out var diagnostic);

        Assert.True(produced);
        Assert.NotNull(diagnostic);
        Assert.Contains("COMException", diagnostic);
        Assert.Contains("HRESULT 0x", diagnostic);
        // Non-secret: only type name and HRESULT, never paths or credentials.
        Assert.DoesNotContain(":\\", diagnostic);
    }

    [Fact]
    public void TryDescribeDiagnostics_EmitsTypeName_WhenNoHResult()
    {
        var thrown = new InvalidOperationException("Word returned no document instance.")
        {
            HResult = 0
        };

        var produced = OfficeComExceptionInterpreter.TryDescribeDiagnostics(thrown, out var diagnostic);

        Assert.True(produced);
        Assert.Equal("InvalidOperationException", diagnostic);
    }

    private static void SetInnerException(Exception target, Exception inner)
    {
        var field = typeof(Exception).GetField(
            "_innerException",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, inner);
    }
}
