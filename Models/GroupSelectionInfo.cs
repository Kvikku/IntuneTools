namespace IntuneTools.Models
{
    /// <summary>
    /// Shared model representing a group selection in UI lists.
    /// Used by both ImportPage and AssignmentPage.
    /// </summary>
    public class GroupSelectionInfo
    {
        public string? GroupName { get; set; }
        public string? GroupId { get; set; }
    }

    /// <summary>
    /// Shared model representing a filter selection in UI lists.
    /// Used by both ImportPage and AssignmentPage.
    /// </summary>
    public class FilterSelectionInfo
    {
        public string? FilterName { get; set; }
    }
}
