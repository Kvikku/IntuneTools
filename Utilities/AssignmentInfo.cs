namespace IntuneTools.Utilities
{
    /// <summary>
    /// Represents a single assignment target for a content item.
    /// Used by the Manage Assignments page to display and manage assignment details.
    /// </summary>
    public class AssignmentInfo
    {
        /// <summary>
        /// The assignment ID from Microsoft Graph.
        /// </summary>
        public string? AssignmentId { get; set; }

        /// <summary>
        /// Human-readable description of the assignment target
        /// (e.g., "Group", "All Users", "All Devices", "Exclusion Group").
        /// </summary>
        public string? TargetType { get; set; }

        /// <summary>
        /// The group ID for group-based assignments. Null for virtual group targets.
        /// </summary>
        public string? GroupId { get; set; }

        /// <summary>
        /// The assignment filter ID, if any.
        /// </summary>
        public string? FilterId { get; set; }

        /// <summary>
        /// The assignment filter type (e.g., Include, Exclude), if any.
        /// </summary>
        public string? FilterType { get; set; }

        /// <summary>
        /// Creates a human-readable summary of this assignment.
        /// </summary>
        public override string ToString()
        {
            var target = TargetType ?? "Unknown";
            if (!string.IsNullOrEmpty(GroupId))
                target += $" ({GroupId})";
            if (!string.IsNullOrEmpty(FilterId))
                target += $" [Filter: {FilterId}, Type: {FilterType}]";
            return target;
        }

        /// <summary>
        /// Helper to extract assignment info from a DeviceAndAppManagementAssignmentTarget.
        /// This is the common target type used across most Intune assignment types.
        /// </summary>
        public static AssignmentInfo FromTarget(string? assignmentId, DeviceAndAppManagementAssignmentTarget? target)
        {
            if (target == null)
            {
                return new AssignmentInfo
                {
                    AssignmentId = assignmentId,
                    TargetType = "Unknown"
                };
            }

            var info = new AssignmentInfo
            {
                AssignmentId = assignmentId,
                FilterId = target.DeviceAndAppManagementAssignmentFilterId,
                FilterType = target.DeviceAndAppManagementAssignmentFilterType?.ToString()
            };

            switch (target)
            {
                case AllLicensedUsersAssignmentTarget:
                    info.TargetType = "All Users";
                    break;
                case AllDevicesAssignmentTarget:
                    info.TargetType = "All Devices";
                    break;
                case ExclusionGroupAssignmentTarget exclusionTarget:
                    info.TargetType = "Exclusion Group";
                    info.GroupId = exclusionTarget.GroupId;
                    break;
                case GroupAssignmentTarget groupTarget:
                    info.TargetType = "Group";
                    info.GroupId = groupTarget.GroupId;
                    break;
                default:
                    info.TargetType = target.OdataType ?? "Unknown";
                    break;
            }

            return info;
        }
    }
}
