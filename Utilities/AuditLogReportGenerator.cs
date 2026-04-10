using IntuneTools.Pages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Generates a self-contained HTML dashboard report from audit log events.
    /// Uses Chart.js (CDN) for interactive charts and a sortable/filterable event table.
    /// Dark mode is enabled by default with a toggle to switch to light mode.
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
            sb.AppendLine("<html lang=\"en\" data-theme=\"dark\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"<title>Intune Audit Log Report \u2014 Last {days} Day(s)</title>");
            sb.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4\"></script>");
            sb.AppendLine("<style>");
            sb.AppendLine(CssBlock());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header with theme toggle
            sb.AppendLine("<header>");
            sb.AppendLine("<div class=\"header-row\">");
            sb.AppendLine("<div>");
            sb.AppendLine("<h1><span class=\"header-icon\">&#x1F6E1;</span> Intune Audit Log Report</h1>");
            sb.AppendLine($"<p class=\"subtitle\">Last {days} day(s) &middot; {HtmlEncode(generated)}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<button id=\"themeToggle\" class=\"theme-btn\" onclick=\"toggleTheme()\" title=\"Toggle light/dark mode\">");
            sb.AppendLine("<span id=\"themeIcon\">\u2600\uFE0F</span>");
            sb.AppendLine("</button>");
            sb.AppendLine("</div>");
            sb.AppendLine("</header>");

            // KPI cards
            sb.AppendLine("<section class=\"kpi-row\">");
            AppendKpiCard(sb, "Total Events", totalEvents.ToString(), "&#x1F4CA;", "kpi-blue");
            AppendKpiCard(sb, "Unique Actors", uniqueActors.ToString(), "&#x1F464;", "kpi-purple");
            AppendKpiCard(sb, "Categories", categories.ToString(), "&#x1F4C2;", "kpi-teal");
            AppendKpiCard(sb, "Success / Failure", $"{successCount} / {failureCount}", "\u2705", "kpi-green");
            sb.AppendLine("</section>");

            // Charts row 1
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card wide\"><div class=\"chart-header\"><h2>Activity Over Time</h2></div><canvas id=\"activityChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><div class=\"chart-header\"><h2>Events by Category</h2></div><canvas id=\"categoryChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Charts row 2
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card\"><div class=\"chart-header\"><h2>Top Actors</h2></div><canvas id=\"actorChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><div class=\"chart-header\"><h2>Success vs Failure</h2></div><canvas id=\"resultChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Charts row 3
            sb.AppendLine("<section class=\"chart-row\">");
            sb.AppendLine("<div class=\"chart-card wide\"><div class=\"chart-header\"><h2>Activity by Hour of Day</h2></div><canvas id=\"hourChart\"></canvas></div>");
            sb.AppendLine("<div class=\"chart-card\"><div class=\"chart-header\"><h2>Events by Operation Type</h2></div><canvas id=\"opTypeChart\"></canvas></div>");
            sb.AppendLine("</section>");

            // Event table
            sb.AppendLine("<section class=\"table-section\">");
            sb.AppendLine("<div class=\"table-header-row\"><h2>All Events</h2><span class=\"event-count\">" + totalEvents.ToString("N0") + " records</span></div>");
            sb.AppendLine("<input id=\"tableSearch\" type=\"text\" placeholder=\"&#x1F50D; Search events\u2026\" />");
            sb.AppendLine("<div class=\"table-wrap\">");
            sb.AppendLine("<table id=\"eventsTable\">");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th onclick=\"sortTable(0)\">Date/Time <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(1)\">Actor <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(2)\">Activity <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(3)\">Category <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(4)\">Result <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(5)\">Component <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(6)\">Operation <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("<th onclick=\"sortTable(7)\">Resources <span class=\"sort-icon\">\u21C5</span></th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var evt in events)
            {
                var resultClass = (evt.ResultText ?? "").ToLowerInvariant() switch
                {
                    "success" => "badge-success",
                    "failure" => "badge-failure",
                    _ => "badge-other"
                };
                sb.AppendLine("<tr>");
                sb.Append($"<td><span class=\"mono\">{HtmlEncode(evt.ActivityDateTimeFormatted)}</span></td>");
                sb.Append($"<td>{HtmlEncode(evt.ActorDisplayName)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.ActivityDisplayName)}</td>");
                sb.Append($"<td><span class=\"category-badge\">{HtmlEncode(evt.CategoryName)}</span></td>");
                sb.Append($"<td><span class=\"badge {resultClass}\">{HtmlEncode(evt.ResultText)}</span></td>");
                sb.Append($"<td>{HtmlEncode(evt.ComponentName)}</td>");
                sb.Append($"<td>{HtmlEncode(evt.OperationType)}</td>");
                sb.Append($"<td title=\"{HtmlEncode(evt.ResourceInfo)}\">{HtmlEncode(evt.ResourceInfo)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table></div></section>");

            // Footer
            sb.AppendLine("<footer>Generated by <strong>IntuneTools</strong></footer>");

            // Scripts
            sb.AppendLine("<script>");
            sb.AppendLine(ThemeScript());
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

        private static void AppendKpiCard(StringBuilder sb, string title, string value, string icon, string colorClass)
        {
            sb.AppendLine($"<div class=\"kpi-card {colorClass}\"><div class=\"kpi-icon\">{icon}</div><div><div class=\"kpi-title\">{HtmlEncode(title)}</div><div class=\"kpi-value\">{HtmlEncode(value)}</div></div></div>");
        }

        private static string JsArray(IEnumerable<string> items) =>
            "[" + string.Join(",", items.Select(i => $"\"{JsEscape(i)}\"")) + "]";

        private static string JsNumArray(IEnumerable<int> items) =>
            "[" + string.Join(",", items) + "]";

        private static string JsEscape(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        private static string HtmlEncode(string? s) =>
            HtmlEncoder.Default.Encode(s ?? "");

        private static string ThemeScript() => @"
function getTheme() { return document.documentElement.getAttribute('data-theme') || 'dark'; }
function setTheme(t) {
    document.documentElement.setAttribute('data-theme', t);
    document.getElementById('themeIcon').textContent = t === 'dark' ? '\u2600\uFE0F' : '\uD83C\uDF19';
    updateChartColors();
}
function toggleTheme() { setTheme(getTheme() === 'dark' ? 'light' : 'dark'); }
let chartInstances = {};
function isDark() { return getTheme() === 'dark'; }
function textColor() { return isDark() ? '#e0e0e0' : '#323130'; }
function gridColor() { return isDark() ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.06)'; }
function updateChartColors() {
    Object.values(chartInstances).forEach(c => {
        if (c.options.scales?.x) { c.options.scales.x.ticks.color = textColor(); c.options.scales.x.grid.color = gridColor(); }
        if (c.options.scales?.y) { c.options.scales.y.ticks.color = textColor(); c.options.scales.y.grid.color = gridColor(); }
        if (c.options.plugins?.legend) { c.options.plugins.legend.labels.color = textColor(); }
        c.update();
    });
}";

        private static string BuildChartScripts(
            List<string> dateLabels, List<int> dateCounts,
            List<string> catLabels, List<int> catCounts,
            List<string> actorLabels, List<int> actorCounts,
            int success, int failure, int other,
            List<string> opLabels, List<int> opCounts,
            List<string> hourLabels, List<int> hourCounts)
        {
            var palette = "['#60a5fa','#f87171','#34d399','#a78bfa','#fbbf24','#38bdf8','#fb923c','#c084fc','#22d3ee','#4ade80','#e879f9','#94a3b8']";

            var sb = new StringBuilder();

            // Shared defaults
            sb.AppendLine("Chart.defaults.color = textColor();");
            sb.AppendLine("Chart.defaults.borderColor = gridColor();");

            // Activity over time - bar with gradient
            sb.AppendLine($@"(function(){{
const ctx=document.getElementById('activityChart').getContext('2d');
const g=ctx.createLinearGradient(0,0,0,300);g.addColorStop(0,'rgba(96,165,250,0.8)');g.addColorStop(1,'rgba(96,165,250,0.1)');
chartInstances.activity=new Chart(ctx,{{type:'bar',data:{{labels:{JsArray(dateLabels)},datasets:[{{label:'Events',data:{JsNumArray(dateCounts)},backgroundColor:g,borderColor:'#60a5fa',borderWidth:1,borderRadius:4}}]}},options:{{responsive:true,plugins:{{legend:{{display:false}}}},scales:{{y:{{beginAtZero:true,ticks:{{precision:0,color:textColor()}},grid:{{color:gridColor()}}}},x:{{ticks:{{color:textColor()}},grid:{{color:gridColor()}}}}}}}}}});
}})();");

            // Category - doughnut
            sb.AppendLine($"chartInstances.category=new Chart(document.getElementById('categoryChart'),{{type:'doughnut',data:{{labels:{JsArray(catLabels)},datasets:[{{data:{JsNumArray(catCounts)},backgroundColor:{palette},borderWidth:0,hoverOffset:6}}]}},options:{{responsive:true,cutout:'60%',plugins:{{legend:{{position:'right',labels:{{color:textColor(),padding:12,usePointStyle:true,pointStyle:'circle'}}}}}}}}}});");

            // Top actors - horizontal bar
            sb.AppendLine($"chartInstances.actor=new Chart(document.getElementById('actorChart'),{{type:'bar',data:{{labels:{JsArray(actorLabels)},datasets:[{{label:'Events',data:{JsNumArray(actorCounts)},backgroundColor:'rgba(167,139,250,0.7)',borderColor:'#a78bfa',borderWidth:1,borderRadius:4}}]}},options:{{indexAxis:'y',responsive:true,plugins:{{legend:{{display:false}}}},scales:{{x:{{beginAtZero:true,ticks:{{precision:0,color:textColor()}},grid:{{color:gridColor()}}}},y:{{ticks:{{color:textColor()}},grid:{{display:false}}}}}}}}}});");

            // Success vs failure - doughnut
            sb.AppendLine($"chartInstances.result=new Chart(document.getElementById('resultChart'),{{type:'doughnut',data:{{labels:['Success','Failure','Other'],datasets:[{{data:[{success},{failure},{other}],backgroundColor:['#34d399','#f87171','#94a3b8'],borderWidth:0,hoverOffset:6}}]}},options:{{responsive:true,cutout:'60%',plugins:{{legend:{{position:'bottom',labels:{{color:textColor(),padding:16,usePointStyle:true,pointStyle:'circle'}}}}}}}}}});");

            // Operation type - doughnut
            sb.AppendLine($"chartInstances.opType=new Chart(document.getElementById('opTypeChart'),{{type:'doughnut',data:{{labels:{JsArray(opLabels)},datasets:[{{data:{JsNumArray(opCounts)},backgroundColor:{palette},borderWidth:0,hoverOffset:6}}]}},options:{{responsive:true,cutout:'60%',plugins:{{legend:{{position:'right',labels:{{color:textColor(),padding:12,usePointStyle:true,pointStyle:'circle'}}}}}}}}}});");

            // Hour of day - bar with gradient
            sb.AppendLine($@"(function(){{
const ctx=document.getElementById('hourChart').getContext('2d');
const g=ctx.createLinearGradient(0,0,0,300);g.addColorStop(0,'rgba(52,211,153,0.8)');g.addColorStop(1,'rgba(52,211,153,0.1)');
chartInstances.hour=new Chart(ctx,{{type:'bar',data:{{labels:{JsArray(hourLabels)},datasets:[{{label:'Events',data:{JsNumArray(hourCounts)},backgroundColor:g,borderColor:'#34d399',borderWidth:1,borderRadius:4}}]}},options:{{responsive:true,plugins:{{legend:{{display:false}}}},scales:{{y:{{beginAtZero:true,ticks:{{precision:0,color:textColor()}},grid:{{color:gridColor()}}}},x:{{ticks:{{color:textColor()}},grid:{{color:gridColor()}}}}}}}}}});
}})();");

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
/* ── CSS Custom Properties (Dark default) ── */
:root, [data-theme='dark'] {
    --bg: #0f1117;
    --bg-card: #1a1d27;
    --bg-card-hover: #222632;
    --text: #e4e4e7;
    --text-muted: #9ca3af;
    --border: rgba(255,255,255,0.06);
    --accent: #60a5fa;
    --shadow: 0 2px 8px rgba(0,0,0,0.3), 0 0 0 1px rgba(255,255,255,0.04);
    --input-bg: #1e2130;
    --table-stripe: rgba(255,255,255,0.02);
    --table-hover: rgba(96,165,250,0.06);
    --badge-success-bg: rgba(52,211,153,0.15);
    --badge-success-text: #34d399;
    --badge-failure-bg: rgba(248,113,113,0.15);
    --badge-failure-text: #f87171;
    --badge-other-bg: rgba(148,163,184,0.12);
    --badge-other-text: #94a3b8;
    --kpi-blue: linear-gradient(135deg, rgba(96,165,250,0.12), rgba(96,165,250,0.03));
    --kpi-purple: linear-gradient(135deg, rgba(167,139,250,0.12), rgba(167,139,250,0.03));
    --kpi-teal: linear-gradient(135deg, rgba(52,211,153,0.12), rgba(52,211,153,0.03));
    --kpi-green: linear-gradient(135deg, rgba(74,222,128,0.12), rgba(74,222,128,0.03));
}
[data-theme='light'] {
    --bg: #f4f5f7;
    --bg-card: #ffffff;
    --bg-card-hover: #f9fafb;
    --text: #1f2937;
    --text-muted: #6b7280;
    --border: rgba(0,0,0,0.08);
    --accent: #2563eb;
    --shadow: 0 1px 4px rgba(0,0,0,0.06), 0 0 0 1px rgba(0,0,0,0.04);
    --input-bg: #ffffff;
    --table-stripe: rgba(0,0,0,0.015);
    --table-hover: rgba(37,99,235,0.04);
    --badge-success-bg: rgba(22,163,74,0.1);
    --badge-success-text: #16a34a;
    --badge-failure-bg: rgba(220,38,38,0.1);
    --badge-failure-text: #dc2626;
    --badge-other-bg: rgba(107,114,128,0.1);
    --badge-other-text: #6b7280;
    --kpi-blue: linear-gradient(135deg, rgba(37,99,235,0.08), rgba(37,99,235,0.02));
    --kpi-purple: linear-gradient(135deg, rgba(124,58,237,0.08), rgba(124,58,237,0.02));
    --kpi-teal: linear-gradient(135deg, rgba(13,148,136,0.08), rgba(13,148,136,0.02));
    --kpi-green: linear-gradient(135deg, rgba(22,163,74,0.08), rgba(22,163,74,0.02));
}

