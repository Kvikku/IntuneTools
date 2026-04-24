using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.GraphWin32LobApp.ContentVersions.Item.Files.Item.Commit;
using Microsoft.Graph.Beta.Models;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// <see cref="IAppContentHandler"/> for <c>#microsoft.graph.win32LobApp</c>.
    /// Routes every Graph call through the SDK's <c>GraphWin32LobApp</c>
    /// request builder; the engine handles transport, polling, and crypto.
    ///
    /// The reflection clone in <see cref="ApplicationCloneHelper.Clone"/>
    /// already round-trips Win32-specific bits (<c>detectionRules</c>,
    /// <c>requirementRules</c>, <c>rules</c>, <c>returnCodes</c>,
    /// <c>installExperience</c>, <c>msiInformation</c>,
    /// <c>minimumSupportedWindowsRelease</c>, <c>setupFilePath</c>,
    /// install/uninstall command lines, allowed architectures, etc.) because
    /// they are public writable properties on <see cref="Win32LobApp"/>.
    /// PowerShell detection script content stays base64-encoded as Graph
    /// returns it.
    /// </summary>
    internal sealed class Win32LobAppContentHandler : IAppContentHandler
    {
        public string ODataType => "#microsoft.graph.win32LobApp";
        public HandlingMode Mode => HandlingMode.BinaryRoundTrip;
        public string DownloadFileName => "app.intunewin";
        public string LocalFileFilter => ".intunewin";
        public bool RequiresUserMetadata => true; // detection rule, install commands, etc. — for the Phase 1 follow-up local-file UI.

        public MobileApp PrepareForClone(MobileApp source) => ApplicationCloneHelper.Clone(source);

        public MobileAppContentFile BuildContentFileMetadata(string fileName, long unencryptedSize, long encryptedSize)
        {
            return new MobileAppContentFile
            {
                OdataType = "#microsoft.graph.mobileAppContentFile",
                Name = fileName,
                Size = unencryptedSize,
                SizeEncrypted = encryptedSize,
                IsDependency = false,
            };
        }

        public Task<MobileAppContent?> CreateContentVersionAsync(GraphServiceClient client, string appId, CancellationToken cancellationToken)
        {
            return client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions
                .PostAsync(new MobileAppContent { OdataType = "#microsoft.graph.mobileAppContent" }, cancellationToken: cancellationToken);
        }

        public Task<MobileAppContentFile?> CreateContentFileAsync(GraphServiceClient client, string appId, string contentVersionId, MobileAppContentFile file, CancellationToken cancellationToken)
        {
            return client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[contentVersionId]
                .Files.PostAsync(file, cancellationToken: cancellationToken);
        }

        public Task<MobileAppContentFile?> GetContentFileAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, CancellationToken cancellationToken)
        {
            return client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[contentVersionId]
                .Files[fileId]
                .GetAsync(cancellationToken: cancellationToken);
        }

        public Task RenewUploadAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, CancellationToken cancellationToken)
        {
            return client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[contentVersionId]
                .Files[fileId]
                .RenewUpload.PostAsync(cancellationToken: cancellationToken);
        }

        public Task CommitAsync(GraphServiceClient client, string appId, string contentVersionId, string fileId, FileEncryptionInfo encryptionInfo, CancellationToken cancellationToken)
        {
            var body = new CommitPostRequestBody { FileEncryptionInfo = encryptionInfo };
            return client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[contentVersionId]
                .Files[fileId]
                .Commit.PostAsync(body, cancellationToken: cancellationToken);
        }

        public Task PatchCommittedContentVersionAsync(GraphServiceClient client, string appId, string contentVersionId, CancellationToken cancellationToken)
        {
            // Use a Win32LobApp body so the OData type lines up with the
            // destination (the MobileApps[id] PATCH route accepts derived
            // types).
            var patch = new Win32LobApp
            {
                OdataType = ODataType,
                CommittedContentVersion = contentVersionId,
            };
            return client.DeviceAppManagement.MobileApps[appId].PatchAsync(patch, cancellationToken: cancellationToken);
        }

        public async Task<SourceContentReference?> GetLatestCommittedContentAsync(GraphServiceClient client, string appId, string committedContentVersion, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(committedContentVersion)) return null;

            var files = await client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[committedContentVersion]
                .Files.GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Win32 LOB apps have exactly one content file per content version;
            // pick the first that's actually committed and has a downloadable
            // SAS URL. (Defensive: the SDK returns a list here.)
            var file = files?.Value?.FirstOrDefault(f => f.IsCommitted == true)
                       ?? files?.Value?.FirstOrDefault();
            if (file == null || string.IsNullOrEmpty(file.Id)) return null;

            // Re-fetch the single file resource so we get the per-file
            // fileEncryptionInfo block. Graph only includes encryption
            // material on the item GET, not the collection GET. The Beta
            // SDK doesn't surface fileEncryptionInfo as a typed property,
            // so it lands in AdditionalData and we extract it manually.
            var fileWithEncryption = await client.DeviceAppManagement.MobileApps[appId]
                .GraphWin32LobApp.ContentVersions[committedContentVersion]
                .Files[file.Id]
                .GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                ?? file;

            var encryptionInfo = ExtractFileEncryptionInfo(fileWithEncryption);

            return new SourceContentReference(committedContentVersion, file.Id, fileWithEncryption, encryptionInfo);
        }

        /// <summary>
        /// Pulls <c>fileEncryptionInfo</c> out of <see cref="MobileAppContentFile.AdditionalData"/>.
        /// The Beta SDK shape for <c>mobileAppContentFile</c> doesn't include
        /// <c>fileEncryptionInfo</c> as a typed property, but Graph does
        /// return it on the item GET (Kiota deposits it as a parsed JSON
        /// object in <see cref="MobileAppContentFile.AdditionalData"/>).
        /// Returns <c>null</c> if the field is missing or shaped differently
        /// than expected; callers must treat <c>null</c> as a fatal error
        /// (you cannot decrypt source content without the keys).
        /// </summary>
        internal static FileEncryptionInfo? ExtractFileEncryptionInfo(MobileAppContentFile file)
        {
            if (file.AdditionalData == null) return null;
            if (!file.AdditionalData.TryGetValue("fileEncryptionInfo", out var raw) || raw == null) return null;

            // Most likely shape under System.Text.Json-backed Kiota: a JsonElement.
            if (raw is JsonElement el && el.ValueKind == JsonValueKind.Object)
            {
                return ParseFromJsonElement(el);
            }
            // Fallback shape: nested IDictionary<string, object> with already-decoded values.
            if (raw is IDictionary<string, object?> dict)
            {
                return ParseFromDictionary(dict);
            }
            return null;
        }

        private static FileEncryptionInfo ParseFromJsonElement(JsonElement el)
        {
            var info = new FileEncryptionInfo
            {
                OdataType = "#microsoft.graph.fileEncryptionInfo",
            };
            if (el.TryGetProperty("encryptionKey", out var k) && k.ValueKind == JsonValueKind.String) info.EncryptionKey = Convert.FromBase64String(k.GetString()!);
            if (el.TryGetProperty("macKey", out var mk) && mk.ValueKind == JsonValueKind.String) info.MacKey = Convert.FromBase64String(mk.GetString()!);
            if (el.TryGetProperty("initializationVector", out var iv) && iv.ValueKind == JsonValueKind.String) info.InitializationVector = Convert.FromBase64String(iv.GetString()!);
            if (el.TryGetProperty("mac", out var mac) && mac.ValueKind == JsonValueKind.String) info.Mac = Convert.FromBase64String(mac.GetString()!);
            if (el.TryGetProperty("fileDigest", out var fd) && fd.ValueKind == JsonValueKind.String) info.FileDigest = Convert.FromBase64String(fd.GetString()!);
            if (el.TryGetProperty("fileDigestAlgorithm", out var fda) && fda.ValueKind == JsonValueKind.String) info.FileDigestAlgorithm = fda.GetString();
            if (el.TryGetProperty("profileIdentifier", out var pi) && pi.ValueKind == JsonValueKind.String) info.ProfileIdentifier = pi.GetString();
            return info;
        }

        private static FileEncryptionInfo ParseFromDictionary(IDictionary<string, object?> d)
        {
            var info = new FileEncryptionInfo
            {
                OdataType = "#microsoft.graph.fileEncryptionInfo",
            };
            byte[]? AsBytes(string key) => d.TryGetValue(key, out var v) && v is string s ? Convert.FromBase64String(s) : null;
            string? AsStr(string key) => d.TryGetValue(key, out var v) ? v as string : null;
            info.EncryptionKey = AsBytes("encryptionKey");
            info.MacKey = AsBytes("macKey");
            info.InitializationVector = AsBytes("initializationVector");
            info.Mac = AsBytes("mac");
            info.FileDigest = AsBytes("fileDigest");
            info.FileDigestAlgorithm = AsStr("fileDigestAlgorithm");
            info.ProfileIdentifier = AsStr("profileIdentifier");
            return info;
        }
    }
}
