using InToolz.Graph.DemoMode;
using InToolz.Graph.EntraHelperClasses;
using InToolz.Graph.IntuneHelperClasses;

namespace InToolz.Tests;

/// <summary>
/// The Cleanup page's Stale Devices scan, run through the Graph SDK against Demo Mode's store:
/// threshold presets, never-synced devices, and paging past the first page of results.
/// </summary>
public class StaleDeviceTests
{
    /// <summary>
    /// Days since last activity for a spread of devices. Kept clear of the preset boundaries,
    /// since the seed timestamps and the scan's "now" are taken a few milliseconds apart.
    /// </summary>
    private static readonly int?[] DaysSinceActivity = { 10, 45, 75, 100, 150, 200, null };

    [Theory]
    [InlineData(30, 6)]
    [InlineData(60, 5)]
    [InlineData(90, 4)]
    [InlineData(120, 3)]
    [InlineData(180, 2)]
    public async Task GetStaleManagedDevices_EachThresholdPreset_CountsDevicesPastIt(int staleDays, int expectedCount)
    {
        var graph = new DemoGraphTestClient();
        foreach (var days in DaysSinceActivity)
            graph.AddManagedDevice($"DEVICE-{days?.ToString() ?? "NEVER"}", days);

        var stale = await ManagedDeviceHelper.GetStaleManagedDevicesAsync(graph.Client, staleDays);

        Assert.Equal(expectedCount, stale.Count);
    }

    [Theory]
    [InlineData(30, 6)]
    [InlineData(60, 5)]
    [InlineData(90, 4)]
    [InlineData(120, 3)]
    [InlineData(180, 2)]
    public async Task GetStaleEntraDevices_EachThresholdPreset_CountsDevicesPastIt(int staleDays, int expectedCount)
    {
        var graph = new DemoGraphTestClient();
        foreach (var days in DaysSinceActivity)
            graph.AddEntraDevice($"DEVICE-{days?.ToString() ?? "NEVER"}", days);

        var stale = await EntraDeviceHelper.GetStaleEntraDevicesAsync(graph.Client, staleDays);

        Assert.Equal(expectedCount, stale.Count);
    }

    [Fact]
    public async Task GetStaleManagedDevices_NeverSynced_IsStaleAndMapped()
    {
        var graph = new DemoGraphTestClient();
        var id = graph.AddManagedDevice("NEVER-SYNCED", daysSinceSync: null);
        graph.AddManagedDevice("FRESH", daysSinceSync: 1);

        var stale = await ManagedDeviceHelper.GetStaleManagedDevicesAsync(graph.Client, 30);

        var device = Assert.Single(stale);
        Assert.Equal(id, device.DeviceId);
        Assert.Equal("NEVER-SYNCED", device.DeviceName);
        Assert.Equal("Windows", device.OperatingSystem);
        Assert.Equal("SN-NEVER-SYNCED", device.SerialNumber);
        Assert.Null(device.LastSyncDateTime);
    }

    [Fact]
    public async Task GetStaleEntraDevices_NeverSignedIn_IsStaleAndMapped()
    {
        var graph = new DemoGraphTestClient();
        var id = graph.AddEntraDevice("NEVER-SIGNED-IN", daysSinceSignIn: null);
        graph.AddEntraDevice("FRESH", daysSinceSignIn: 1);

        var stale = await EntraDeviceHelper.GetStaleEntraDevicesAsync(graph.Client, 30);

        var device = Assert.Single(stale);
        Assert.Equal(id, device.DeviceObjectId);
        Assert.Equal("NEVER-SIGNED-IN", device.DisplayName);
        Assert.Equal("AzureAd", device.TrustType);
        Assert.Null(device.ApproximateLastSignInDateTime);
    }

    [Fact]
    public async Task StaleScan_DemoModeSourceTenant_FindsTheSeededStaleDevices()
    {
        var graph = new DemoGraphTestClient(DemoDataStore.CreateSourceSeed());

        var intune = await ManagedDeviceHelper.GetStaleManagedDevicesAsync(graph.Client, 90);
        var entra = await EntraDeviceHelper.GetStaleEntraDevicesAsync(graph.Client, 90);

        Assert.Equal(new[] { "DESKTOP-OLD06", "IPAD-EXEC05", "LAPTOP-FIN03", "MACBOOK-SALES04" }, intune.Select(d => d.DeviceName).OrderBy(n => n));
        Assert.Equal(new[] { "LAPTOP-FIN03", "MACBOOK-SALES04", "OLD-SURFACE07" }, entra.Select(d => d.DisplayName).OrderBy(n => n));
    }

    [Fact]
    public async Task GetAllManagedDevices_MoreThanOnePage_ReturnsEveryDevice()
    {
        var graph = new DemoGraphTestClient();
        for (var i = 0; i < 2500; i++)
            graph.AddManagedDevice($"DEVICE-{i:D4}", daysSinceSync: null);

        var devices = await ManagedDeviceHelper.GetAllManagedDevicesAsync(graph.Client);

        Assert.Equal(2500, devices.Count);
        Assert.Equal(2500, devices.Select(d => d.Id).Distinct().Count());
        // $top=1000, so pages of 1000 + 1000 + 500
        Assert.Equal(3, graph.Requests.Count(r => r.Uri.AbsolutePath == "/beta/deviceManagement/managedDevices"));
    }

    [Fact]
    public async Task GetAllEntraDevices_MoreThanOnePage_ReturnsEveryDevice()
    {
        var graph = new DemoGraphTestClient();
        for (var i = 0; i < 2500; i++)
            graph.AddEntraDevice($"DEVICE-{i:D4}", daysSinceSignIn: null);

        var devices = await EntraDeviceHelper.GetAllEntraDevicesAsync(graph.Client);

        Assert.Equal(2500, devices.Count);
        Assert.Equal(2500, devices.Select(d => d.Id).Distinct().Count());
        // $top=999, so pages of 999 + 999 + 502
        Assert.Equal(3, graph.Requests.Count(r => r.Uri.AbsolutePath == "/beta/devices"));
    }
}
