using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace IntuneTools.Pages
{
    public class AssignmentInfo
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Platform { get; set; }
    }

    public sealed partial class AssignmentPage : Page
    {
        public static ObservableCollection<AssignmentInfo> AssignmentList { get; } = new();
        private bool _suppressOptionEvents = false;
        private bool _suppressSelectAllEvents = false;

        public AssignmentPage()
        {
            this.InitializeComponent();

            AssignmentList.Add(new AssignmentInfo { Name = "App One", Id = "001", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Two", Id = "002", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Three", Id = "003", Platform = "Windows", Type = "Win32" });
            AssignmentList.Add(new AssignmentInfo { Name = "App Four", Id = "004", Platform = "Windows", Type = "Win32" });

            AppDataGrid.ItemsSource = AssignmentList;

            this.Loaded += AssignmentPage_Loaded;

            AppendToDetailsRichTextBlock("Assignment page loaded.");
        }

        

        #region Orchestrators

        private async Task ListAllOrchestrator(GraphServiceClient graphServiceClient)
        {
            AssignmentList.Clear();

            var selectedContent = GetCheckedOptionNames();

           
            if (selectedContent.Count == 0)
            {
                // If no options are selected, show a message and return
                AppendToDetailsRichTextBlock("No content types selected for import.");
                return;
            }

            AppendToDetailsRichTextBlock("Listing all content.");
        }

        #endregion

        #region Button click handlers
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            AppendToDetailsRichTextBlock("Search clicked (not implemented).");
        }

        private async void ListAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ListAllOrchestrator(sourceGraphServiceClient);
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppDataGrid.SelectedItems == null || AppDataGrid.SelectedItems.Count == 0)
                return;

            var toRemove = AppDataGrid.SelectedItems.Cast<AssignmentInfo>().ToList();
            foreach (var item in toRemove)
            {
                AssignmentList.Remove(item);
            }
            AppendToDetailsRichTextBlock($"Removed {toRemove.Count} item(s).");
        }

        private async void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Log Console?",
                Content = "Are you sure you want to clear all log console text? This action cannot be undone.",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync().AsTask();
            if (result == ContentDialogResult.Primary)
            {
                LogConsole.Blocks.Clear();
            }
        }

        #endregion

        #region Helpers

        private void AssignmentPage_Loaded(object sender, RoutedEventArgs e)
        {
            AutoCheckAllOptions();
        }

        private void AutoCheckAllOptions()
        {
            _suppressOptionEvents = true;
            foreach (var cb in OptionsPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox"))
            {
                cb.IsChecked = true;
            }
            _suppressOptionEvents = false;

            _suppressSelectAllEvents = true;
            OptionsAllCheckBox.IsChecked = true;
            _suppressSelectAllEvents = false;
        }

        private void AppendToDetailsRichTextBlock(string text)
        {
            Paragraph paragraph;
            if (LogConsole.Blocks.Count == 0)
            {
                paragraph = new Paragraph();
                LogConsole.Blocks.Add(paragraph);
            }
            else
            {
                paragraph = LogConsole.Blocks.First() as Paragraph;
                if (paragraph == null)
                {
                    paragraph = new Paragraph();
                    LogConsole.Blocks.Add(paragraph);
                }
            }
            if (paragraph.Inlines.Count > 0)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new Run { Text = text });
        }

        public List<string> GetCheckedOptionNames()
        {
            var checkedNames = new List<string>();
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true)
                {
                    checkedNames.Add(cb.Name);
                }
            }
            return checkedNames;
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = true;
                }
            }
            _suppressOptionEvents = false;
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllEvents) return;
            _suppressOptionEvents = true;
            foreach (var child in OptionsPanel.Children)
            {
                if (child is CheckBox cb && cb.Name != "OptionsAllCheckBox")
                {
                    cb.IsChecked = false;
                }
            }
            _suppressOptionEvents = false;
        }

        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // Optional: handle indeterminate state
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        private void Option_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressOptionEvents) return;
            UpdateSelectAllCheckBox();
        }

        private void UpdateSelectAllCheckBox()
        {
            var optionCheckBoxes = OptionsPanel.Children.OfType<CheckBox>().Where(cb => cb.Name != "OptionsAllCheckBox").ToList();

            if (!optionCheckBoxes.Any())
                return;

            bool?[] states = optionCheckBoxes.Select(cb => cb.IsChecked).ToArray();

            _suppressSelectAllEvents = true;
            if (states.All(x => x == true))
                OptionsAllCheckBox.IsChecked = true;
            else if (states.All(x => x == false))
                OptionsAllCheckBox.IsChecked = false;
            else
                OptionsAllCheckBox.IsChecked = null;
            _suppressSelectAllEvents = false;
        }

        #endregion
    }
}