/* ── Reset & Base ── */
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI Variable','Segoe UI',system-ui,-apple-system,sans-serif;background:var(--bg);color:var(--text);padding:24px 40px;line-height:1.5;transition:background .25s,color .25s}

/* ── Header ── */
header{margin-bottom:28px}
.header-row{display:flex;align-items:center;justify-content:space-between}
h1{font-size:26px;font-weight:700;color:var(--accent);display:flex;align-items:center;gap:10px}
.header-icon{font-size:28px}
.subtitle{color:var(--text-muted);margin-top:4px;font-size:13px;letter-spacing:0.2px}
h2{font-size:15px;font-weight:600;color:var(--text);letter-spacing:0.1px}

/* ── Theme toggle ── */
.theme-btn{background:var(--bg-card);border:1px solid var(--border);border-radius:10px;width:42px;height:42px;font-size:18px;cursor:pointer;display:flex;align-items:center;justify-content:center;transition:all .2s;box-shadow:var(--shadow)}
.theme-btn:hover{transform:scale(1.08);background:var(--bg-card-hover)}

/* ── KPI Cards ── */
.kpi-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:16px;margin-bottom:24px}
.kpi-card{background:var(--bg-card);border-radius:12px;padding:20px 22px;box-shadow:var(--shadow);display:flex;align-items:center;gap:16px;transition:transform .15s,box-shadow .15s,background .25s;border:1px solid var(--border)}
.kpi-card:hover{transform:translateY(-2px);box-shadow:var(--shadow),0 8px 24px rgba(0,0,0,0.12)}
.kpi-card.kpi-blue{background:var(--kpi-blue)}
.kpi-card.kpi-purple{background:var(--kpi-purple)}
.kpi-card.kpi-teal{background:var(--kpi-teal)}
.kpi-card.kpi-green{background:var(--kpi-green)}
.kpi-icon{font-size:32px;flex-shrink:0}
.kpi-title{font-size:12px;color:var(--text-muted);text-transform:uppercase;letter-spacing:0.6px;margin-bottom:2px}
.kpi-value{font-size:28px;font-weight:700;color:var(--text)}

