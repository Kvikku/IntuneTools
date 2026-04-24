
namespace IntuneTools.Utilities
{
    /// <summary>
    /// Root document for JSON export/import. Contains metadata and a list of exported items.
    /// </summary>
    public class JsonExportDocument
    {
        public string? ExportedAt { get; set; }
        public string? TenantName { get; set; }
        public List<JsonExportItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Represents a single exported content item in the JSON file.
    /// </summary>
    public class JsonExportItem
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Platform { get; set; }
        public string? Id { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// Full policy payload serialized via Kiota. Used to recreate the policy in another tenant.
        /// Only populated during "Export to JSON" when full data is fetched from Graph.
        /// </summary>
        public JsonElement? PolicyData { get; set; }
    }
}
