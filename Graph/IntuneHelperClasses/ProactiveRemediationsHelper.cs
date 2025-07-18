using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class ProactiveRemediationsHelper
    {
        public static async Task<List<DeviceHealthScript>> SearchForProactiveRemediations(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for proactive remediation scripts. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceHealthScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

                List<DeviceHealthScript> healthScripts = new List<DeviceHealthScript>();
                var pageIterator = PageIterator<DeviceHealthScript, DeviceHealthScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    if (!script.Publisher.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        healthScripts.Add(script);
                    }
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {healthScripts.Count} proactive remediation scripts.");

                return healthScripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for proactive remediation scripts",LogType.Error);
                return new List<DeviceHealthScript>();
            }
        }

        public static async Task<List<DeviceHealthScript>> GetAllProactiveRemediations(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all proactive remediation scripts.");

                var result = await graphServiceClient.DeviceManagement.DeviceHealthScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceHealthScript> healthScripts = new List<DeviceHealthScript>();
                var pageIterator = PageIterator<DeviceHealthScript, DeviceHealthScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    if (!script.Publisher.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
                    {
                        healthScripts.Add(script);
                    }
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {healthScripts.Count} proactive remediation scripts.");

                return healthScripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all proactive remediation scripts",LogType.Error);
                return new List<DeviceHealthScript>();
            }
        }

        public static async Task ImportMultipleProactiveRemediations(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {scripts.Count} proactive remediation scripts.");

                foreach (var script in scripts)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceHealthScripts[script].GetAsync();

                        var requestBody = new DeviceHealthScript
                        {
                        };

                        foreach (var property in result.GetType().GetProperties())
                        {
                            var value = property.GetValue(result);
                            if (value != null && property.CanWrite)
                            {
                                property.SetValue(requestBody, value);
                            }
                        }

                        requestBody.Id = "";


                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceHealthScripts.PostAsync(requestBody);
                        WriteToImportStatusFile($"Imported script: {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleProactiveRemediation(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToImportStatusFile($"Error importing script {script}", LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred during the import process",LogType.Error);
            }
        }

        public static async Task AssignGroupsToSingleProactiveRemediation(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
        {
            try
            {
                if (scriptID == null)
                {
                    throw new ArgumentNullException(nameof(scriptID));
                }

                if (groupID == null)
                {
                    throw new ArgumentNullException(nameof(groupID));
                }

                if (destinationGraphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(destinationGraphServiceClient));
                }

                List<DeviceHealthScriptAssignment> assignments = new List<DeviceHealthScriptAssignment>();

                foreach (var group in groupID)
                {
                    var assignment = new DeviceHealthScriptAssignment
                    {
                        OdataType = "#microsoft.graph.deviceHealthScriptAssignment",
                        Id = group,
                        Target = new GroupAssignmentTarget
                        {
                            OdataType = "#microsoft.graph.groupAssignmentTarget",
                            DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                            DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType,
                            GroupId = group,
                        },
                    };
                    assignments.Add(assignment);
                }

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceHealthScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceHealthScriptAssignments = assignments,
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile("Assigned groups to script " + scriptID + " with filter type" + deviceAndAppManagementAssignmentFilterType.ToString());
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to script {scriptID}", LogType.Error);
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while assigning groups to a single proactive remediation script",LogType.Error);
            }
        }
        public static async Task DeleteProactiveRemediationScript(GraphServiceClient graphServiceClient, string policyID)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (policyID == null)
                {
                    throw new InvalidOperationException("Policy ID cannot be null.");
                }
                await graphServiceClient.DeviceManagement.DeviceHealthScripts[policyID].DeleteAsync();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while deleting proactive remediation scripts",LogType.Error);
            }
        }

        public static async Task RenameProactiveRemediation(GraphServiceClient graphServiceClient, string scriptID, string newName)
        {
            try
            {
                if (graphServiceClient == null)
                {
                    throw new ArgumentNullException(nameof(graphServiceClient));
                }

                if (scriptID == null)
                {
                    throw new InvalidOperationException("Script ID cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new InvalidOperationException("New name cannot be null or empty.");
                }

                // Look up the existing script
                var existingScript = await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].GetAsync();

                if (existingScript == null)
                {
                    throw new InvalidOperationException($"Script with ID '{scriptID}' not found.");
                }

                var name = FindPreFixInPolicyName(existingScript.DisplayName, newName);

                var script = new DeviceHealthScript
                {
                    DisplayName = name,
                };

                await graphServiceClient.DeviceManagement.DeviceHealthScripts[scriptID].PatchAsync(script);
                WriteToImportStatusFile($"Renamed Proactive remediation script {scriptID} to {name}");
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while renaming proactive remediation scripts", LogType.Warning);
                WriteToImportStatusFile(ex.Message, LogType.Error);
            }
        }
    }
}
