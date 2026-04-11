using IntuneTools.Utilities;
using System;
using System.Collections.Generic;

namespace IntuneTools.Graph
{
    /// <summary>
    /// Centralized helper for building and merging Graph API assignment targets.
    /// Eliminates duplicated AllUsers/AllDevices/Group target creation logic across all helper classes.
    /// </summary>
    public static class GraphAssignmentHelper
    {
        /// <summary>
        /// Applies the currently selected assignment filter to a target, if one is selected.
        /// Consolidates the ApplySelectedFilter pattern from SettingsCatalogHelper and inline usages.
        /// </summary>
        public static void ApplySelectedFilter(DeviceAndAppManagementAssignmentTarget target)
        {
            if (target == null) return;

            if (IsFilterSelected
                && !string.IsNullOrWhiteSpace(SelectedFilterID)
                && Guid.TryParse(SelectedFilterID, out _)
                && deviceAndAppManagementAssignmentFilterType != DeviceAndAppManagementAssignmentFilterType.None)
            {
                target.DeviceAndAppManagementAssignmentFilterId = SelectedFilterID;
                target.DeviceAndAppManagementAssignmentFilterType = deviceAndAppManagementAssignmentFilterType;
                return;
            }

            target.DeviceAndAppManagementAssignmentFilterId = null;
            target.DeviceAndAppManagementAssignmentFilterType = DeviceAndAppManagementAssignmentFilterType.None;
        }

        /// <summary>
        /// Result of building assignments from a list of group IDs.
        /// </summary>
        public class AssignmentBuildResult
        {
            public bool HasAllUsers { get; set; }
            public bool HasAllDevices { get; set; }
            public HashSet<string> SeenGroupIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds assignment target objects from a list of group IDs, handling virtual groups
        /// (All Users / All Devices) and applying filters. Uses a factory delegate to create
        /// the typed assignment wrapper specific to each resource type.
        /// </summary>
        /// <typeparam name="TAssignment">The typed assignment (e.g., DeviceManagementConfigurationPolicyAssignment).</typeparam>
        /// <param name="groupIds">List of group IDs (may include virtual group IDs).</param>
        /// <param name="createAssignment">Factory that takes a target and returns a typed assignment object.</param>
        /// <param name="assignments">Output list of built assignments.</param>
        /// <returns>Build result with flags for AllUsers/AllDevices and seen group IDs.</returns>
        public static AssignmentBuildResult BuildAssignments<TAssignment>(
            List<string> groupIds,
            Func<DeviceAndAppManagementAssignmentTarget, string?, TAssignment> createAssignment,
            List<TAssignment> assignments)
        {
            var result = new AssignmentBuildResult();

            foreach (var group in groupIds)
            {
                if (string.IsNullOrWhiteSpace(group) || !result.SeenGroupIds.Add(group))
                    continue;

                DeviceAndAppManagementAssignmentTarget target;
                string? groupIdForAssignment = null;

                if (group.Equals(allUsersVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                {
                    result.HasAllUsers = true;
                    target = new AllLicensedUsersAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.allLicensedUsersAssignmentTarget"
                    };
                }
                else if (group.Equals(allDevicesVirtualGroupID, StringComparison.OrdinalIgnoreCase))
                {
                    result.HasAllDevices = true;
                    target = new AllDevicesAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.allDevicesAssignmentTarget"
                    };
                }
                else
                {
                    groupIdForAssignment = group;
                    target = new GroupAssignmentTarget
                    {
                        OdataType = "#microsoft.graph.groupAssignmentTarget",
                        GroupId = group
                    };
                }

                ApplySelectedFilter(target);
                assignments.Add(createAssignment(target, groupIdForAssignment));
            }

            return result;
        }

        /// <summary>
        /// Merges existing assignments into the new assignment list, skipping duplicates.
        /// Works with any assignment type that has a Target property of type DeviceAndAppManagementAssignmentTarget.
        /// </summary>
        /// <typeparam name="TAssignment">The typed assignment (e.g., DeviceManagementConfigurationPolicyAssignment).</typeparam>
        /// <param name="existingAssignments">The existing assignments from the Graph API.</param>
        /// <param name="newAssignments">The new assignments list to merge into.</param>
        /// <param name="buildResult">The build result from BuildAssignments.</param>
        /// <param name="getTarget">Extracts the target from an assignment.</param>
        public static void MergeExistingAssignments<TAssignment>(
            IEnumerable<TAssignment>? existingAssignments,
            List<TAssignment> newAssignments,
            AssignmentBuildResult buildResult,
            Func<TAssignment, DeviceAndAppManagementAssignmentTarget?> getTarget)
        {
            if (existingAssignments == null) return;

            foreach (var existing in existingAssignments)
            {
                var target = getTarget(existing);

                if (target is AllLicensedUsersAssignmentTarget)
                {
                    if (!buildResult.HasAllUsers)
                        newAssignments.Add(existing);
                }
                else if (target is AllDevicesAssignmentTarget)
                {
                    if (!buildResult.HasAllDevices)
                        newAssignments.Add(existing);
                }
                else if (target is GroupAssignmentTarget groupTarget)
                {
                    var existingGroupId = groupTarget.GroupId;
                    if (!string.IsNullOrWhiteSpace(existingGroupId) && buildResult.SeenGroupIds.Add(existingGroupId))
                        newAssignments.Add(existing);
                }
                else
                {
                    // Include any other assignment types (e.g., exclusions)
                    newAssignments.Add(existing);
                }
            }
        }
    }
}
