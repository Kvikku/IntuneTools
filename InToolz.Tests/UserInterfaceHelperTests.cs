using static InToolz.Utilities.Variables;

namespace InToolz.Tests;

public class UserInterfaceHelperTests : IDisposable
{
    public UserInterfaceHelperTests()
    {
        Variables.totalTimeSavedInSeconds = 0;
        Variables.numberOfItemsRenamed = 0;
        Variables.numberOfItemsDeleted = 0;
    }

    public void Dispose()
    {
        Variables.totalTimeSavedInSeconds = 0;
        Variables.numberOfItemsRenamed = 0;
        Variables.numberOfItemsDeleted = 0;
    }

    [Theory]
    [InlineData("Application", true)]
    [InlineData("application", true)]
    [InlineData("APPLICATION", true)]
    [InlineData("App - Windows app (Win32)", true)]
    [InlineData("app - iOS VPP app", true)]
    public void IsApplicationContentType_MatchingTypes_ReturnsTrue(string contentType, bool expected)
    {
        Assert.Equal(expected, UserInterfaceHelper.IsApplicationContentType(contentType));
    }

    [Theory]
    [InlineData("Settings Catalog", false)]
    [InlineData("Device Compliance Policy", false)]
    [InlineData("Apple BYOD Enrollment Profile", false)]
    [InlineData("AppSomething", false)]
    [InlineData("", false)]
    [InlineData("NotApplication", false)]
    public void IsApplicationContentType_NonMatchingTypes_ReturnsFalse(string contentType, bool expected)
    {
        Assert.Equal(expected, UserInterfaceHelper.IsApplicationContentType(contentType));
    }

    [Fact]
    public void IsApplicationContentType_Null_ReturnsFalse()
    {
        Assert.False(UserInterfaceHelper.IsApplicationContentType(null));
    }

    [Fact]
    public async Task PopulateCollectionAsync_AddsItemsAndReturnsCount()
    {
        var collection = new ObservableCollection<string>();
        var items = new[] { "a", "b", "c" };

        var count = await UserInterfaceHelper.PopulateCollectionAsync(
            collection,
            () => Task.FromResult<IEnumerable<string>>(items));

        Assert.Equal(3, count);
        Assert.Equal(new[] { "a", "b", "c" }, collection);
    }

    [Fact]
    public async Task PopulateCollectionAsync_EmptyLoader_ReturnsZero()
    {
        var collection = new ObservableCollection<string>();

        var count = await UserInterfaceHelper.PopulateCollectionAsync(
            collection,
            () => Task.FromResult<IEnumerable<string>>(Array.Empty<string>()));

        Assert.Equal(0, count);
        Assert.Empty(collection);
    }

    [Fact]
    public async Task PopulateCollectionAsync_WithMapper_TransformsItems()
    {
        var collection = new ObservableCollection<string>();
        var items = new[] { 1, 2, 3 };

        var count = await UserInterfaceHelper.PopulateCollectionAsync(
            collection,
            () => Task.FromResult<IEnumerable<int>>(items),
            i => $"item-{i}");

        Assert.Equal(3, count);
        Assert.Equal(new[] { "item-1", "item-2", "item-3" }, collection);
    }

    [Fact]
    public async Task ExecuteBatchOperationAsync_AllSucceed_ReturnsCount()
    {
        var ids = new List<string> { "id-1", "id-2", "id-3" };
        var processedIds = new List<string>();

        var count = await UserInterfaceHelper.ExecuteBatchOperationAsync(
            ids,
            id => { processedIds.Add(id); return Task.CompletedTask; },
            "Policy",
            "Deleted",
            10,
            appFunction.Delete);

        Assert.Equal(3, count);
        Assert.Equal(ids, processedIds);
    }

