using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IntuneTools.Pages.Controls
{
    /// <summary>
    /// Connection status of a tenant, used by <see cref="TenantPill"/>.
    /// </summary>
    public enum TenantConnectionStatus
    {
        NotSignedIn,
        SignedIn,
        Warning,
        Error
    }

    /// <summary>
    /// Status pill showing a tenant role ("Source" / "Destination") together with the
    /// signed-in account, plus a colored dot indicating connection health.
    ///
    /// Single source of truth for tenant chrome — used by MainWindow PaneFooter and by
    /// SettingsPage so they always look identical. See XAML_STYLE_GUIDE.md §8.
    /// </summary>
    public sealed partial class TenantPill : UserControl
    {
        public TenantPill()
        {
            InitializeComponent();
            UpdateDerived();
        }

        public static readonly DependencyProperty RoleProperty =
            DependencyProperty.Register(nameof(Role), typeof(string), typeof(TenantPill),
                new PropertyMetadata("Source", OnAnyChanged));

        /// <summary>"Source" or "Destination" — prepended to the display text.</summary>
        public string Role
        {
            get => (string)GetValue(RoleProperty);
            set => SetValue(RoleProperty, value);
        }

        public static readonly DependencyProperty TenantNameProperty =
            DependencyProperty.Register(nameof(TenantName), typeof(string), typeof(TenantPill),
                new PropertyMetadata(string.Empty, OnAnyChanged));

        /// <summary>Display name of the signed-in tenant/account, or empty when not signed in.</summary>
        public string TenantName
        {
            get => (string)GetValue(TenantNameProperty);
            set => SetValue(TenantNameProperty, value);
        }

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(TenantConnectionStatus), typeof(TenantPill),
                new PropertyMetadata(TenantConnectionStatus.NotSignedIn, OnAnyChanged));

        public TenantConnectionStatus Status
        {
            get => (TenantConnectionStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(TenantPill),
                new PropertyMetadata(string.Empty));

        public string ToolTipText
        {
            get => (string)GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(TenantPill),
                new PropertyMetadata(string.Empty));

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public static readonly DependencyProperty DotBrushProperty =
            DependencyProperty.Register(nameof(DotBrush), typeof(Brush), typeof(TenantPill),
                new PropertyMetadata(null));

        public Brush? DotBrush
        {
            get => (Brush?)GetValue(DotBrushProperty);
            private set => SetValue(DotBrushProperty, value);
        }

        private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TenantPill)d).UpdateDerived();
        }

        private void UpdateDerived()
        {
            var role = string.IsNullOrEmpty(Role) ? "Tenant" : Role;
            var name = string.IsNullOrEmpty(TenantName) ? "Not signed in" : TenantName;
            DisplayText = $"{role}: {name}";

            string brushKey = Status switch
            {
                TenantConnectionStatus.SignedIn => "StatusSuccessBrush",
                TenantConnectionStatus.Warning  => "StatusWarningBrush",
                TenantConnectionStatus.Error    => "StatusDangerBrush",
                _                                => "StatusNeutralBrush",
            };

            if (Application.Current?.Resources?.TryGetValue(brushKey, out var brushObj) == true && brushObj is Brush b)
            {
                DotBrush = b;
            }
        }
    }
}
