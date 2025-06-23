using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

namespace IntuneTools.Graph.IntuneHelperClasses
{
    public class DeviceCompliancePolicyHelper
    {
        public static async Task<List<DeviceCompliancePolicy>> GetAllDeviceCompliancePolicies(GraphServiceClient graphServiceClient)
        {
            // This method retrieves all the device compliance policies from Intune and returns them as a list of DeviceManagementCompliancePolicy objects
            try
            {
                LogToImportStatusFile("Retrieving all device compliance policies.");

                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 1000;
                });

                if (result == null)
                {
                    throw new InvalidOperationException("The result from the Graph API is null.");
                }

                List<DeviceCompliancePolicy> compliancePolicies = new List<DeviceCompliancePolicy>();
                // Iterate through the pages of results
                var pageIterator = PageIterator<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    compliancePolicies.Add(policy);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();

                LogToImportStatusFile($"Found {compliancePolicies.Count} device compliance policies.");

                // return the list of policies
                return compliancePolicies;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                LogToImportStatusFile("ODataError occurred", LogLevels.Warning);
                LogToImportStatusFile(me.Message, LogLevels.Warning);
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An unexpected error occurred", LogLevels.Warning);
                LogToImportStatusFile(ex.Message, LogLevels.Warning);
            }

            // Return an empty list if an exception occurs
            return new List<DeviceCompliancePolicy>();
        }

        public static async Task<List<DeviceCompliancePolicy>> SearchForDeviceCompliancePolicies(GraphServiceClient graphServiceClient, string searchQuery)
        {
            // This method searches the Intune device compliance policies for a specific query and returns the results as a list of DeviceManagementCompliancePolicy objects
            try
            {
                LogToImportStatusFile("Searching for device compliance policies. Search query: " + searchQuery);

                var result = await graphServiceClient.DeviceManagement.DeviceCompliancePolicies.GetAsync();


                List<DeviceCompliancePolicy> compliancePolicies = new List<DeviceCompliancePolicy>();
                // Iterate through the pages of results
                var pageIterator = PageIterator<DeviceCompliancePolicy, DeviceCompliancePolicyCollectionResponse>.CreatePageIterator(graphServiceClient, result, (policy) =>
                {
                    compliancePolicies.Add(policy);
                    return true;
                });
                // start the iteration
                await pageIterator.IterateAsync();


                LogToImportStatusFile($"Found {compliancePolicies.Count} device compliance policies.");

                // Filter the collected policies based on the searchQuery - Graph API does not allow for server side filtering 
                var filteredPolicies = compliancePolicies
                    .Where(policy => policy.DisplayName != null && policy.DisplayName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                LogToImportStatusFile($"Filtered policies count: {filteredPolicies.Count}");


                // return the list of policies
                return filteredPolicies;
            }
            catch (Microsoft.Graph.Beta.Models.ODataErrors.ODataError me)
            {
                LogToImportStatusFile("ODataError occurred", LogLevels.Warning);
                LogToImportStatusFile(me.Message, LogLevels.Warning);
            }
            catch (Exception ex)
            {
                LogToImportStatusFile("An unexpected error occurred", LogLevels.Warning);
                LogToImportStatusFile(ex.Message, LogLevels.Warning);
            }

            // Return an empty list if an exception occurs
            return new List<DeviceCompliancePolicy>();
        }

        public static async Task ImportMultipleDeviceCompliancePolicies(GraphServiceClient sourceGraphServiceClient, GraphServiceClient destinationGraphServiceClient, System.Windows.Forms.DataGridView dtg, List<string> policies, System.Windows.Forms.RichTextBox rtb, bool assignments, bool filter, List<string> groups)
        {
            try
            {
                rtb.AppendText(Environment.NewLine);
                rtb.AppendText($"{DateTime.Now.ToString()} - Importing {policies.Count} Device Compliance policies.\n");
                WriteToImportStatusFile(" ");
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Importing {policies.Count} Device Compliance policies.");

                foreach (var policy in policies)
                {
                    var policyName = string.Empty;
                    try
                    {
                        var result = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policy].GetAsync((requestConfiguration) =>
                        {
                            requestConfiguration.QueryParameters.Expand = new string[] { "scheduledActionsForRule" };
                        });

                        //var rules = await sourceGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policy].ScheduledActionsForRule.GetAsync();

                        policyName = result.DisplayName;

                        // Get the type of the policy with reflection
                        var type = result.GetType();

                        // Create a new instance of the same type
                        var newPolicy = Activator.CreateInstance(type);

                        // Copy all settings from the source policy to the new policy
                        foreach (var property in result.GetType().GetProperties())
                        {
                            if (property.CanWrite && property.Name != "Id" && property.Name != "CreatedDateTime" && property.Name != "LastModifiedDateTime")
                            {
                                var value = property.GetValue(result);
                                if (value != null)
                                {
                                    property.SetValue(newPolicy, value);
                                }
                            }
                        }




                        // Cast the new policy to DeviceCompliancePolicy
                        var deviceCompliancePolicy = newPolicy as DeviceCompliancePolicy;


                        // new device compliance scheduled action rule

                        // Note - this manual test works. Need to copy the scheduled actions for rule

                        var testRule = new DeviceComplianceScheduledActionForRule
                        {
                            RuleName = "Test Rule",
                            ScheduledActionConfigurations = new List<DeviceComplianceActionItem>()
                            {
                                new DeviceComplianceActionItem
                                {
                                    ActionType = DeviceComplianceActionType.Block,
                                    GracePeriodHours = 8,
                                    NotificationMessageCCList = new List<string>(),
                                    NotificationTemplateId = ""
                                }
                            }
                        };

                        deviceCompliancePolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>
                        {
                            testRule
                        };


                        //// Ensure the ScheduledActionsForRule is copied
                        //if (result.ScheduledActionsForRule != null)
                        //{
                        //    deviceCompliancePolicy.ScheduledActionsForRule = new List<DeviceComplianceScheduledActionForRule>();
                        //    foreach (var action in result.ScheduledActionsForRule)
                        //    {
                        //        var newAction = new DeviceComplianceScheduledActionForRule
                        //        {
                        //            RuleName = action.RuleName,
                        //            ScheduledActionConfigurations = action.ScheduledActionConfigurations

                        //        };
                        //        deviceCompliancePolicy.ScheduledActionsForRule.Add(newAction);
                        //    }
                        //}


                        var import = await destinationGraphServiceClient.DeviceManagement.DeviceCompliancePolicies.PostAsync(deviceCompliancePolicy);
                        rtb.AppendText($"Successfully imported {import.DisplayName}\n");
                        WriteToLog($"Successfully imported {import.DisplayName}");

                        if (assignments)
                        {
                            await AssignGroupsToSingleDeviceCompliance(import.Id, groups, destinationGraphServiceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteErrorToRTB($"Failed to import {policyName}\n", rtb);
                        WriteToImportStatusFile($"Failed to import {policyName}: {ex.Message}", LogType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToImportStatusFile($"An unexpected error occurred during the import process: {ex.Message}", LogType.Error);
                WriteErrorToRTB($"An unexpected error occurred during the import process. Please check the log file for more information.", rtb);
            }
            finally
            {
                rtb.AppendText($"{DateTime.Now.ToString()} - Finished importing Device Compliance policies.\n");
                WriteToImportStatusFile($"{DateTime.Now.ToString()} - Finished importing Device Compliance policies.");
            }
        }

        public static async Task AssignGroupsToSingleDeviceCompliance(string policyID, List<string> groupIDs, GraphServiceClient destinationGraphServiceClient)
        {
            if (policyID == null)
            {
                throw new ArgumentNullException(nameof(policyID));
            }

            if (groupIDs == null)
            {
                throw new ArgumentNullException(nameof(groupIDs));
            }

            if (destinationGraphServiceClient == null)
            {
                throw new ArgumentNullException(nameof(destinationGraphServiceClient));
            }

            List<DeviceCompliancePolicyAssignment> assignments = new List<DeviceCompliancePolicyAssignment>();

            foreach (var group in groupIDs)
            {
                var assignment = new DeviceCompliancePolicyAssignment
                {
                    Target = new GroupAssignmentTarget
                    {
                        GroupId = group,
                        DeviceAndAppManagementAssignmentFilterId = SelectedFilterID,
                        DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType
                    }
                };

                assignments.Add(assignment);
            }

            var requestBody = new Microsoft.Graph.Beta.DeviceManagement.DeviceCompliancePolicies.Item.Assign.AssignPostRequestBody
            {
                Assignments = assignments
            };

            try
            {
                await destinationGraphServiceClient.DeviceManagement.DeviceCompliancePolicies[policyID].Assign.PostAsync(requestBody);
                WriteToLog($"Assigned groups to policy {policyID}");
            }
            catch (Exception ex)
            {
                HandleException(ex, "An unexpected error occurred");
            }
        }

    }

}
