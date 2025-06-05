using Microsoft.UI.Xaml; // Added for RoutedEventArgs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using static IntuneTools.Utilities.HelperClass;
using static IntuneTools.Utilities.Variables;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IntuneTools.Pages
{

    public class ContentInfo
    {
        public string? ContentName { get; set; }
        public string? ContentPlatform { get; set; }
        public string? ContentType { get; set; }
    }

    public sealed partial class ImportPage : Page
    {
        public ObservableCollection<ContentInfo> ContentList { get; set; } = new ObservableCollection<ContentInfo>();

        public ImportPage()
        {
            this.InitializeComponent();
        }

        public void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            CreateImportStatusFile(); // Ensure the import status file is created before importing
        }
        private void ListAllButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {

        }
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the selected items in the DataGrid
            ContentDataGrid.SelectedItems.Clear();
        }
    }
} // End of namespace IntuneTools.Pages
