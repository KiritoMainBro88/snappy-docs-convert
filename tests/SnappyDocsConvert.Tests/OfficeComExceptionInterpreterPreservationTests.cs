using System.Runtime.InteropServices;
using SnappyDocsConvert.Core.Services.Office;

namespace SnappyDocsConvert.Tests;

/// <summary>
/// Property 2 (Preservation - Non-buggy inputs unchanged): for any exception X with no inner
/// cause (X.InnerException == null), ResolveFailureMessage(X) == X.Message, identical to the
/// original mapping F. Implemented as deterministic, seeded generative loops (no new PBT
/// dependency per AGENTS.md).
/// </summary>
public sealed class OfficeComExceptionInterpreterPreservationTests
{
    private const int Iterations = 500;
    private const int Seed = 20240117;

    /// <summary>
    /// Builds a single exception with NO InnerException, varying type and message content so the
    /// generated input space covers the non-buggy domain.
    /// </summary>
    private static Exception BuildSingleException(Random random)
    {
        var message = BuildMessage(random);
        return (random.Next(0, 5)) switch
        {
            0 => new InvalidOperationException(message),
            1 => new COMException(message),
            2 => new ArgumentException(message),
            3 => new NotSupportedException(message),
            _ => new Exception(message)
        };
    }

    private static string BuildMessage(Random random)
    {
        // Cover empty, whitespace, and arbitrary content - all valid for the no-inner case.
        return (random.Next(0, 6)) switch
        {
            0 => string.Empty,
            1 => "   ",
            2 => "Word returned no document instance.",
            3 => "PowerPoint returned no presentation instance.",
            4 => RandomText(random),
            _ => "Microsoft Office COM export failed."
        };
    }

    private static string RandomText(Random random)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 .,:;-_";
        var length = random.Next(1, 40);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Length)];
        }

        return new string(chars);
    }

    [Fact]
    public void ResolveFailureMessage_PreservesOwnMessage_WhenNoInnerException()
    {
        var random = new Random(Seed);

        for (var i = 0; i < Iterations; i++)
        {
            var exception = BuildSingleException(random);

            // Precondition: bug condition does NOT hold.
            Assert.Null(exception.InnerException);

            var resolved = OfficeComExceptionInterpreter.ResolveFailureMessage(exception);

            // F(X) == F'(X): the original mapping recorded exception.Message verbatim.
            Assert.Equal(exception.Message, resolved);
        }
    }

    [Theory]
    [InlineData("Word returned no document instance.")]
    [InlineData("PowerPoint returned no presentation instance.")]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFailureMessage_ReturnsOwnMessage_ForExplicitNoInnerCases(string message)
    {
        var exception = new InvalidOperationException(message);

        Assert.Null(exception.InnerException);
        Assert.Equal(exception.Message, OfficeComExceptionInterpreter.ResolveFailureMessage(exception));
    }
}
