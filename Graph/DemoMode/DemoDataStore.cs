using System.Text.Json.Nodes;

namespace IntuneTools.Graph.DemoMode;

/// <summary>
/// In-memory fake tenant backing Demo Mode. Holds one JSON array per Graph collection plus a
/// side table of assignments keyed by "{collection}/{itemId}", mutated in place by
/// <see cref="DemoGraphMessageHandler"/> as the app lists/creates/renames/deletes/assigns content.
/// </summary>
internal sealed class DemoDataStore
{
    private readonly Dictionary<string, JsonArray> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JsonArray> _assignments = new(StringComparer.OrdinalIgnoreCase);

    public string TenantDisplayName { get; }
    public string TenantId { get; } = Guid.NewGuid().ToString();

    private DemoDataStore(string tenantDisplayName)
    {
        TenantDisplayName = tenantDisplayName;
    }

    public JsonArray GetCollection(string segment) =>
        _collections.TryGetValue(segment, out var arr) ? arr : _collections[segment] = new JsonArray();

    public JsonObject? GetById(string segment, string id) =>
        GetCollection(segment).OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == id);

    public void Add(string segment, JsonObject item) => GetCollection(segment).Add(item);

    public void Remove(string segment, string id)
    {
        var collection = GetCollection(segment);
        var match = collection.OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == id);
        if (match != null) collection.Remove(match);
        _assignments.Remove(AssignmentKey(segment, id));
    }

    public JsonArray GetAssignments(string segment, string id) =>
        _assignments.TryGetValue(AssignmentKey(segment, id), out var arr) ? arr : _assignments[AssignmentKey(segment, id)] = new JsonArray();

    public void SetAssignments(string segment, string id, JsonArray assignments) =>
        _assignments[AssignmentKey(segment, id)] = assignments;

    private static string AssignmentKey(string segment, string id) => $"{segment}/{id}";

    // ---- Seeding ----

    public static DemoDataStore CreateSourceSeed()
    {
        var store = new DemoDataStore("Contoso Demo (Source)");
        store.SeedOrganization();

        var sales = store.AddGroup("Sales - Windows Devices", "Security group for the sales team's Windows devices");
        var it = store.AddGroup("IT - Pilot Ring", "Pilot ring for IT-managed policies");
        var finance = store.AddGroup("Finance - Compliance Required", "Requires compliance policies before access");

        var salesId = (string)sales["id"]!;
        var itId = (string)it["id"]!;
        var financeId = (string)finance["id"]!;

        var winBaseline = store.AddPolicy("deviceConfigurations", "#microsoft.graph.windows10CustomConfiguration",
            "displayName", "Windows 10 Baseline Settings", "Baseline OMA-URI configuration for all Windows devices.");
        store.AddGroupAssignment("deviceConfigurations", (string)winBaseline["id"]!, salesId);

        var macBaseline = store.AddPolicy("deviceConfigurations", "#microsoft.graph.macOSCustomConfiguration",
            "displayName", "macOS Security Baseline", "Custom configuration profile for macOS devices.");
        store.AddAllDevicesAssignment("deviceConfigurations", (string)macBaseline["id"]!);

        var winCompliance = store.AddPolicy("deviceCompliancePolicies", "#microsoft.graph.windows10CompliancePolicy",
            "displayName", "Windows 10 Compliance Baseline", "Requires BitLocker and a minimum OS version.");
        store.AddGroupAssignment("deviceCompliancePolicies", (string)winCompliance["id"]!, financeId);

        var androidCompliance = store.AddPolicy("deviceCompliancePolicies", "#microsoft.graph.androidWorkProfileCompliancePolicy",
            "displayName", "Android Work Profile Compliance", "Requires a work profile passcode.");
        store.AddAllUsersAssignment("deviceCompliancePolicies", (string)androidCompliance["id"]!);

        var settingsCatalog1 = store.AddPolicy("configurationPolicies", "#microsoft.graph.deviceManagementConfigurationPolicy",
            "name", "Defender Antivirus - Settings Catalog", "Configures Microsoft Defender Antivirus settings.",
            o => o["settings"] = DemoSetting("defender_realtimeprotection", "Enabled"));
        store.AddGroupAssignment("configurationPolicies", (string)settingsCatalog1["id"]!, itId);

        var settingsCatalog2 = store.AddPolicy("configurationPolicies", "#microsoft.graph.deviceManagementConfigurationPolicy",
            "name", "Edge Browser Settings", "Configures Microsoft Edge browser policies.",
            o => o["settings"] = DemoSetting("edge_homepage", "https://intranet.contoso.demo"));
        store.AddAllDevicesAssignment("configurationPolicies", (string)settingsCatalog2["id"]!);

        var script1 = store.AddPolicy("deviceManagementScripts", "#microsoft.graph.deviceManagementScript",
            "displayName", "Rename Local Admin Account.ps1", "Renames the built-in local administrator account.",
            o =>
            {
                o["scriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo script content"));
                o["runAsAccount"] = "system";
                o["fileName"] = "Rename-LocalAdmin.ps1";
            });
        store.AddGroupAssignment("deviceManagementScripts", (string)script1["id"]!, itId);

        var script2 = store.AddPolicy("deviceManagementScripts", "#microsoft.graph.deviceManagementScript",
            "displayName", "Set Power Plan to Balanced.ps1", "Sets the active Windows power plan.",
            o =>
            {
                o["scriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo script content"));
                o["runAsAccount"] = "system";
                o["fileName"] = "Set-PowerPlan.ps1";
            });

        var remediation1 = store.AddPolicy("deviceHealthScripts", "#microsoft.graph.deviceHealthScript",
            "displayName", "Detect and Fix Disk Space Issues", "Clears temp files when disk space is low.",
            o =>
            {
                o["detectionScriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo detection"));
                o["remediationScriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo remediation"));
                o["runAsAccount"] = "system";
                o["publisher"] = "Contoso IT";
            });
        store.AddGroupAssignment("deviceHealthScripts", (string)remediation1["id"]!, itId);

        var remediation2 = store.AddPolicy("deviceHealthScripts", "#microsoft.graph.deviceHealthScript",
            "displayName", "Clear Teams Cache", "Clears the Microsoft Teams local cache.",
            o =>
            {
                o["detectionScriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo detection"));
                o["remediationScriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("# demo remediation"));
                o["runAsAccount"] = "user";
                o["publisher"] = "Contoso IT";
            });

        var macScript = store.AddPolicy("deviceShellScripts", "#microsoft.graph.deviceShellScript",
            "displayName", "Install Company Portal (macOS)", "Installs Company Portal on managed Macs.",
            o =>
            {
                o["scriptContent"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("#!/bin/zsh\necho demo"));
                o["runAsAccount"] = "system";
                o["fileName"] = "install-company-portal.sh";
                o["executionFrequency"] = "P1D";
                o["retryCount"] = 3;
            });
        store.AddGroupAssignment("deviceShellScripts", (string)macScript["id"]!, salesId);

        var appleEnrollment = store.AddPolicy("appleUserInitiatedEnrollmentProfiles", "#microsoft.graph.appleUserInitiatedEnrollmentProfile",
            "displayName", "Corporate BYOD Enrollment", "User-initiated enrollment profile for personal Apple devices.");
        store.AddAllUsersAssignment("appleUserInitiatedEnrollmentProfiles", (string)appleEnrollment["id"]!);

        var autopilot = store.AddPolicy("windowsAutopilotDeploymentProfiles", "#microsoft.graph.azureADWindowsAutopilotDeploymentProfile",
            "displayName", "Standard Autopilot - User-Driven", "Standard user-driven Autopilot deployment profile.",
            o =>
            {
                o["language"] = "os-default";
                o["deviceNameTemplate"] = "CON-%SERIAL%";
                o["outOfBoxExperienceSettings"] = new JsonObject
                {
                    ["hidePrivacySettings"] = true,
                    ["hideEULA"] = true,
                    ["userType"] = "standard",
                    ["deviceUsageType"] = "singleUser",
                    ["skipKeyboardSelectionPage"] = false,
                    ["hideEscapeLink"] = true
                };
            });
        store.AddGroupAssignment("windowsAutopilotDeploymentProfiles", (string)autopilot["id"]!, itId);

        var driverUpdate = store.AddPolicy("windowsDriverUpdateProfiles", "#microsoft.graph.windowsDriverUpdateProfile",
            "displayName", "Driver Updates - Approve Automatically", "Automatically approves recommended driver updates.");
        store.AddAllDevicesAssignment("windowsDriverUpdateProfiles", (string)driverUpdate["id"]!);

        var featureUpdate = store.AddPolicy("windowsFeatureUpdateProfiles", "#microsoft.graph.windowsFeatureUpdateProfile",
            "displayName", "Windows 11 23H2 Rollout", "Rolls out Windows 11, version 23H2.");
        store.AddGroupAssignment("windowsFeatureUpdateProfiles", (string)featureUpdate["id"]!, itId);

        var qualityPolicy = store.AddPolicy("windowsQualityUpdatePolicies", "#microsoft.graph.windowsQualityUpdatePolicy",
            "displayName", "Monthly Quality Updates - Pilot Ring", "Approves quality updates for the pilot ring.");
        store.AddGroupAssignment("windowsQualityUpdatePolicies", (string)qualityPolicy["id"]!, itId);

        var qualityProfile = store.AddPolicy("windowsQualityUpdateProfiles", "#microsoft.graph.windowsQualityUpdateProfile",
            "displayName", "Expedited Security Updates", "Expedites out-of-band security quality updates.");
        store.AddAllDevicesAssignment("windowsQualityUpdateProfiles", (string)qualityProfile["id"]!);

        store.AddFilter("Windows 11 Devices", "windows10AndLater", "(device.osVersion -startsWith \"10.0.22\")");
        store.AddFilter("Corporate-Owned iOS", "iOS", "(device.enrollmentProfileName -eq \"Corporate\")");

        var companyPortal = store.AddPolicy("mobileApps", "#microsoft.graph.win32LobApp",
            "displayName", "Company Portal", "Line-of-business Win32 app package.");
        store.AddAllDevicesAssignment("mobileApps", (string)companyPortal["id"]!);

        store.AddPolicy("mobileApps", "#microsoft.graph.iosStoreApp", "displayName", "Microsoft Teams", "iOS store app.");
        store.AddPolicy("mobileApps", "#microsoft.graph.androidManagedStoreApp", "displayName", "Slack", "Android managed store app.");

        store.SeedAuditEvents();

        return store;
    }

    public static DemoDataStore CreateDestinationSeed()
    {
        var store = new DemoDataStore("Fabrikam Demo (Destination)");
        store.SeedOrganization();
        store.AddGroup("Pilot Users", "Small pilot group used to validate imported content");
        return store;
    }

    private void SeedOrganization()
    {
        Add("organization", new JsonObject
        {
            ["id"] = TenantId,
            ["@odata.type"] = "#microsoft.graph.organization",
            ["displayName"] = TenantDisplayName
        });
    }

    private JsonObject AddGroup(string displayName, string description)
    {
        var group = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["@odata.type"] = "#microsoft.graph.group",
            ["displayName"] = displayName,
            ["description"] = description,
            ["mailEnabled"] = false,
            ["securityEnabled"] = true,
            ["groupTypes"] = new JsonArray(),
            ["createdDateTime"] = DateTimeOffset.UtcNow.AddDays(-90).ToString("o")
        };
        Add("groups", group);
        return group;
    }

    private JsonObject AddPolicy(string segment, string odataType, string displayNameProperty, string displayName, string description, Action<JsonObject>? customize = null)
    {
        var item = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["@odata.type"] = odataType,
            [displayNameProperty] = displayName,
            ["description"] = description,
            ["createdDateTime"] = DateTimeOffset.UtcNow.AddDays(-45).ToString("o"),
            ["lastModifiedDateTime"] = DateTimeOffset.UtcNow.AddDays(-3).ToString("o"),
            ["version"] = 1
        };
        customize?.Invoke(item);
        Add(segment, item);
        return item;
    }

    private void AddFilter(string displayName, string platform, string rule)
    {
        Add("assignmentFilters", new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["@odata.type"] = "#microsoft.graph.deviceAndAppManagementAssignmentFilter",
            ["displayName"] = displayName,
            ["description"] = string.Empty,
            ["platform"] = platform,
            ["rule"] = rule,
            ["createdDateTime"] = DateTimeOffset.UtcNow.AddDays(-60).ToString("o")
        });
    }

    private static JsonArray DemoSetting(string settingDefinitionId, string value) => new()
    {
        new JsonObject
        {
            ["id"] = "0",
            ["settingInstance"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationSimpleSettingInstance",
                ["settingDefinitionId"] = settingDefinitionId,
                ["simpleSettingValue"] = new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.deviceManagementConfigurationStringSettingValue",
                    ["value"] = value
                }
            }
        }
    };

    private void AddGroupAssignment(string segment, string itemId, string groupId) =>
        GetAssignments(segment, itemId).Add(new JsonObject
        {
            ["id"] = $"{itemId}_{groupId}",
            ["target"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.groupAssignmentTarget",
                ["groupId"] = groupId,
                ["deviceAndAppManagementAssignmentFilterId"] = null,
                ["deviceAndAppManagementAssignmentFilterType"] = "none"
            }
        });

    private void AddAllDevicesAssignment(string segment, string itemId) =>
        GetAssignments(segment, itemId).Add(new JsonObject
        {
            ["id"] = $"{itemId}_alldevices",
            ["target"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.allDevicesAssignmentTarget",
                ["deviceAndAppManagementAssignmentFilterId"] = null,
                ["deviceAndAppManagementAssignmentFilterType"] = "none"
            }
        });

    private void AddAllUsersAssignment(string segment, string itemId) =>
        GetAssignments(segment, itemId).Add(new JsonObject
        {
            ["id"] = $"{itemId}_allusers",
            ["target"] = new JsonObject
            {
                ["@odata.type"] = "#microsoft.graph.allLicensedUsersAssignmentTarget",
                ["deviceAndAppManagementAssignmentFilterId"] = null,
                ["deviceAndAppManagementAssignmentFilterType"] = "none"
            }
        });

    private void SeedAuditEvents()
    {
        var events = new (string Activity, string Type, string OperationType, int DaysAgo)[]
        {
            ("Patch DeviceConfiguration", "DeviceConfiguration", "Patch", 1),
            ("Create Group", "Group", "Create", 2),
            ("Assign DeviceCompliancePolicy", "DeviceCompliancePolicy", "Assign", 3),
            ("Delete DeviceManagementScript", "DeviceManagementScript", "Delete", 5),
            ("Create ConfigurationPolicy", "ConfigurationPolicy", "Create", 7),
            ("Patch MobileApp", "MobileApp", "Patch", 9),
            ("Assign WindowsAutopilotDeploymentProfile", "WindowsAutopilotDeploymentProfile", "Assign", 12),
            ("Create AssignmentFilter", "AssignmentFilter", "Create", 15)
        };

        foreach (var e in events)
        {
            Add("auditEvents", new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["@odata.type"] = "#microsoft.graph.auditEvent",
                ["displayName"] = e.Activity,
                ["componentName"] = "Microsoft Intune",
                ["activity"] = e.Activity,
                ["activityDateTime"] = DateTimeOffset.UtcNow.AddDays(-e.DaysAgo).ToString("o"),
                ["activityType"] = e.Type,
                ["activityOperationType"] = e.OperationType,
                ["activityResult"] = "Success",
                ["category"] = "Object",
                ["actor"] = new JsonObject
                {
                    ["@odata.type"] = "#microsoft.graph.auditActor",
                    ["type"] = "ItPro",
                    ["userPermissions"] = new JsonArray(),
                    ["applicationDisplayName"] = "IntuneTools",
                    ["userPrincipalName"] = "admin@contosodemo.onmicrosoft.com",
                    ["userDisplayName"] = "Demo Administrator",
                    ["ipAddress"] = "203.0.113.10"
                },
                ["resources"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["@odata.type"] = "#microsoft.graph.auditResource",
                        ["displayName"] = e.Activity,
                        ["modifiedProperties"] = new JsonArray(),
                        ["auditResourceType"] = e.Type
                    }
                }
            });
        }
    }
}
