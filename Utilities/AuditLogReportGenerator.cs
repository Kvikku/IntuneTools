using IntuneTools.Pages;
using System.Globalization;
using System.Text.Encodings.Web;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Generates a self-contained HTML dashboard report from audit log events.
    /// Uses Chart.js (CDN) for interactive charts and a sortable/filterable event table.
    /// </summary>
    public static class AuditLogReportGenerator
    {
        public static string Generate(IReadOnlyList<AuditEventViewModel> events, int days)
        {
            var sb = new StringBuilder();
            var generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ── Pre-compute data for charts ──
            var totalEvents = events.Count;
            var uniqueActors = events
                .Select(e => e.ActorDisplayName)
                .Where(a => !string.IsNullOrEmpty(a) && a != "Unknown")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var categories = events
                .Select(e => e.CategoryName)
                .Where(c => !string.IsNullOrEmpty(c) && c != "N/A")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var successCount = events.Count(e =>
                string.Equals(e.ResultText, "Success", StringComparison.OrdinalIgnoreCase));
            var failureCount = events.Count(e =>
                string.Equals(e.ResultText, "Failure", StringComparison.OrdinalIgnoreCase));
            var otherResultCount = totalEvents - successCount - failureCount;

            // Activity over time (group by date)
            var activityByDate = events
                .Where(e => e.ActivityDateTime.HasValue)
                .GroupBy(e => e.ActivityDateTime!.Value.LocalDateTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
                .ToList();

            // Events by category
            var byCategory = events
                .GroupBy(e => e.CategoryName ?? "N/A", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            // Top 10 actors
            var topActors = events
                .GroupBy(e => e.ActorDisplayName ?? "Unknown", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            // Events by operation type
            var byOpType = events
                .GroupBy(e => e.OperationType ?? "N/A", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            // Activity by hour of day
            var byHour = events
                .Where(e => e.ActivityDateTime.HasValue)
                .GroupBy(e => e.ActivityDateTime!.Value.LocalDateTime.Hour)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
            var hourLabels = Enumerable.Range(0, 24).Select(h => $"{h:D2}:00").ToList();
            var hourData = Enumerable.Range(0, 24).Select(h => byHour.GetValueOrDefault(h, 0)).ToList();

            // ── Build HTML ──
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"<title>Intune Audit Log Report — Last {days} Day(s)</title>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4\"></script>");
            sb.AppendLine("<style>");
            sb.AppendLine(CssBlock());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("<header>");
            sb.AppendLine($"<h1>Intune Audit Log Report</h1>");
            sb.AppendLine($"<p class=\"subtitle\">Last {days} day(s) &middot; Generated {HtmlEncode(generated)}</p>");
            sb.AppendLine("</header>");

            // KPI cards
            sb.AppendLine("<section class=\"kpi-row\">");
            AppendKpiCard(sb, "Total Events", totalEvents.ToString());
            AppendKpiCard(sb, "Unique Actors", uniqueActors.ToString());
            AppendKpiCard(sb, "Categories", categories.ToString());
            AppendKpiCard(sb, "Success / Failure", $"{successCount} / {failureCount}");
            sb.AppendLine("</section>");

            // Charts row 1
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card wide\"><h2>Activity Over Time</h2><canvas id=\"activityChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><h2>Events by Category</h2><canvas id=\"categoryChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Charts row 2
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card\"><h2>Top Actors</h2><canvas id=\"actorChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><h2>Success vs Failure</h2><canvas id=\"resultChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Charts row 3
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card wide\"><h2>Activity by Hour of Day</h2><canvas id=\"hourChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><h2>Events by Operation Type</h2><canvas id=\"opTypeChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Event table
            sb.AppendLine("<section class=\"table-section\">");
            sb.AppendLine("<h2>All Events</h2>");
            sb.AppendLine("<input id=\"tableSearch\" type=\"text\" placeholder=\"Search events…\" />");
            sb.AppendLine("<div class=\"table-wrap\">");
            sb.AppendLine("<table id=\"eventsTable\">");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th onclick=\"sortTable(0)\">Date/Time ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(1)\">Actor ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(2)\">Activity ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(3)\">Category ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(4)\">Result ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(5)\">Component ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(6)\">Operation ⇅</th>");
            sb.AppendLine("<th onclick=\"sortTable(7)\">Resources ⇅</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var evt in events)
            {
                sb.AppendLine("<tr>");
                sb.Append($"<td>{HtmlEncode(evt.ActivityDateTimeFormatted)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.ActorDisplayName)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.ActivityDisplayName)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.CategoryName)}</td>");
                sb.Append($"<td class=\"result-{(evt.ResultText ?? "").ToLowerInvariant()}\">{HtmlEncode(evt.ResultText)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.ComponentName)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.OperationType)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.ResourceInfo)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table></div></section>");

            // Chart scripts
            sb.AppendLine("<script>");
            sb.AppendLine(BuildChartScripts(
                activityByDate.Select(a => a.Date).ToList(),
                activityByDate.Select(a => a.Count).ToList(),
                byCategory.Select(c => c.Label).ToList(),
                byCategory.Select(c => c.Count).ToList(),
                topActors.Select(a => a.Label).ToList(),
                topActors.Select(a => a.Count).ToList(),
                successCount, failureCount, otherResultCount,
                byOpType.Select(o => o.Label).ToList(),
                byOpType.Select(o => o.Count).ToList(),
                hourLabels, hourData));
            sb.AppendLine(TableScripts());
            sb.AppendLine("</script>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static void AppendKpiCard(StringBuilder sb, string title, string value)
        {
            sb.AppendLine($"<div class=\"kpi-card\"><div class=\"kpi-title\">{HtmlEncode(title)}</div><div class=\"kpi-value\">{HtmlEncode(value)}</div></div>");
        }

        private static string JsArray(IEnumerable<string> items) =>
            "[" + string.Join(",", items.Select(i => $"\"{JsEscape(i)}\"")) + "]";

        private static string JsNumArray(IEnumerable<int> items) =>
            "[" + string.Join(",", items) + "]";

        private static string JsEscape(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        private static string HtmlEncode(string? s) =>
            HtmlEncoder.Default.Encode(s ?? "");

        private static string BuildChartScripts(
            List<string> dateLabels, List<int> dateCounts,
            List<string> catLabels, List<int> catCounts,
            List<string> actorLabels, List<int> actorCounts,
            int success, int failure, int other,
            List<string> opLabels, List<int> opCounts,
            List<string> hourLabels, List<int> hourCounts)
        {
            var palette = "['#0078d4','#e74856','#00b7c3','#8764b8','#ff8c00','#107c10','#d83b01','#5c2d91','#004e8c','#498205','#b4009e','#7a7574']";

            var sb = new StringBuilder();

            // Activity over time - bar
            sb.AppendLine($"new Chart(document.getElementById('activityChart'),{{type:'bar',data:{{labels:{JsArray(dateLabels)},datasets:[{{label:'Events',data:{JsNumArray(dateCounts)},backgroundColor:'#0078d4'}}]}},options:{{responsive:true,plugins:{{legend:{{display:false}}}},scales:{{y:{{beginAtZero:true,ticks:{{precision:0}}}}}}}}}});");

            // Category - doughnut
            sb.AppendLine($"new Chart(document.getElementById('categoryChart'),{{type:'doughnut',data:{{labels:{JsArray(catLabels)},datasets:[{{data:{JsNumArray(catCounts)},backgroundColor:{palette}}}]}},options:{{responsive:true,plugins:{{legend:{{position:'right'}}}}}}}});");

            // Top actors - horizontal bar
            sb.AppendLine($"new Chart(document.getElementById('actorChart'),{{type:'bar',data:{{labels:{JsArray(actorLabels)},datasets:[{{label:'Events',data:{JsNumArray(actorCounts)},backgroundColor:'#8764b8'}}]}},options:{{indexAxis:'y',responsive:true,plugins:{{legend:{{display:false}}}},scales:{{x:{{beginAtZero:true,ticks:{{precision:0}}}}}}}}}});");

            // Success vs failure - pie
            sb.AppendLine($"new Chart(document.getElementById('resultChart'),{{type:'pie',data:{{labels:['Success','Failure','Other'],datasets:[{{data:[{success},{failure},{other}],backgroundColor:['#107c10','#e74856','#7a7574']}}]}},options:{{responsive:true,plugins:{{legend:{{position:'bottom'}}}}}}}});");

            // Operation type - bar
            sb.AppendLine($"new Chart(document.getElementById('opTypeChart'),{{type:'doughnut',data:{{labels:{JsArray(opLabels)},datasets:[{{data:{JsNumArray(opCounts)},backgroundColor:{palette}}}]}},options:{{responsive:true,plugins:{{legend:{{position:'right'}}}}}}}});");

            // Hour of day - bar
            sb.AppendLine($"new Chart(document.getElementById('hourChart'),{{type:'bar',data:{{labels:{JsArray(hourLabels)},datasets:[{{label:'Events',data:{JsNumArray(hourCounts)},backgroundColor:'#00b7c3'}}]}},options:{{responsive:true,plugins:{{legend:{{display:false}}}},scales:{{y:{{beginAtZero:true,ticks:{{precision:0}}}}}}}}}});");

            return sb.ToString();
        }

        private static string TableScripts() => @"
let sortDir = {};
function sortTable(col) {
    const table = document.getElementById('eventsTable');
    const tbody = table.tBodies[0];
    const rows = Array.from(tbody.rows);
    sortDir[col] = !sortDir[col];
    rows.sort((a, b) => {
        const aText = a.cells[col].textContent.trim();
        const bText = b.cells[col].textContent.trim();
        return sortDir[col] ? aText.localeCompare(bText) : bText.localeCompare(aText);
    });
    rows.forEach(r => tbody.appendChild(r));
}
document.getElementById('tableSearch').addEventListener('input', function() {
    const filter = this.value.toLowerCase();
    const rows = document.querySelectorAll('#eventsTable tbody tr');
    rows.forEach(row => {
        row.style.display = row.textContent.toLowerCase().includes(filter) ? '' : 'none';
    });
});";

        private static string CssBlock() => @"
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',system-ui,-apple-system,sans-serif;background:#f3f2f1;color:#323130;padding:24px 32px}
header{margin-bottom:24px}
h1{font-size:28px;font-weight:700;color:#0078d4}
.subtitle{color:#605e5c;margin-top:4px;font-size:14px}
h2{font-size:16px;font-weight:600;margin-bottom:12px;color:#323130}

.kpi-row{display:flex;gap:16px;margin-bottom:24px;flex-wrap:wrap}
.kpi-card{flex:1;min-width:180px;background:#fff;border-radius:8px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.1)}
.kpi-title{font-size:13px;color:#605e5c;margin-bottom:6px}
.kpi-value{font-size:28px;font-weight:700;color:#323130}

.chart-row{display:flex;gap:16px;margin-bottom:24px;flex-wrap:wrap}
.chart-card{flex:1;min-width:320px;background:#fff;border-radius:8px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.1)}
.chart-card.wide{flex:2;min-width:480px}

.table-section{background:#fff;border-radius:8px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.1);margin-bottom:24px}
#tableSearch{width:100%;padding:8px 12px;margin-bottom:12px;border:1px solid #d2d0ce;border-radius:4px;font-size:14px}
.table-wrap{overflow-x:auto}
table{width:100%;border-collapse:collapse;font-size:13px}
thead{background:#f3f2f1}
th{padding:8px 10px;text-align:left;cursor:pointer;user-select:none;white-space:nowrap;font-weight:600;border-bottom:2px solid #d2d0ce}
th:hover{background:#edebe9}
td{padding:6px 10px;border-bottom:1px solid #edebe9;max-width:300px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
tr:hover td{background:#f3f2f1}
.result-success{color:#107c10;font-weight:600}
.result-failure{color:#e74856;font-weight:600}

@media(max-width:768px){.chart-row{flex-direction:column}.kpi-row{flex-direction:column}}
@media print{body{background:#fff;padding:12px}
.chart-card,.kpi-card,.table-section{box-shadow:none;border:1px solid #d2d0ce;break-inside:avoid}}";
    }
}
