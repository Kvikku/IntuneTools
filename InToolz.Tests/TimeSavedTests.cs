using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Tests;

/// <summary>
/// Tests for TimeSaved counter logic. Each test resets global state to avoid cross-test pollution.
/// </summary>
public class TimeSavedTests : IDisposable
{
    public TimeSavedTests()
    {
        // Reset mutable state before each test
        Variables.totalTimeSavedInSeconds = 0;
        Variables.numberOfItemsRenamed = 0;
        Variables.numberOfItemsDeleted = 0;
        Variables.numberOfItemsImported = 0;
        Variables.numberOfItemsAssigned = 0;
        Variables.numberOfItemsCheckedForAssignments = 0;
    }

    public void Dispose()
    {
        // Reset after test to avoid leaking state
        Variables.totalTimeSavedInSeconds = 0;
        Variables.numberOfItemsRenamed = 0;
        Variables.numberOfItemsDeleted = 0;
        Variables.numberOfItemsImported = 0;
        Variables.numberOfItemsAssigned = 0;
        Variables.numberOfItemsCheckedForAssignments = 0;
    }

    [Fact]
    public void UpdateTotalTimeSaved_AccumulatesSeconds()
    {
        TimeSaved.UpdateTotalTimeSaved(30, appFunction.Assignment);
        var result = TimeSaved.UpdateTotalTimeSaved(20, appFunction.Assignment);

        Assert.Equal(50, result);
        Assert.Equal(50, TimeSaved.GetTotalTimeSaved());
    }

    [Fact]
    public void UpdateTotalTimeSaved_IncrementsRenameCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(20, appFunction.Rename);

        Assert.Equal(1, Variables.numberOfItemsRenamed);
        Assert.Equal(0, Variables.numberOfItemsDeleted);
    }

    [Fact]
    public void UpdateTotalTimeSaved_IncrementsDeleteCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(10, appFunction.Delete);

        Assert.Equal(1, Variables.numberOfItemsDeleted);
        Assert.Equal(0, Variables.numberOfItemsRenamed);
    }

    [Fact]
    public void UpdateTotalTimeSaved_IncrementsImportCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(90, appFunction.Import);

        Assert.Equal(1, Variables.numberOfItemsImported);
    }

    [Fact]
    public void UpdateTotalTimeSaved_IncrementsAssignmentCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(30, appFunction.Assignment);

        Assert.Equal(1, Variables.numberOfItemsAssigned);
    }

    [Fact]
    public void UpdateTotalTimeSaved_IncrementsFindUnassignedCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(30, appFunction.FindUnassigned);

        Assert.Equal(1, Variables.numberOfItemsCheckedForAssignments);
    }

    [Fact]
    public void UpdateTotalTimeSaved_MainFunction_DoesNotIncrementAnyItemCounter()
    {
        TimeSaved.UpdateTotalTimeSaved(10, appFunction.Main);

        Assert.Equal(10, TimeSaved.GetTotalTimeSaved());
        Assert.Equal(0, Variables.numberOfItemsRenamed);
        Assert.Equal(0, Variables.numberOfItemsDeleted);
        Assert.Equal(0, Variables.numberOfItemsImported);
        Assert.Equal(0, Variables.numberOfItemsAssigned);
        Assert.Equal(0, Variables.numberOfItemsCheckedForAssignments);
    }

    [Fact]
    public void GetTotalTimeSavedInMinutes_TruncatesToWholeMinutes()
    {
        TimeSaved.UpdateTotalTimeSaved(150, appFunction.Main); // 2.5 minutes

        Assert.Equal(2, TimeSaved.GetTotalTimeSavedInMinutes());
    }

    [Fact]
    public void GetTotalTimeSavedInMinutes_ZeroSeconds_ReturnsZero()
    {
        Assert.Equal(0, TimeSaved.GetTotalTimeSavedInMinutes());
    }
}