    [Fact]
    public async Task ExecuteBatchOperationAsync_PartialFailure_ContinuesAndReportsErrors()
    {
        var ids = new List<string> { "id-1", "id-2", "id-3" };
        var callCount = 0;

        var count = await UserInterfaceHelper.ExecuteBatchOperationAsync(
            ids,
            id =>
            {
                callCount++;
                if (id == "id-2") throw new InvalidOperationException("Simulated failure");
                return Task.CompletedTask;
            },
            "Policy",
            "Renamed",
            20,
            appFunction.Rename);

        Assert.Equal(2, count); // 2 succeeded
        Assert.Equal(3, callCount); // all 3 were attempted
    }

    [Fact]
    public async Task ExecuteBatchOperationAsync_EmptyIds_ReturnsZero()
    {
        var count = await UserInterfaceHelper.ExecuteBatchOperationAsync(
            new List<string>(),
            _ => Task.CompletedTask,
            "Policy",
            "Deleted",
            10,
            appFunction.Delete);

        Assert.Equal(0, count);
    }

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDeviceStale_NullLastActivity_ReturnsTrue()
    {
        Assert.True(UserInterfaceHelper.IsDeviceStale(null, 90, Now));
    }

    [Fact]
    public void IsDeviceStale_LastActivityOlderThanThreshold_ReturnsTrue()
    {
        var lastActivity = Now.AddDays(-91);

        Assert.True(UserInterfaceHelper.IsDeviceStale(lastActivity, 90, Now));
    }

    [Fact]
    public void IsDeviceStale_LastActivityExactlyAtThreshold_ReturnsTrue()
    {
        var lastActivity = Now.AddDays(-90);

        Assert.True(UserInterfaceHelper.IsDeviceStale(lastActivity, 90, Now));
    }

    [Fact]
    public void IsDeviceStale_LastActivityWithinThreshold_ReturnsFalse()
    {
        var lastActivity = Now.AddDays(-30);

        Assert.False(UserInterfaceHelper.IsDeviceStale(lastActivity, 90, Now));
    }

    [Fact]
    public void IsDeviceStale_ZeroThreshold_OnlyFlagsPastActivity()
    {
        Assert.True(UserInterfaceHelper.IsDeviceStale(Now, 0, Now));
        Assert.False(UserInterfaceHelper.IsDeviceStale(Now.AddSeconds(1), 0, Now));
    }

    [Fact]
    public void IsDeviceStale_NegativeThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UserInterfaceHelper.IsDeviceStale(Now, -1, Now));
    }

    // --- Bulk-operation confirmation ---------------------------------------------------------
    // Six pages used to hardcode "10 or more items" next to their own copy of the dialog. A wrong
    // answer here means a bulk delete, rename or import without the safety prompt.

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(500, true)]
    public void RequiresBulkConfirmation_AtAndAroundTheThreshold(int itemCount, bool expected)
    {
        Assert.Equal(expected, UserInterfaceHelper.RequiresBulkConfirmation(itemCount));
    }

    [Fact]
    public void RequiresBulkConfirmation_ThresholdIsTen()
    {
        // The number users have been told about ("warn when 10 or more items"). Changing it is a
        // product decision, not a refactor, so it should take a test edit.
        Assert.Equal(10, UserInterfaceHelper.BulkOperationConfirmationThreshold);
    }

    [Fact]
    public void RequiresBulkConfirmation_NegativeCount_DoesNotRequireConfirmation()
    {
        Assert.False(UserInterfaceHelper.RequiresBulkConfirmation(-1));
    }

    // --- Assignment target -------------------------------------------------------------------

    [Theory]
    [InlineData(0, false, false, false)]   // nothing chosen: the dialog must not close
    [InlineData(1, false, false, true)]    // one group
    [InlineData(25, false, false, true)]   // many groups
    [InlineData(0, true, false, true)]     // All Users alone
    [InlineData(0, false, true, true)]     // All Devices alone
    [InlineData(0, true, true, true)]      // both virtual groups, no real group
    [InlineData(3, true, true, true)]      // everything
    public void HasAssignmentTarget_NeedsAGroupOrAVirtualGroup(int selectedGroupCount, bool allUsers, bool allDevices, bool expected)
    {
        Assert.Equal(expected, UserInterfaceHelper.HasAssignmentTarget(selectedGroupCount, allUsers, allDevices));
    }
}
