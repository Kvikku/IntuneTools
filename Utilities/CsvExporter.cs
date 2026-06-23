using System.Text;
using Windows.Storage.Pickers;

namespace IntuneTools.Utilities
{
    public static class CsvExporter
    {
        /// <summary>
        /// Exports a standard content list (Name, Type, Platform, Description, ID) to a CSV file
        /// chosen by the user via a FileSavePicker.
        /// </summary>
        public static async Task ExportContentListAsync(
            IEnumerable<CustomContentInfo> items,
            string pageName)
        {
            var file = await PickSaveFileAsync($"{pageName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            if (file == null)
                return;

            var csv = new StringBuilder();
            csv.AppendLine("Name,Type,Platform,Description,ID");

            foreach (var item in items)
            {
                csv.AppendLine(string.Join(",",
                    Escape(item.ContentName),
                    Escape(item.ContentType),
                    Escape(item.ContentPlatform),
                    Escape(item.ContentDescription),
                    Escape(item.ContentId)));
            }

            await File.WriteAllTextAsync(file.Path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        /// <summary>
        /// Exports a content list with its assignment targets, one row per assignment.
        /// Items with no assignments are exported as a single row with empty assignment columns.
        /// Group IDs are substituted with display names from the provided lookup.
        /// </summary>
        public static async Task ExportWithAssignmentsAsync(
            IEnumerable<(CustomContentInfo Content, IEnumerable<AssignmentInfo> Assignments)> items,
            Dictionary<string, string> groupNames,
            string pageName)
        {
            var file = await PickSaveFileAsync($"{pageName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            if (file == null)
                return;

            var csv = new StringBuilder();
            csv.AppendLine("Name,Type,Platform,Assignment Target,Target Group,Filter ID,Filter Type,ID");

            foreach (var (content, assignments) in items)
            {
                var assignmentList = assignments?.ToList() ?? new List<AssignmentInfo>();

                if (assignmentList.Count == 0)
                {
                    csv.AppendLine(string.Join(",",
                        Escape(content.ContentName),
                        Escape(content.ContentType),
                        Escape(content.ContentPlatform),
                        string.Empty, string.Empty, string.Empty, string.Empty,
                        Escape(content.ContentId)));
                    continue;
                }

                foreach (var assignment in assignmentList)
                {
                    var targetGroup = ResolveTargetGroup(assignment, groupNames);

                    csv.AppendLine(string.Join(",",
                        Escape(content.ContentName),
                        Escape(content.ContentType),
                        Escape(content.ContentPlatform),
                        Escape(assignment.TargetType),
                        Escape(targetGroup),
                        Escape(assignment.FilterId),
                        Escape(assignment.FilterType),
                        Escape(content.ContentId)));
                }
            }

            await File.WriteAllTextAsync(file.Path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static string ResolveTargetGroup(AssignmentInfo assignment, Dictionary<string, string> groupNames)
        {
            if (string.IsNullOrEmpty(assignment.GroupId))
                return string.Empty;

            if (groupNames.TryGetValue(assignment.GroupId, out var name))
                return name;

            return assignment.GroupId;
        }

        private static async Task<Windows.Storage.StorageFile?> PickSaveFileAsync(string suggestedFileName)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedFileName
            };
            picker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            return await picker.PickSaveFileAsync();
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var escaped = value
                .Replace("\"", "\"\"")
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");

            // Quote if the value needs it for CSV, or starts with a formula trigger character (Excel/Sheets injection)
            if (escaped.Contains(',') || escaped.Contains('"') ||
                escaped[0] == '=' || escaped[0] == '+' || escaped[0] == '-' || escaped[0] == '@')
                return $"\"{escaped}\"";

            return escaped;
        }
    }
}
