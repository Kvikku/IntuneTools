using System.Text.Json.Nodes;
using InToolz.Graph.IntuneHelperClasses;
using Microsoft.Graph.Beta.Models;

namespace InToolz.Tests;

/// <summary>
/// App assignment, issue #104: All Devices with Required/Uninstall must go through while
/// Available stays blocked, and because Graph's assign action replaces every assignment on the
/// app, the existing ones have to be carried over in the same request.
/// </summary>
public class AppAssignmentTests : IDisposable
{
    private const string AppId = "app-1";

    public AppAssignmentTests() => ResetGlobals();

    public void Dispose() => ResetGlobals();

    private static void ResetGlobals()
    {
        Variables._selectedAppDeploymentIntent = default;
        Variables.SelectedFilterID = string.Empty;
        Variables.deviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        Variables.totalTimeSavedInSeconds = 0;
    }

    [Theory]
    [InlineData("Required", InstallIntent.Required, true)]
    [InlineData("Uninstall", InstallIntent.Uninstall, true)]
    [InlineData("Available", InstallIntent.Available, false)]
    public void DeploymentDialogIntent_WithAllDevices_OnlyAvailableIsBlocked(string comboText, InstallIntent expectedIntent, bool expectedAllowed)
    {
        HelperClass.GetInstallIntent(comboText);

        Assert.Equal(expectedIntent, Variables._selectedAppDeploymentIntent);
        Assert.Equal(expectedAllowed, UserInterfaceHelper.IsAppAssignmentIntentAllowed(Variables._selectedAppDeploymentIntent, includesAllDevices: true));
    }

    [Theory]
    [InlineData(InstallIntent.Available)]
    [InlineData(InstallIntent.Required)]
    [InlineData(InstallIntent.Uninstall)]
    public void IsAppAssignmentIntentAllowed_WithoutAllDevices_AllowsEveryIntent(InstallIntent intent)
    {
        Assert.True(UserInterfaceHelper.IsAppAssignmentIntentAllowed(intent, includesAllDevices: false));
    }

    [Fact]
    public async Task AssignAllDevicesRequired_VppDeviceLicensing_KeepsExistingAssignments()
    {
        var graph = new DemoGraphTestClient();
        AddApp(graph, "#microsoft.graph.iosVppApp");
        AddExistingAssignment(graph, "#microsoft.graph.groupAssignmentTarget", "available", groupId: "group-existing");
        AddExistingAssignment(graph, "#microsoft.graph.exclusionGroupAssignmentTarget", "required", groupId: "group-excluded");
        AddExistingAssignment(graph, "#microsoft.graph.allLicensedUsersAssignmentTarget", "available");
        Variables._selectedAppDeploymentIntent = InstallIntent.Required;
        var vppSettings = ApplicationHelper.CreateiOSVppAppAssignmentSettings(
            useDeviceLicensing: true, uninstallOnDeviceRemoval: false, isRemovable: true, preventManagedAppBackup: false, preventAutoAppUpdate: false);

        await ApplicationHelper.AssignGroupsToApplication(AppId, "VPP App", new List<string> { Variables.allDevicesVirtualGroupID }, graph.Client, vppSettings);

        var assignments = StoredAssignments(graph);
        Assert.Equal(4, assignments.Count);

        var allDevices = Assert.Single(assignments, a => TargetType(a) == "#microsoft.graph.allDevicesAssignmentTarget");
        Assert.Equal("required", (string?)allDevices["intent"]);
        Assert.Equal("#microsoft.graph.iosVppAppAssignmentSettings", (string?)allDevices["settings"]?["@odata.type"]);
        Assert.True((bool?)allDevices["settings"]?["useDeviceLicensing"]);

        var group = Assert.Single(assignments, a => TargetType(a) == "#microsoft.graph.groupAssignmentTarget");
        Assert.Equal("group-existing", (string?)group["target"]?["groupId"]);
        Assert.Equal("available", (string?)group["intent"]);

        var exclusion = Assert.Single(assignments, a => TargetType(a) == "#microsoft.graph.exclusionGroupAssignmentTarget");
        Assert.Equal("group-excluded", (string?)exclusion["target"]?["groupId"]);

        var allUsers = Assert.Single(assignments, a => TargetType(a) == "#microsoft.graph.allLicensedUsersAssignmentTarget");
        Assert.Equal("available", (string?)allUsers["intent"]);
    }

    [Fact]
    public async Task AssignAllDevices_AlreadyAssigned_ReplacesItInsteadOfDuplicating()
    {
        var graph = new DemoGraphTestClient();
        AddApp(graph, "#microsoft.graph.win32LobApp");
        AddExistingAssignment(graph, "#microsoft.graph.allDevicesAssignmentTarget", "uninstall");
        Variables._selectedAppDeploymentIntent = InstallIntent.Required;

        await ApplicationHelper.AssignGroupsToApplication(AppId, "Win32 App", new List<string> { Variables.allDevicesVirtualGroupID }, graph.Client);

        var assignment = Assert.Single(StoredAssignments(graph));
        Assert.Equal("#microsoft.graph.allDevicesAssignmentTarget", TargetType(assignment));
        Assert.Equal("required", (string?)assignment["intent"]);
    }

