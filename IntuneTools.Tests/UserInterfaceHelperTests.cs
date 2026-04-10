using IntuneTools.Utilities;
using System.Collections.ObjectModel;
using Xunit;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Tests;

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
    [InlineData("AppSomething", true)]
    [InlineData("appOther", true)]
    public void IsApplicationContentType_MatchingTypes_ReturnsTrue(string contentType, bool expected)
    {
        Assert.Equal(expected, UserInterfaceHelper.IsApplicationContentType(contentType));
    }

    [Theory]
    [InlineData("Settings Catalog", false)]
    [InlineData("Device Compliance Policy", false)]
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
        var logMessages = new List<string>();

        var count = await UserInterfaceHelper.ExecuteBatchOperationAsync(
            ids,
            id => { processedIds.Add(id); return Task.CompletedTask; },
            "Policy",
            "Deleted",
            msg => logMessages.Add(msg),
            10,
            appFunction.Delete);

        Assert.Equal(3, count);
        Assert.Equal(ids, processedIds);
        Assert.Equal(3, logMessages.Count);
        Assert.All(logMessages, msg => Assert.Contains("Deleted", msg));
    }

    [Fact]
    public async Task ExecuteBatchOperationAsync_PartialFailure_ContinuesAndReportsErrors()
    {
        var ids = new List<string> { "id-1", "id-2", "id-3" };
        var logMessages = new List<string>();
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
            msg => logMessages.Add(msg),
            20,
            appFunction.Rename);

        Assert.Equal(2, count); // 2 succeeded
        Assert.Equal(3, callCount); // all 3 were attempted
        Assert.Contains(logMessages, m => m.Contains("Error") && m.Contains("id-2"));
    }

    [Fact]
    public async Task ExecuteBatchOperationAsync_EmptyIds_ReturnsZero()
    {
        var count = await UserInterfaceHelper.ExecuteBatchOperationAsync(
            new List<string>(),
            _ => Task.CompletedTask,
            "Policy",
            "Deleted",
            _ => { },
            10,
            appFunction.Delete);

        Assert.Equal(0, count);
    }
}
