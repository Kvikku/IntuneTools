using System.Collections.Generic;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Root document for JSON export/import.
    /// Shared between ExportPage and ImportFromJsonPage.
    /// </summary>
    public sealed class JsonExportDocument
    {
        public string? ExportDate { get; set; }
        public string? TenantName { get; set; }
        public string? TenantId { get; set; }
        public int TotalItems { get; set; }
        public List<JsonExportItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Individual item in the JSON export/import.
    /// </summary>
    public sealed class JsonExportItem
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Platform { get; set; }
        public string? Id { get; set; }
        public string? Description { get; set; }
    }
}
