using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Per-app-type "shape" used by <see cref="IntuneContentEngine"/>.
    /// Adding a new binary-upload app type = adding one implementation of
    /// this interface and registering it in
    /// <see cref="AppContentHandlerRegistry"/>. The transport, polling,
    /// crypto, and progress reporting stay in the engine.
    /// </summary>
    internal interface IAppContentHandler
    {
        /// <summary>OData type, e.g. <c>#microsoft.graph.win32LobApp</c>.</summary>
        string ODataType { get; }

        /// <summary>How this app type can be moved between tenants.</summary>
        HandlingMode Mode { get; }

        /// <summary>Suggested file name for a freshly downloaded payload (e.g. <c>app.intunewin</c>).</summary>
        string DownloadFileName { get; }

        /// <summary>
        /// File-picker filter for the local-file import flow (Phase 1+ UI).
        /// Empty for app types that only support tenant-to-tenant.
        /// </summary>
        string LocalFileFilter { get; }

        /// <summary>
        /// Returns <c>true</c> when the local-file import flow needs the user
        /// to fill in extra metadata that can't be inferred from the file
        /// (e.g. PKG bundle ID/version, Win32 detection rule).
        /// </summary>
        bool RequiresUserMetadata { get; }

        /// <summary>
        /// Strips server-managed properties from a clone and returns a payload
        /// ready to POST to the destination tenant. The default
        /// implementation in <see cref="ApplicationCloneHelper"/> handles the
        /// common case (reflection clone + property strip); per-type
        /// overrides can layer additional fix-ups on top.
        /// </summary>
        MobileApp PrepareForClone(MobileApp source);

        /// <summary>
        /// Builds the per-type <see cref="MobileAppContentFile"/> metadata
        /// payload that initiates the upload. Most implementations only
        /// populate <c>Name</c>, <c>Size</c>, and <c>SizeEncrypted</c>; types
        /// like macOS PKG also set a manifest/bundle blob.
        /// </summary>
        MobileAppContentFile BuildContentFileMetadata(string fileName, long unencryptedSize, long encryptedSize);

        // --- Graph operations against the per-type request builders -----
        // Each app-type request builder lives at
        // MobileApps[id].GraphWin32LobApp / GraphMacOSPkgApp / etc. and
        // exposes ContentVersions + Files + Commit + RenewUpload. We funnel
        // those through the handler so the engine stays type-agnostic.

        Task<MobileAppContent?> CreateContentVersionAsync(GraphServiceClient client, string appId, CancellationToken cancellationToken);

        Task<MobileAppContentFile?> CreateContentFileAsync(GraphServiceClient client, string appId, string contentVersionId, MobileAppContentFile file, CancellationToken cancellationToken);

        Task<MobileAppContentFile?> GetContentFileAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, CancellationToken cancellationToken);

        Task RenewUploadAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, CancellationToken cancellationToken);

        Task CommitAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, FileEncryptionInfo encryptionInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Patches the destination app to point its <c>committedContentVersion</c>
        /// at the freshly uploaded version. This is the "go live" step; until
        /// this PATCH lands, the new content is invisible to the Intune UI.
        /// </summary>
        Task PatchCommittedContentVersionAsync(GraphServiceClient client, string appId, string contentVersionId, CancellationToken cancellationToken);

        /// <summary>
        /// Walks the source tenant to find the latest committed content
        /// version for <paramref name="appId"/> and returns its (single)
        /// content file. Returns <c>null</c> if the source app has no
        /// committed content (in which case there is nothing to copy).
        /// </summary>
        Task<SourceContentReference?> GetLatestCommittedContentAsync(GraphServiceClient client, string appId, string committedContentVersion, CancellationToken cancellationToken);
    }

    /// <summary>
    /// What the engine needs to know about the source content before it can
    /// download it: the file metadata (azureStorageUri + size + name) plus
    /// the IDs needed if we have to re-fetch the file (e.g. for SAS renewal).
    /// </summary>
    internal sealed record SourceContentReference(
        string ContentVersionId,
        string FileId,
        MobileAppContentFile File);
}
