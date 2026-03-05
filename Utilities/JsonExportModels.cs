using System;
using System.Collections.Generic;

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
    }
}