/* ── Chart Cards ── */
.chart-row{display:flex;gap:16px;margin-bottom:20px;flex-wrap:wrap}
.chart-card{flex:1;min-width:320px;background:var(--bg-card);border-radius:12px;padding:22px;box-shadow:var(--shadow);transition:background .25s;border:1px solid var(--border)}
.chart-card.wide{flex:2;min-width:480px}
.chart-header{margin-bottom:16px;display:flex;align-items:center;justify-content:space-between}

/* ── Table ── */
.table-section{background:var(--bg-card);border-radius:12px;padding:22px;box-shadow:var(--shadow);margin-bottom:24px;transition:background .25s;border:1px solid var(--border)}
.table-header-row{display:flex;align-items:center;justify-content:space-between;margin-bottom:14px}
.event-count{font-size:13px;color:var(--text-muted);background:var(--table-stripe);padding:4px 12px;border-radius:20px}
#tableSearch{width:100%;padding:10px 14px;margin-bottom:14px;border:1px solid var(--border);border-radius:8px;font-size:14px;background:var(--input-bg);color:var(--text);transition:border-color .2s,background .25s,color .25s;outline:none}
#tableSearch:focus{border-color:var(--accent);box-shadow:0 0 0 3px rgba(96,165,250,0.15)}
.table-wrap{overflow-x:auto}
table{width:100%;border-collapse:collapse;font-size:13px}
thead{background:transparent}
th{padding:10px 12px;text-align:left;cursor:pointer;user-select:none;white-space:nowrap;font-weight:600;border-bottom:2px solid var(--border);color:var(--text-muted);font-size:12px;text-transform:uppercase;letter-spacing:0.5px;transition:color .15s}
th:hover{color:var(--accent)}
.sort-icon{font-size:11px;opacity:0.4}
td{padding:8px 12px;border-bottom:1px solid var(--border);max-width:280px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;transition:background .1s}
tbody tr:nth-child(even){background:var(--table-stripe)}
tbody tr:hover td{background:var(--table-hover)}
.mono{font-family:'Cascadia Code','Consolas',monospace;font-size:12px;opacity:0.9}

/* ── Badges ── */
.badge{display:inline-block;padding:2px 10px;border-radius:20px;font-size:12px;font-weight:600;letter-spacing:0.2px}
.badge-success{background:var(--badge-success-bg);color:var(--badge-success-text)}
.badge-failure{background:var(--badge-failure-bg);color:var(--badge-failure-text)}
.badge-other{background:var(--badge-other-bg);color:var(--badge-other-text)}
.category-badge{display:inline-block;padding:2px 8px;border-radius:4px;font-size:12px;background:var(--table-stripe);color:var(--text-muted)}

/* ── Footer ── */
footer{text-align:center;color:var(--text-muted);font-size:12px;padding:16px 0 8px;border-top:1px solid var(--border);margin-top:8px}

/* ── Responsive ── */
@media(max-width:768px){body{padding:16px}
.chart-row,.kpi-row{flex-direction:column}
.chart-card.wide{min-width:auto}}
@media print{body{background:#fff;color:#000;padding:12px}
.theme-btn{display:none}
.chart-card,.kpi-card,.table-section{box-shadow:none;border:1px solid #d2d0ce;break-inside:avoid}}";
    }
}
