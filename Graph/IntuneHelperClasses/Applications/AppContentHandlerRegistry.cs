using System;
using System.Collections.Generic;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Single source of truth for "which app type is handled how". Adding a
    /// new <see cref="HandlingMode.BinaryRoundTrip"/> app type is a one-line
    /// change in <see cref="Build"/>.
    ///
    /// The registry intentionally lists every known
    /// <see cref="HandlingMode.ManualHandover"/> type (Apple VPP, Managed
    /// Google Play, etc.) so the import loop can route them to the manual
    /// hand-over CSV (Phase 4) instead of silently skipping them or — worse —
    /// trying to upload them. Cloneable types are inferred (anything not
    /// registered as binary or manual is assumed to be a metadata-only clone,
    /// which is how the legacy code already works).
    /// </summary>
    internal static class AppContentHandlerRegistry
    {
        private static readonly IReadOnlyDictionary<string, IAppContentHandler> Handlers;
        private static readonly IReadOnlyDictionary<string, ManualHandoverInfo> ManualHandovers;

        static AppContentHandlerRegistry()
        {
            Handlers = Build();
            ManualHandovers = BuildManualHandovers();
        }

        private static IReadOnlyDictionary<string, IAppContentHandler> Build()
        {
            var map = new Dictionary<string, IAppContentHandler>(StringComparer.OrdinalIgnoreCase);
            void Add(IAppContentHandler h) => map[h.ODataType] = h;

            // Phase 1
            Add(new Win32LobAppContentHandler());

            // Phase 2 / 3 handlers will register here:
            //   Add(new MacOSPkgAppContentHandler());
            //   Add(new MacOSDmgAppContentHandler());
            //   Add(new IosLobAppContentHandler());
            //   ...

            return map;
        }

        private static IReadOnlyDictionary<string, ManualHandoverInfo> BuildManualHandovers()
        {
            var map = new Dictionary<string, ManualHandoverInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["#microsoft.graph.iosVppApp"] = new(
                    "#microsoft.graph.iosVppApp",
                    "iOS VPP App",
                    "Re-purchase or transfer the licenses in the destination ABM/ASM, then sync the destination VPP token."),
                ["#microsoft.graph.macOsVppApp"] = new(
                    "#microsoft.graph.macOsVppApp",
                    "macOS VPP App",
                    "Re-purchase or transfer the licenses in the destination ABM/ASM, then sync the destination VPP token."),
                ["#microsoft.graph.androidManagedStoreApp"] = new(
                    "#microsoft.graph.androidManagedStoreApp",
                    "Android Managed Google Play App",
                    "Re-approve the app in the destination Managed Google Play account, then trigger a sync."),
                ["#microsoft.graph.androidForWorkApp"] = new(
                    "#microsoft.graph.androidForWorkApp",
                    "Android for Work App",
                    "Re-approve the app in the destination Managed Google Play account, then trigger a sync."),
                ["#microsoft.graph.managedIOSStoreApp"] = new(
                    "#microsoft.graph.managedIOSStoreApp",
                    "Managed iOS Store App",
                    "Re-add the App Store reference in the destination tenant."),
                ["#microsoft.graph.managedAndroidStoreApp"] = new(
                    "#microsoft.graph.managedAndroidStoreApp",
                    "Managed Android Store App",
                    "Re-add the Google Play reference in the destination tenant."),
                ["#microsoft.graph.winGetApp"] = new(
                    "#microsoft.graph.winGetApp",
                    "WinGet App",
                    "Re-add the WinGet manifest reference in the destination tenant."),
            };
            return map;
        }

        /// <summary>Returns the binary-upload handler for <paramref name="odataType"/>, or <c>null</c>.</summary>
        public static IAppContentHandler? GetHandler(string? odataType)
        {
            if (string.IsNullOrEmpty(odataType)) return null;
            return Handlers.TryGetValue(odataType, out var h) ? h : null;
        }

        /// <summary>Returns the manual-handover hint for <paramref name="odataType"/>, or <c>null</c>.</summary>
        public static ManualHandoverInfo? GetManualHandover(string? odataType)
        {
            if (string.IsNullOrEmpty(odataType)) return null;
            return ManualHandovers.TryGetValue(odataType, out var info) ? info : null;
        }

        /// <summary>
        /// Resolves the handling mode for an OData type. Defaults to
        /// <see cref="HandlingMode.Cloneable"/> for anything we don't
        /// explicitly know about — that's the behaviour the legacy
        /// reflection clone has always had.
        /// </summary>
        public static HandlingMode GetMode(string? odataType)
        {
            if (GetHandler(odataType) != null) return HandlingMode.BinaryRoundTrip;
            if (GetManualHandover(odataType) != null) return HandlingMode.ManualHandover;
            return HandlingMode.Cloneable;
        }

        /// <summary>All known binary-upload OData types (test-friendly view).</summary>
        public static IReadOnlyCollection<string> BinaryUploadODataTypes => (IReadOnlyCollection<string>)Handlers.Keys;

        /// <summary>All known tenant-bound OData types (test-friendly view).</summary>
        public static IReadOnlyCollection<string> ManualHandoverODataTypes => (IReadOnlyCollection<string>)ManualHandovers.Keys;
    }

    /// <summary>
    /// Per-type metadata for the Phase 4 "manual hand-over" CSV/XLSX export.
    /// </summary>
    internal sealed record ManualHandoverInfo(string ODataType, string DisplayLabel, string Hint);
}
