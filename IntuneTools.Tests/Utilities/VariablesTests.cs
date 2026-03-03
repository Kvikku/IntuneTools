using IntuneTools.Utilities;

namespace IntuneTools.Tests.Utilities
{
    public class VariablesTests
    {
        [Fact]
        public void AppVersion_IsNotNullOrEmpty()
        {
            Assert.False(string.IsNullOrEmpty(Variables.appVersion));
        }

        [Fact]
        public void AppVersion_IsValidVersionFormat()
        {
            Assert.True(Version.TryParse(Variables.appVersion, out _),
                $"appVersion '{Variables.appVersion}' is not a valid version format.");
        }

        [Fact]
        public void AllUsersVirtualGroupID_IsExpectedValue()
        {
            Assert.Equal("acacacac-9df4-4c7d-9d50-4ef0226f57a9", Variables.allUsersVirtualGroupID);
        }

        [Fact]
        public void AllDevicesVirtualGroupID_IsExpectedValue()
        {
            Assert.Equal("adadadad-808e-44e2-905a-0b7873a8a531", Variables.allDevicesVirtualGroupID);
        }

        [Fact]
        public void TimeSavedConstants_ArePositive()
        {
            Assert.True(Variables.secondsSavedOnAssignments > 0);
            Assert.True(Variables.secondsSavedOnRenaming > 0);
            Assert.True(Variables.secondsSavedOnDeleting > 0);
            Assert.True(Variables.secondsSavedOnImporting > 0);
        }

        [Fact]
        public void LogLevels_HasExpectedValues()
        {
            Assert.True(Enum.IsDefined(typeof(Variables.LogLevels), Variables.LogLevels.Info));
            Assert.True(Enum.IsDefined(typeof(Variables.LogLevels), Variables.LogLevels.Warning));
            Assert.True(Enum.IsDefined(typeof(Variables.LogLevels), Variables.LogLevels.Error));
        }

        [Fact]
        public void RenameMode_HasExpectedValues()
        {
            Assert.Equal(0, (int)Variables.RenameMode.Prefix);
            Assert.Equal(1, (int)Variables.RenameMode.Description);
            Assert.Equal(2, (int)Variables.RenameMode.RemovePrefix);
        }

        [Fact]
        public void AppFunction_HasExpectedValues()
        {
            var values = Enum.GetValues<Variables.appFunction>();
            Assert.Contains(Variables.appFunction.Main, values);
            Assert.Contains(Variables.appFunction.Summary, values);
            Assert.Contains(Variables.appFunction.Import, values);
            Assert.Contains(Variables.appFunction.Assignment, values);
            Assert.Contains(Variables.appFunction.Rename, values);
            Assert.Contains(Variables.appFunction.Delete, values);
        }

        [Fact]
        public void AppDataPath_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(Variables.appDataPath));
        }

        [Fact]
        public void AppFolderName_IsNotEmpty()
        {
            Assert.False(string.IsNullOrEmpty(Variables.appFolderName));
        }
    }
}
