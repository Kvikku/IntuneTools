using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InToolz.Utilities
{
    /// <summary>
    /// Holds assignment retrieval results for a single content item, for display in
    /// <see cref="AssignmentViewerDialog"/>.
    /// </summary>
    public record AssignmentResult(
        string ContentName,
        string ContentType,
        string ContentId,
        List<AssignmentInfo>? Assignments,
        string? ErrorMessage);

    /// <summary>
    /// Shared "view current assignments" dialog: summary stat badges + one expander per content
    /// item, each listing its assignments. Generalizes what was previously
    /// ManageAssignmentsPage's private ShowAssignmentsDialogAsync and its four Build* helpers.
    ///
    /// Pass <paramref name="removeRegistry"/> (via <see cref="ShowAsync"/>) to get interactive
    /// mode — per-assignment remove buttons and a "Remove All Assignments" primary button —
    /// which is ManageAssignmentsPage's own flow. Omit it (or pass an empty dictionary) for
    /// read-only mode, used by <see cref="RightClickMenu"/>'s "View assignments" item (and
    /// therefore every page that attaches <see cref="RightClickMenu.AttachDataGridContextMenu"/>,
    /// including AssignmentPage).
    /// </summary>
    public static class AssignmentViewerDialog
    {
        public static async Task<bool> ShowAsync(
            XamlRoot xamlRoot,
            List<AssignmentResult> results,
            Dictionary<string, string> groupNames,
            Dictionary<string, Func<GraphServiceClient, string, string, Task>>? removeRegistry = null,
            Action<string>? onLogSuccess = null,
            Action<string>? onLogError = null)
        {
            removeRegistry ??= new Dictionary<string, Func<GraphServiceClient, string, string, Task>>();

            var rootPanel = new StackPanel { Spacing = 16 };

            int totalAssignments = results.Where(r => r.Assignments != null).Sum(r => r.Assignments!.Count);
            int withAssignments = results.Count(r => r.Assignments is { Count: > 0 });
            int withoutAssignments = results.Count(r => r.Assignments != null && r.Assignments.Count == 0);
            int withErrors = results.Count(r => r.ErrorMessage != null);

            var summaryPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            summaryPanel.Children.Add(BuildStatBadge(results.Count.ToString(), "Checked", 0x00, 0x78, 0xD4));
            summaryPanel.Children.Add(BuildStatBadge(totalAssignments.ToString(), "Assignments", 0x10, 0x7C, 0x10));
            summaryPanel.Children.Add(BuildStatBadge(withoutAssignments.ToString(), "Unassigned", 0xCA, 0x50, 0x10));
            if (withErrors > 0)
                summaryPanel.Children.Add(BuildStatBadge(withErrors.ToString(), "Errors", 0xC4, 0x2B, 0x1C));
            rootPanel.Children.Add(summaryPanel);

            foreach (var result in results)
            {
                rootPanel.Children.Add(BuildItemExpander(result, results.Count <= 5, groupNames, removeRegistry, onLogSuccess, onLogError));
            }

            var dialog = new ContentDialog
            {
                Title = "Manage Assignments",
                Content = new ScrollViewer
                {
                    Content = rootPanel,
                    MaxHeight = 500,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                CloseButtonText = "Close",
                XamlRoot = xamlRoot,
                DefaultButton = ContentDialogButton.Close
            };

            // Only offer bulk removal when the caller actually wired up a remove registry — in
            // read-only mode (RightClickMenu) there's nothing for this button to do.
            if (withAssignments > 0 && removeRegistry.Count > 0)
            {
                dialog.PrimaryButtonText = "Remove All Assignments";
                var destructiveStyle = new Style(typeof(Button));
                destructiveStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                    new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C))));
                destructiveStyle.Setters.Add(new Setter(Control.ForegroundProperty,
                    new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF))));
                dialog.PrimaryButtonStyle = destructiveStyle;
            }

            var dialogResult = await dialog.ShowAsync();
            return dialogResult == ContentDialogResult.Primary;
        }

        /// <summary>
        /// Builds a colored stat badge for the summary row.
        /// </summary>
        private static Border BuildStatBadge(string value, string label, byte r, byte g, byte b)
        {
            var color = Windows.UI.Color.FromArgb(255, r, g, b);
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return new Border
            {
                Background = new SolidColorBrush(color) { Opacity = 0.1 },
                BorderBrush = new SolidColorBrush(color) { Opacity = 0.3 },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = panel
            };
        }

        /// <summary>
        /// Builds an expander for a single content item showing its assignments.
        /// Includes per-assignment remove buttons when a removal handler is available for its type.
        /// </summary>
        private static Expander BuildItemExpander(
            AssignmentResult result,
            bool autoExpand,
            Dictionary<string, string> groupNames,
            Dictionary<string, Func<GraphServiceClient, string, string, Task>> removeRegistry,
            Action<string>? onLogSuccess,
            Action<string>? onLogError)
        {
            var expander = new Expander
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsExpanded = autoExpand
            };

            // Header: name + type badge + assignment count
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            headerPanel.Children.Add(new TextBlock
            {
                Text = result.ContentName,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            headerPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 128, 128, 128)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock
                {
                    Text = result.ContentType,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            var countText = result.ErrorMessage != null
                ? "Error"
                : $"{result.Assignments?.Count ?? 0} assignment(s)";
            var countBlock = new TextBlock
            {
                Text = countText,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.6
            };
            headerPanel.Children.Add(countBlock);
            expander.Header = headerPanel;

            // Content: assignments list, empty state, or error
            if (result.ErrorMessage != null)
            {
                expander.Content = new InfoBar
                {
                    Severity = InfoBarSeverity.Error,
                    Message = result.ErrorMessage,
                    IsOpen = true,
                    IsClosable = false
                };
            }
            else if (result.Assignments == null || result.Assignments.Count == 0)
            {
                expander.Content = new TextBlock
                {
                    Text = "No assignments found.",
                    Opacity = 0.6,
                    Margin = new Thickness(0, 4, 0, 4)
                };
            }
            else
            {
                var listPanel = new StackPanel { Spacing = 4 };
                var canRemove = !string.IsNullOrEmpty(result.ContentId)
                    && removeRegistry.ContainsKey(result.ContentType);

                foreach (var assignment in result.Assignments)
                {
                    var row = BuildAssignmentRow(assignment, groupNames);

                    if (canRemove && !string.IsNullOrEmpty(assignment.AssignmentId))
                    {
                        var btn = BuildRemoveAssignmentButton(
                            result, assignment, removeRegistry, listPanel, countBlock, expander, onLogSuccess, onLogError);
                        Grid.SetColumn(btn, 3);
                        row.Children.Add(btn);
                    }

                    listPanel.Children.Add(row);
                }
                expander.Content = listPanel;
            }

            return expander;
        }

        /// <summary>
        /// Builds a remove button with flyout confirmation for a single assignment row.
        /// </summary>
        private static Button BuildRemoveAssignmentButton(
            AssignmentResult result,
            AssignmentInfo assignment,
            Dictionary<string, Func<GraphServiceClient, string, string, Task>> removeRegistry,
            StackPanel listPanel,
            TextBlock countBlock,
            Expander expander,
            Action<string>? onLogSuccess,
            Action<string>? onLogError)
        {
            var removeBtn = new Button
            {
                Content = new FontIcon
                {
                    Glyph = "",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C))
                },
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(removeBtn, "Remove this assignment");

            // Flyout confirmation
            var flyout = new Flyout();
            var flyoutPanel = new StackPanel { Spacing = 8, Width = 260 };
            flyoutPanel.Children.Add(new TextBlock
            {
                Text = "Remove this assignment?",
                FontWeight = FontWeights.SemiBold
            });
            flyoutPanel.Children.Add(new TextBlock
            {
                Text = "This will remove this specific assignment from the policy. This action cannot be undone.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                FontSize = 12
            });

            var confirmBtn = new Button
            {
                Content = "Remove",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            // Style the confirm button red
            confirmBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C));
            confirmBtn.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));

            flyoutPanel.Children.Add(confirmBtn);
            flyout.Content = flyoutPanel;
            removeBtn.Flyout = flyout;

            // Capture references for the async closure
            var capturedAssignmentId = assignment.AssignmentId!;
            var capturedContentType = result.ContentType;
            var capturedContentId = result.ContentId;
            var capturedContentName = result.ContentName;

            confirmBtn.Click += async (s, e) =>
            {
                flyout.Hide();
                removeBtn.IsEnabled = false;
                removeBtn.Content = new ProgressRing { Width = 14, Height = 14, IsActive = true };

                try
                {
                    await removeRegistry[capturedContentType](
                        RequireSourceClient(), capturedContentId, capturedAssignmentId);

                    // Find and remove the row (the button's parent Grid)
                    var row = removeBtn.Parent as Grid;
                    if (row != null)
                        listPanel.Children.Remove(row);

                    var remaining = listPanel.Children.Count;
                    countBlock.Text = $"{remaining} assignment(s)";

                    if (remaining == 0)
                    {
                        expander.Content = new TextBlock
                        {
                            Text = "All assignments removed.",
                            Opacity = 0.6,
                            Margin = new Thickness(0, 4, 0, 4)
                        };
                    }

                    onLogSuccess?.Invoke($"Removed assignment from '{capturedContentName}'.");
                }
                catch (Exception ex)
                {
                    removeBtn.Content = new FontIcon
                    {
                        Glyph = "",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xC4, 0x2B, 0x1C))
                    };
                    removeBtn.IsEnabled = true;
                    onLogError?.Invoke($"Failed to remove assignment from '{capturedContentName}': {ex.Message}");
                }
            };

            return removeBtn;
        }

        /// <summary>
        /// Builds a single assignment row with a colored indicator dot and details.
        /// Uses a Grid layout with 4 columns: dot, type, details (flexible), and a placeholder for the remove button.
        /// </summary>
        private static Grid BuildAssignmentRow(AssignmentInfo assignment, Dictionary<string, string> groupNames)
        {
            var (r, g, b) = assignment.TargetType switch
            {
                "All Users" => ((byte)0x00, (byte)0x78, (byte)0xD4),
                "All Devices" => ((byte)0x00, (byte)0x78, (byte)0xD4),
                "Group" => ((byte)0x10, (byte)0x7C, (byte)0x10),
                "Exclusion Group" => ((byte)0xC4, (byte)0x2B, (byte)0x1C),
                _ => ((byte)0x88, (byte)0x88, (byte)0x88)
            };

            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = GridLength.Auto });                      // dot
            row.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = GridLength.Auto });                      // type
            row.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // details
            row.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = GridLength.Auto });                      // remove button

            // Color indicator dot
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(dot, 0);
            row.Children.Add(dot);

            // Target type label
            var typeBlock = new TextBlock
            {
                Text = assignment.TargetType ?? "Unknown",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(typeBlock, 1);
            row.Children.Add(typeBlock);

            // Details: Group name (with ID fallback), Filter info
            var details = new List<string>();
            if (!string.IsNullOrEmpty(assignment.GroupId))
            {
                var groupLabel = groupNames.TryGetValue(assignment.GroupId, out var name)
                    ? $"{name} ({assignment.GroupId})"
                    : assignment.GroupId;
                details.Add(groupLabel);
            }
            if (!string.IsNullOrEmpty(assignment.FilterId))
                details.Add($"Filter: {assignment.FilterId} ({assignment.FilterType})");

            if (details.Count > 0)
            {
                var detailsBlock = new TextBlock
                {
                    Text = string.Join(" · ", details),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsTextSelectionEnabled = true,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Opacity = 0.8
                };
                Grid.SetColumn(detailsBlock, 2);
                row.Children.Add(detailsBlock);
            }

            return row;
        }
    }
}
