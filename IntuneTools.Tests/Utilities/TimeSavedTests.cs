using IntuneTools.Utilities;

namespace IntuneTools.Tests.Utilities
{
    public class TimeSavedTests : IDisposable
    {
        public TimeSavedTests()
        {
            // Reset static state before each test
            Variables.totalTimeSavedInSeconds = 0;
            Variables.numberOfItemsRenamed = 0;
            Variables.numberOfItemsDeleted = 0;
            Variables.numberOfItemsImported = 0;
            Variables.numberOfItemsAssigned = 0;
        }

        public void Dispose()
        {
            // Clean up static state after each test
            Variables.totalTimeSavedInSeconds = 0;
            Variables.numberOfItemsRenamed = 0;
            Variables.numberOfItemsDeleted = 0;
            Variables.numberOfItemsImported = 0;
            Variables.numberOfItemsAssigned = 0;
        }

        [Fact]
        public void GetTotalTimeSaved_InitialValue_ReturnsZero()
        {
            Assert.Equal(0, TimeSaved.GetTotalTimeSaved());
        }

        [Fact]
        public void GetTotalTimeSavedInMinutes_InitialValue_ReturnsZero()
        {
            Assert.Equal(0, TimeSaved.GetTotalTimeSavedInMinutes());
        }

        [Fact]
        public void UpdateTotalTimeSaved_Rename_IncrementsTimeAndCount()
        {
            var result = TimeSaved.UpdateTotalTimeSaved(20, Variables.appFunction.Rename);

            Assert.Equal(20, result);
            Assert.Equal(20, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(1, Variables.numberOfItemsRenamed);
        }

        [Fact]
        public void UpdateTotalTimeSaved_Delete_IncrementsTimeAndCount()
        {
            var result = TimeSaved.UpdateTotalTimeSaved(10, Variables.appFunction.Delete);

            Assert.Equal(10, result);
            Assert.Equal(10, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(1, Variables.numberOfItemsDeleted);
        }

        [Fact]
        public void UpdateTotalTimeSaved_Import_IncrementsTimeAndCount()
        {
            var result = TimeSaved.UpdateTotalTimeSaved(90, Variables.appFunction.Import);

            Assert.Equal(90, result);
            Assert.Equal(90, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(1, Variables.numberOfItemsImported);
        }

        [Fact]
        public void UpdateTotalTimeSaved_Assignment_IncrementsTimeAndCount()
        {
            var result = TimeSaved.UpdateTotalTimeSaved(30, Variables.appFunction.Assignment);

            Assert.Equal(30, result);
            Assert.Equal(30, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(1, Variables.numberOfItemsAssigned);
        }

        [Fact]
        public void UpdateTotalTimeSaved_MultipleCalls_AccumulatesTime()
        {
            TimeSaved.UpdateTotalTimeSaved(20, Variables.appFunction.Rename);
            TimeSaved.UpdateTotalTimeSaved(30, Variables.appFunction.Assignment);
            TimeSaved.UpdateTotalTimeSaved(10, Variables.appFunction.Delete);

            Assert.Equal(60, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(1, TimeSaved.GetTotalTimeSavedInMinutes());
            Assert.Equal(1, Variables.numberOfItemsRenamed);
            Assert.Equal(1, Variables.numberOfItemsAssigned);
            Assert.Equal(1, Variables.numberOfItemsDeleted);
        }

        [Fact]
        public void UpdateTotalTimeSaved_MainFunction_DoesNotIncrementAnyItemCount()
        {
            TimeSaved.UpdateTotalTimeSaved(50, Variables.appFunction.Main);

            Assert.Equal(50, TimeSaved.GetTotalTimeSaved());
            Assert.Equal(0, Variables.numberOfItemsRenamed);
            Assert.Equal(0, Variables.numberOfItemsDeleted);
            Assert.Equal(0, Variables.numberOfItemsImported);
            Assert.Equal(0, Variables.numberOfItemsAssigned);
        }

        [Fact]
        public void GetTotalTimeSavedInMinutes_LessThanSixtySeconds_ReturnsZero()
        {
            TimeSaved.UpdateTotalTimeSaved(59, Variables.appFunction.Rename);
            Assert.Equal(0, TimeSaved.GetTotalTimeSavedInMinutes());
        }

        [Fact]
        public void GetTotalTimeSavedInMinutes_ExactlyOneMinute_ReturnsOne()
        {
            TimeSaved.UpdateTotalTimeSaved(60, Variables.appFunction.Rename);
            Assert.Equal(1, TimeSaved.GetTotalTimeSavedInMinutes());
        }

        [Fact]
        public void GetTotalTimeSavedInMinutes_MultipleMinutes_ReturnsCorrectValue()
        {
            TimeSaved.UpdateTotalTimeSaved(150, Variables.appFunction.Rename);
            Assert.Equal(2, TimeSaved.GetTotalTimeSavedInMinutes());
        }
    }
}
