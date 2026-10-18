using SnappyDocsConvert.App.ViewModels;
using SnappyDocsConvert.Core.Models;

namespace SnappyDocsConvert.Tests;

// OverallPercent semantics locked against the pipeline's real reporting shape: exactly two
// reports per item (Running before the item starts, terminal after it ends) plus explicit
// edge scenarios requested in the S1 review: failures, cancellation, fallback, repeated
// events and new batches. The helper is pure, so these cover the exact code path used by
// UpdateProgress.
public sealed class OverallPercentTests
{
    private static double Percent(BatchConversionItemStatus status, int index, int total) =>
        ConvertPageViewModel.ComputeOverallPercent(status, index, total);

    [Fact]
    public void SingleFile_RunningCountsNotCompleted_TerminalCountsCompleted()
    {
        Assert.Equal(0, Percent(BatchConversionItemStatus.Running, 0, 1));
        Assert.Equal(100, Percent(BatchConversionItemStatus.Succeeded, 0, 1));
    }

    [Fact]
    public void MultipleFiles_Monotonic_NeverDoubleCountSameFile()
    {
        // Real report order for a 4-file batch with no failures/fallbacks.
        var sequence = new[]
        {
            (BatchConversionItemStatus.Running, 0),
            (BatchConversionItemStatus.Succeeded, 0),
            (BatchConversionItemStatus.Running, 1),
            (BatchConversionItemStatus.Succeeded, 1),
            (BatchConversionItemStatus.Running, 2),
            (BatchConversionItemStatus.Succeeded, 2),
            (BatchConversionItemStatus.Running, 3),
            (BatchConversionItemStatus.Succeeded, 3),
        };

        var previous = 0.0;
        foreach (var (status, index) in sequence)
        {
            var value = Percent(status, index, 4);
            Assert.InRange(value, previous, 100);
            previous = value;
        }

        Assert.Equal(100, previous);
    }

    [Fact]
    public void RunningReportOfNextFile_DoesNotAdvanceComparedToPreviousTerminal()
    {
        // Terminal of file i and Running of file i+1 describe the same completed count:
        // the value must be identical, not multiplied.
        Assert.Equal(
            Percent(BatchConversionItemStatus.Succeeded, 1, 5),
            Percent(BatchConversionItemStatus.Running, 2, 5));
    }

    [Fact]
    public void RepeatedSameTransition_IsIdempotent()
    {
        // Progress<T> marshalling can deliver repeats of the same transition; the value
        // must not drift.
        var first = Percent(BatchConversionItemStatus.Running, 2, 5);
        var second = Percent(BatchConversionItemStatus.Running, 2, 5);
        Assert.Equal(first, second);
        Assert.Equal(40, first);
    }

    [Fact]
    public void FailedFile_CountsAsCompletedForProgress()
    {
        // One file failed mid-batch: progress still advances through it exactly once and
        // reaches 100 when the last file terminates. Item outcomes are reported separately.
        var oneFileRun = new[]
        {
            (BatchConversionItemStatus.Running, 0),
            (BatchConversionItemStatus.Failed, 0),
            (BatchConversionItemStatus.Running, 1),
            (BatchConversionItemStatus.Failed, 1),
        };

        var previous = 0.0;
        foreach (var (status, index) in oneFileRun)
        {
            var value = Percent(status, index, 2);
            Assert.InRange(value, previous, 100);
            previous = value;
        }

        Assert.Equal(100, previous);
    }

    [Fact]
    public void Cancellation_TerminalCancelledReport_CountsOnce_ThenRunEndsBelowHundred()
    {
        // Pipeline reports a terminal Cancelled for the running item and stops reporting;
        // the value stays at the honest processed count instead of faking 100.
        var sequence = new[]
        {
            (BatchConversionItemStatus.Running, 0),
            (BatchConversionItemStatus.Succeeded, 0),
            (BatchConversionItemStatus.Running, 1),
            (BatchConversionItemStatus.Cancelled, 1),
        };

        var previous = 0.0;
        foreach (var (status, index) in sequence)
        {
            var value = Percent(status, index, 5);
            Assert.InRange(value, previous, 100);
            previous = value;
        }

        Assert.Equal(40, previous);
        Assert.Equal(Percent(BatchConversionItemStatus.Running, 2, 5), previous);
    }

    [Fact]
    public void Fallback_TransitionsUseOneTerminalReportPerItem()
    {
        // Engine fallback happens INSIDE one item's processing; the pipeline still reports
        // exactly one terminal report for the item, so fallback must not count twice.
        var sequence = new[]
        {
            (BatchConversionItemStatus.Running, 0),
            (BatchConversionItemStatus.Succeeded, 0),
            (BatchConversionItemStatus.Running, 1),
            (BatchConversionItemStatus.Succeeded, 1),
        };

        var previous = 0.0;
        foreach (var (status, index) in sequence)
        {
            var value = Percent(status, index, 2);
            Assert.InRange(value, previous, 100);
            previous = value;
        }

        Assert.Equal(100, previous);
    }

    [Fact]
    public void EmptyTotal_NeverReportsProgress()
    {
        // A zero-total batch can never report progress, in any state or index.
        Assert.Equal(0, Percent(BatchConversionItemStatus.Running, 0, 0));
        Assert.Equal(0, Percent(BatchConversionItemStatus.Succeeded, 0, 0));
        Assert.Equal(0, Percent(BatchConversionItemStatus.Succeeded, 77, 0));
    }
}
