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
    public class PowerShellScriptsHelper
    {
        public static async Task<List<DeviceManagementScript>> SearchForPowerShellScripts(GraphServiceClient graphServiceClient, string searchQuery)
        {
            try
            {
                WriteToImportStatusFile("Searching for PowerShell scripts. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceManagementScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Filter = $"contains(displayName,'{searchQuery}')";
                });

                List<DeviceManagementScript> scripts = new List<DeviceManagementScript>();
                var pageIterator = PageIterator<DeviceManagementScript, DeviceManagementScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    scripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {scripts.Count} PowerShell scripts.");

                return scripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while searching for PowerShell scripts",LogType.Error);
                return new List<DeviceManagementScript>();
            }
        }

        public static async Task<List<DeviceManagementScript>> GetAllPowerShellScripts(GraphServiceClient graphServiceClient)
        {
            try
            {
                WriteToImportStatusFile("Retrieving all PowerShell scripts.");

                var result = await graphServiceClient.DeviceManagement.DeviceManagementScripts.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                List<DeviceManagementScript> scripts = new List<DeviceManagementScript>();
                var pageIterator = PageIterator<DeviceManagementScript, DeviceManagementScriptCollectionResponse>.CreatePageIterator(graphServiceClient, result, (script) =>
                {
                    scripts.Add(script);
                    return true;
                });
                await pageIterator.IterateAsync();

                WriteToImportStatusFile($"Found {scripts.Count} PowerShell scripts.");

                return scripts;
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while retrieving all PowerShell scripts",LogType.Error);
                return new List<DeviceManagementScript>();
            }
        }

        public static async Task ImportMultiplePowerShellScripts(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, List<string> scripts, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                WriteToImportStatusFile($"Importing {scripts.Count} PowerShell scripts.");

                foreach (var script in scripts)
                {
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceManagementScripts[script].GetAsync();

                        var requestBody = new DeviceManagementScript
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

                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceManagementScripts.PostAsync(requestBody);
                        WriteToImportStatusFile($"Imported script: {requestBody.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSinglePowerShellScript(import.Id, groups, destinationGraphServiceClient);
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

        public static async Task AssignGroupsToSinglePowerShellScript(string scriptID, List<string> groupID, GraphServiceClient destinationGraphServiceClient)
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

                List<DeviceManagementScriptAssignment> assignments = new List<DeviceManagementScriptAssignment>();

                foreach (var group in groupID)
                {
                    var assignment = new DeviceManagementScriptAssignment
                    {
                        OdataType = "#microsoft.graph.deviceManagementScriptAssignment",
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

                var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceManagementScripts.Item.Assign.AssignPostRequestBody
                {
                    DeviceManagementScriptAssignments = assignments,
                };

                try
                {
                    await destinationGraphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].Assign.PostAsync(requestBody);
                    WriteToImportStatusFile("Assigned groups to script " + scriptID + " with filter type" + deviceAndAppManagementAssignmentFilterType.ToString());
                }
                catch (Exception ex)
                {
                    WriteToImportStatusFile($"Error assigning groups to script {scriptID}",LogType.Error);
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while assigning groups to a single PowerShell script",LogType.Error);
            }
        }
        public static async Task DeletePowerShellScript(GraphServiceClient graphServiceClient, string scriptID)
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
                await graphServiceClient.DeviceManagement.DeviceManagementScripts[scriptID].DeleteAsync();
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile("An error occurred while deleting PowerShell scripts",LogType.Error);
            }
        }
    }
}
