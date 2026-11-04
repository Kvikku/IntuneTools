namespace InToolz.Utilities
{
    public class DuplicateContentInfo
    {
        public string ContentName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string ContentPlatform { get; set; } = string.Empty;
        public DateTimeOffset? CreatedDateTime { get; set; }
        public DateTimeOffset? LastModifiedDateTime { get; set; }
        public bool? HasAssignments { get; set; }
        public bool? HasMembers { get; set; }
        public bool IsOddGroup { get; set; }

        public string CreatedDisplay =>
            CreatedDateTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—";

        public string ModifiedDisplay =>
            LastModifiedDateTime?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—";

        public string AssignedDisplay => HasAssignments switch
        {
            true => "Yes",
            false => "No",
            null => "—"
        };
    }
}