    [Fact]
    public async Task AssignGroup_AlreadyAssigned_ReplacesItInsteadOfDuplicating()
    {
        var graph = new DemoGraphTestClient();
        AddApp(graph, "#microsoft.graph.win32LobApp");
        AddExistingAssignment(graph, "#microsoft.graph.groupAssignmentTarget", "available", groupId: "group-a");
        Variables._selectedAppDeploymentIntent = InstallIntent.Required;

        await ApplicationHelper.AssignGroupsToApplication(AppId, "Win32 App", new List<string> { "group-a" }, graph.Client);

        var assignment = Assert.Single(StoredAssignments(graph));
        Assert.Equal("group-a", (string?)assignment["target"]?["groupId"]);
        Assert.Equal("required", (string?)assignment["intent"]);
    }

    [Fact]
    public async Task AssignVppUninstall_DropsSettingsThatOnlyApplyToInstalls()
    {
        var graph = new DemoGraphTestClient();
        AddApp(graph, "#microsoft.graph.iosVppApp");
        Variables._selectedAppDeploymentIntent = InstallIntent.Uninstall;
        var vppSettings = ApplicationHelper.CreateiOSVppAppAssignmentSettings(
            useDeviceLicensing: true, uninstallOnDeviceRemoval: true, isRemovable: true, preventManagedAppBackup: true, preventAutoAppUpdate: true);

        await ApplicationHelper.AssignGroupsToApplication(AppId, "VPP App", new List<string> { Variables.allDevicesVirtualGroupID }, graph.Client, vppSettings);

        var settings = Assert.Single(StoredAssignments(graph))["settings"];
        Assert.NotNull(settings);
        Assert.True((bool?)settings["useDeviceLicensing"]);
        Assert.Null(settings["isRemovable"]);
        Assert.Null(settings["preventAutoAppUpdate"]);
        Assert.Null(settings["preventManagedAppBackup"]);
        Assert.Null(settings["uninstallOnDeviceRemoval"]);
    }

    [Fact]
    public async Task AssignVppAvailable_DropsIsRemovableOnly()
    {
        var graph = new DemoGraphTestClient();
        AddApp(graph, "#microsoft.graph.iosVppApp");
        Variables._selectedAppDeploymentIntent = InstallIntent.Available;
        var vppSettings = ApplicationHelper.CreateiOSVppAppAssignmentSettings(
            useDeviceLicensing: false, uninstallOnDeviceRemoval: true, isRemovable: true, preventManagedAppBackup: true, preventAutoAppUpdate: true);

        await ApplicationHelper.AssignGroupsToApplication(AppId, "VPP App", new List<string> { "group-a" }, graph.Client, vppSettings);

        var settings = Assert.Single(StoredAssignments(graph))["settings"];
        Assert.NotNull(settings);
        Assert.Null(settings["isRemovable"]);
        Assert.True((bool?)settings["uninstallOnDeviceRemoval"]);
        Assert.True((bool?)settings["preventManagedAppBackup"]);
        Assert.True((bool?)settings["preventAutoAppUpdate"]);
    }

    [Fact]
    public async Task AssignAppWithoutSettings_DemoModeIosStoreApp_Succeeds()
    {
        // Apps that take no assignment settings get Settings = null, which the Graph SDK
        // serializes as "settings":null twice — Demo Mode has to accept that like Graph does.
        var graph = new DemoGraphTestClient(Graph.DemoMode.DemoDataStore.CreateSourceSeed());
        var teams = graph.Store.GetCollection("mobileApps").OfType<JsonObject>().Single(a => (string?)a["displayName"] == "Microsoft Teams");
        var teamsId = (string)teams["id"]!;
        Variables._selectedAppDeploymentIntent = InstallIntent.Required;

        await ApplicationHelper.AssignGroupsToApplication(teamsId, "Microsoft Teams", new List<string> { "group-a" }, graph.Client);

        var assignment = Assert.Single(graph.Store.GetAssignments("mobileApps", teamsId).OfType<JsonObject>());
        Assert.Equal("group-a", (string?)assignment["target"]?["groupId"]);
        Assert.Null(assignment["settings"]);
    }

    private static void AddApp(DemoGraphTestClient graph, string odataType) =>
        graph.Store.Add("mobileApps", new JsonObject
        {
            ["id"] = AppId,
            ["@odata.type"] = odataType,
            ["displayName"] = "Test App"
        });

    private static void AddExistingAssignment(DemoGraphTestClient graph, string targetType, string intent, string? groupId = null)
    {
        var target = new JsonObject { ["@odata.type"] = targetType };
        if (groupId != null) target["groupId"] = groupId;

        graph.Store.GetAssignments("mobileApps", AppId).Add(new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["@odata.type"] = "#microsoft.graph.mobileAppAssignment",
            ["intent"] = intent,
            ["target"] = target
        });
    }

    private static List<JsonObject> StoredAssignments(DemoGraphTestClient graph) =>
        graph.Store.GetAssignments("mobileApps", AppId).OfType<JsonObject>().ToList();

    private static string? TargetType(JsonObject assignment) => (string?)assignment["target"]?["@odata.type"];
}
