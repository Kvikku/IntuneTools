using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IntuneTools.Utilities
{
    /// <summary>
    /// Describes a single Intune/Entra content item displayed in the staging grids.
    /// Implements <see cref="INotifyPropertyChanged"/> so that the live-preview column
    /// on the Renaming page can refresh as the user edits prefix/description input
    /// without having to rebind the entire DataGrid.
    /// </summary>
    public class CustomContentInfo : INotifyPropertyChanged
    {
        private string? _contentName;
        private string? _contentPlatform;
        private string? _contentType;
        private string? _contentId;
        private string? _contentDescription;
        private string? _previewName;

        public string? ContentName
        {
            get => _contentName;
            set => SetField(ref _contentName, value);
        }

        public string? ContentPlatform
        {
            get => _contentPlatform;
            set => SetField(ref _contentPlatform, value);
        }

        public string? ContentType
        {
            get => _contentType;
            set => SetField(ref _contentType, value);
        }

        public string? ContentId
        {
            get => _contentId;
            set => SetField(ref _contentId, value);
        }

        public string? ContentDescription
        {
            get => _contentDescription;
            set => SetField(ref _contentDescription, value);
        }

        /// <summary>
        /// Live preview of the new display name that will be produced by the currently
        /// configured rename operation. Populated by the Renaming page; other pages
        /// leave it <c>null</c> so their DataGrids simply show an empty column if bound.
        /// </summary>
        public string? PreviewName
        {
            get => _previewName;
            set => SetField(ref _previewName, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}