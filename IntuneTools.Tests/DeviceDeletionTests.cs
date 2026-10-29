using System.Net;
using IntuneTools.Graph.EntraHelperClasses;
using IntuneTools.Graph.IntuneHelperClasses;
using Microsoft.Graph.Beta.Models.ODataErrors;

namespace IntuneTools.Tests;

/// <summary>
/// Stale device deletion has no undo, so these pin down exactly which Graph calls are made, and
/// that a missing scope surfaces as an error instead of a silent no-op.
/// </summary>
public class DeviceDeletionTests
{
    [Fact]
    public async Task DeleteManagedDevice_SendsOneDeleteForThatRecordOnly()
    {
        var graph = new DemoGraphTestClient();
        var target = graph.AddManagedDevice("STALE", daysSinceSync: 200);
        var other = graph.AddManagedDevice("KEEP", daysSinceSync: 1);

        await ManagedDeviceHelper.DeleteManagedDeviceAsync(graph.Client, target);

        var request = Assert.Single(graph.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/beta/deviceManagement/managedDevices/{target}", request.Uri.AbsolutePath);
        Assert.Null(graph.Store.GetById("managedDevices", target));
        Assert.NotNull(graph.Store.GetById("managedDevices", other));
    }

    [Fact]
    public async Task DeleteManagedDevice_NeverRetiresOrWipesTheDevice()
    {
        var graph = new DemoGraphTestClient();
        var target = graph.AddManagedDevice("STALE", daysSinceSync: 200);

        await ManagedDeviceHelper.DeleteManagedDeviceAsync(graph.Client, target);

        Assert.DoesNotContain(graph.Requests, r =>
            r.Method == HttpMethod.Post ||
            r.Uri.AbsolutePath.EndsWith("/retire", StringComparison.OrdinalIgnoreCase) ||
            r.Uri.AbsolutePath.EndsWith("/wipe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteEntraDevice_SendsOneDeleteForThatObjectOnly()
    {
        var graph = new DemoGraphTestClient();
        var target = graph.AddEntraDevice("STALE", daysSinceSignIn: 200);
        var other = graph.AddEntraDevice("KEEP", daysSinceSignIn: 1);

        await EntraDeviceHelper.DeleteEntraDeviceAsync(graph.Client, target);

        var request = Assert.Single(graph.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/beta/devices/{target}", request.Uri.AbsolutePath);
        Assert.Null(graph.Store.GetById("devices", target));
        Assert.NotNull(graph.Store.GetById("devices", other));
    }

    [Fact]
    public async Task DeleteEntraDevice_WithoutDeviceReadWriteAll_ThrowsAndKeepsTheDevice()
    {
        var graph = new DemoGraphTestClient();
        var target = graph.AddEntraDevice("STALE", daysSinceSignIn: 200);
        graph.Intercept = request => request.Method == HttpMethod.Delete ? DemoGraphTestClient.Forbidden() : null;

        var error = await Assert.ThrowsAsync<ODataError>(() => EntraDeviceHelper.DeleteEntraDeviceAsync(graph.Client, target));

        Assert.Equal((int)HttpStatusCode.Forbidden, error.ResponseStatusCode);
        Assert.NotNull(graph.Store.GetById("devices", target));
    }

    [Fact]
    public async Task DeleteManagedDevice_Forbidden_ThrowsAndKeepsTheRecord()
    {
        var graph = new DemoGraphTestClient();
        var target = graph.AddManagedDevice("STALE", daysSinceSync: 200);
        graph.Intercept = request => request.Method == HttpMethod.Delete ? DemoGraphTestClient.Forbidden() : null;

        var error = await Assert.ThrowsAsync<ODataError>(() => ManagedDeviceHelper.DeleteManagedDeviceAsync(graph.Client, target));

        Assert.Equal((int)HttpStatusCode.Forbidden, error.ResponseStatusCode);
        Assert.NotNull(graph.Store.GetById("managedDevices", target));
    }
}
