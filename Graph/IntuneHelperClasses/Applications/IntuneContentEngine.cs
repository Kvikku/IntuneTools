using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Transport-only engine shared by every binary-upload app type. Knows
    /// nothing about Win32 vs PKG vs DMG specifics — that lives in the
    /// per-type <see cref="IAppContentHandler"/>. Knows everything about:
    ///   * Streaming download from the source's Azure Storage SAS URL.
    ///   * AES-256-CBC + HMAC-SHA256 decrypt + re-encrypt without buffering
    ///     multi-GB payloads in memory (see <see cref="IntuneAppContentCrypto"/>).
    ///   * Polling <c>azureStorageUriRequest</c> and <c>commit</c> state
    ///     transitions with exponential back-off and a configurable timeout.
    ///   * Mid-flight SAS renewal via <c>renewUpload</c> when an upload
    ///     outlasts the original SAS expiration.
    ///   * Block-blob chunked upload to Azure Storage (the one leg the Graph
    ///     SDK does not model — it's not Graph, it's blob storage with a SAS).
    /// </summary>
    internal sealed class IntuneContentEngine
    {
        private static readonly HttpClient SharedAzureBlobClient = CreateAzureBlobClient();

        private readonly ContentTransferOptions _options;
        private readonly HttpClient _azureBlobClient;

        public IntuneContentEngine(ContentTransferOptions? options = null, HttpClient? azureBlobClient = null)
        {
            _options = options ?? new ContentTransferOptions();
            _azureBlobClient = azureBlobClient ?? SharedAzureBlobClient;
        }

        private static HttpClient CreateAzureBlobClient()
        {
            // We only talk to Azure Blob Storage with this client — never
            // Graph — so it doesn't carry any auth handlers. Long timeout
            // because individual chunk PUTs can be slow on bad links.
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
            })
            {
                Timeout = TimeSpan.FromMinutes(10),
            };
        }

        /// <summary>
        /// End-to-end binary clone: source app + committed content version →
        /// destination app + committed content version. Throws on any
        /// non-recoverable error so the caller can mark this app as failed
        /// without affecting the rest of the batch.
        /// </summary>
        public async Task<MobileApp> CloneApplicationAsync(
            GraphServiceClient sourceClient,
            GraphServiceClient destinationClient,
            MobileApp sourceApp,
            IAppContentHandler handler,
            IProgress<AppTransferProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sourceApp == null) throw new ArgumentNullException(nameof(sourceApp));
            if (string.IsNullOrEmpty(sourceApp.Id)) throw new ArgumentException("Source app has no Id.", nameof(sourceApp));

            var displayName = sourceApp.DisplayName ?? sourceApp.Id;
            Report(progress, AppTransferPhase.FetchingMetadata, displayName);

            var sourceContent = await handler.GetLatestCommittedContentAsync(
                sourceClient,
                sourceApp.Id,
                sourceApp.CommittedContentVersion ?? string.Empty,
                cancellationToken).ConfigureAwait(false);

            if (sourceContent == null)
            {
                throw new InvalidOperationException(
                    $"Source application '{displayName}' has no committed content to download.");
            }

            var sourceFile = sourceContent.File;
            if (string.IsNullOrEmpty(sourceFile.AzureStorageUri))
            {
                throw new InvalidOperationException(
                    $"Source content file for '{displayName}' has no azureStorageUri (cannot be downloaded).");
            }

            // Stage to disk so we never hold a multi-GB payload in memory.
            var stagingPath = Path.Combine(
                _options.TempDirectory,
                $"intunetools-{Guid.NewGuid():N}-{handler.DownloadFileName}");

            try
            {
                Report(progress, AppTransferPhase.Downloading, displayName, 0, sourceFile.Size ?? 0);
                await using (var stagingFile = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await DownloadFromAzureBlobAsync(
                        sourceFile.AzureStorageUri!,
                        stagingFile,
                        sourceFile.Size ?? 0,
                        bytes => Report(progress, AppTransferPhase.Downloading, displayName, bytes, sourceFile.Size ?? 0),
                        cancellationToken).ConfigureAwait(false);
                }

                // POST the metadata to the destination tenant first: we need an
                // app id before we can request a content version.
                Report(progress, AppTransferPhase.CreatingDestinationApp, displayName);
                var clone = handler.PrepareForClone(sourceApp);
                var imported = await destinationClient.DeviceAppManagement.MobileApps.PostAsync(clone, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (imported == null || string.IsNullOrEmpty(imported.Id))
                {
                    throw new InvalidOperationException($"Destination tenant accepted the POST for '{displayName}' but returned no Id.");
                }

                await UploadContentForExistingAppAsync(
                    destinationClient,
                    handler,
                    imported.Id,
                    displayName,
                    stagingPath,
                    sourceFile.Name ?? handler.DownloadFileName,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                Report(progress, AppTransferPhase.Done, displayName);
                return imported;
            }
            finally
            {
                TryDeleteStagingFile(stagingPath);
            }
        }

        /// <summary>
        /// Uploads <paramref name="stagingPath"/> as the next committed
        /// content version of <paramref name="appId"/>. Reusable on its own
        /// for the local-file import flow (Phase 1 follow-up): the caller
        /// POSTs the metadata themselves, then hands the new app id +
        /// staged file to this method.
        /// </summary>
        public async Task UploadContentForExistingAppAsync(
            GraphServiceClient destinationClient,
            IAppContentHandler handler,
            string appId,
            string displayName,
            string stagingPath,
            string contentFileName,
            IProgress<AppTransferProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // 1. Create the content version (returns an Id we hang the file off).
            Report(progress, AppTransferPhase.CreatingContentVersion, displayName);
            var version = await handler.CreateContentVersionAsync(destinationClient, appId, cancellationToken).ConfigureAwait(false);
            if (version == null || string.IsNullOrEmpty(version.Id))
            {
                throw new InvalidOperationException("Failed to create destination content version.");
            }

            // 2. Encrypt the staged plaintext into a sibling temp file so we
            //    can report a stable encryptedSize to Graph and stream the
            //    ciphertext to Azure Blob Storage.
            var encryptedPath = stagingPath + ".enc";
            var material = IntuneAppContentCrypto.CreateMaterial();
            EncryptionResult encResult;
            long plaintextSize;

            try
            {
                await using (var src = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var dst = new FileStream(encryptedPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    plaintextSize = src.Length;
                    encResult = await IntuneAppContentCrypto.EncryptStreamAsync(
                        src, dst, material, _options.StreamBufferBytes, cancellationToken).ConfigureAwait(false);
                }

                // 3. Tell Graph we want to upload n bytes; it provisions an Azure
                //    Storage SAS URL.
                var contentFileMeta = handler.BuildContentFileMetadata(contentFileName, plaintextSize, encResult.EncryptedSize);
                var contentFile = await handler.CreateContentFileAsync(destinationClient, appId, version.Id, contentFileMeta, cancellationToken).ConfigureAwait(false);
                if (contentFile == null || string.IsNullOrEmpty(contentFile.Id))
                {
                    throw new InvalidOperationException("Failed to create destination content file metadata.");
                }

                // 4. Wait for AzureStorageUriRequestSuccess.
                Report(progress, AppTransferPhase.WaitingForUploadUri, displayName);
                contentFile = await WaitForUploadStateAsync(
                    destinationClient,
                    handler,
                    appId,
                    version.Id,
                    contentFile.Id,
                    MobileAppContentFileUploadState.AzureStorageUriRequestSuccess,
                    new[] { MobileAppContentFileUploadState.AzureStorageUriRequestFailed, MobileAppContentFileUploadState.AzureStorageUriRequestTimedOut },
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(contentFile.AzureStorageUri))
                {
                    throw new InvalidOperationException("Graph reported AzureStorageUriRequestSuccess but no azureStorageUri was returned.");
                }

                // 5. Stream-upload the encrypted file as Azure block-blob chunks.
                Report(progress, AppTransferPhase.Uploading, displayName, 0, encResult.EncryptedSize);
                await UploadToAzureBlobAsync(
                    destinationClient,
                    handler,
                    appId,
                    version.Id,
                    contentFile,
                    encryptedPath,
                    encResult.EncryptedSize,
                    bytes => Report(progress, AppTransferPhase.Uploading, displayName, bytes, encResult.EncryptedSize),
                    cancellationToken).ConfigureAwait(false);

                // 6. Commit with the FileEncryptionInfo so Intune can decrypt
                //    on the device.
                Report(progress, AppTransferPhase.Committing, displayName);
                var encryptionInfo = new FileEncryptionInfo
                {
                    OdataType = "#microsoft.graph.fileEncryptionInfo",
                    EncryptionKey = material.EncryptionKey,
                    MacKey = material.HmacKey,
                    InitializationVector = material.InitializationVector,
                    Mac = await ReadMacFromHeaderAsync(encryptedPath, cancellationToken).ConfigureAwait(false),
                    FileDigest = encResult.FileDigest,
                    FileDigestAlgorithm = "SHA256",
                    ProfileIdentifier = "ProfileVersion1",
                };
                await handler.CommitAsync(destinationClient, appId, version.Id, contentFile.Id, encryptionInfo, cancellationToken).ConfigureAwait(false);

                // 7. Wait for CommitFileSuccess.
                Report(progress, AppTransferPhase.WaitingForCommit, displayName);
                await WaitForUploadStateAsync(
                    destinationClient,
                    handler,
                    appId,
                    version.Id,
                    contentFile.Id,
                    MobileAppContentFileUploadState.CommitFileSuccess,
                    new[] { MobileAppContentFileUploadState.CommitFileFailed, MobileAppContentFileUploadState.CommitFileTimedOut },
                    cancellationToken).ConfigureAwait(false);

                // 8. Flip the destination app's CommittedContentVersion to
                //    point at the freshly uploaded version. Until this PATCH
                //    lands, the new content is invisible in the Intune UI.
                Report(progress, AppTransferPhase.Finalizing, displayName);
                await handler.PatchCommittedContentVersionAsync(destinationClient, appId, version.Id, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteStagingFile(encryptedPath);
            }
        }

        // -----------------------------------------------------------------
        //  Polling (azureStorageUriRequest + commit state machines)
        // -----------------------------------------------------------------

        private async Task<MobileAppContentFile> WaitForUploadStateAsync(
            GraphServiceClient client,
            IAppContentHandler handler,
            string appId,
            string contentVersionId,
            string fileId,
            MobileAppContentFileUploadState successState,
            IReadOnlyCollection<MobileAppContentFileUploadState> failureStates,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + _options.PollTimeout;
            var delay = _options.PollInitialDelay;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var file = await handler.GetContentFileAsync(client, appId, contentVersionId, fileId, cancellationToken).ConfigureAwait(false);
                if (file == null) throw new InvalidOperationException("Graph returned null for the content file during polling.");

                if (file.UploadState == successState)
                {
                    return file;
                }
                if (file.UploadState.HasValue && failureStates.Contains(file.UploadState.Value))
                {
                    throw new InvalidOperationException($"Intune reported a terminal failure state '{file.UploadState}' while waiting for '{successState}'.");
                }

                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Timed out after {_options.PollTimeout} waiting for upload state '{successState}' (last observed: '{file.UploadState}').");
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                // Exponential back-off, capped.
                var nextMs = Math.Min(_options.PollMaxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2);
                delay = TimeSpan.FromMilliseconds(nextMs);
            }
        }

        // -----------------------------------------------------------------
        //  Azure Blob Storage transport (the one leg the SDK doesn't model)
        // -----------------------------------------------------------------

        private async Task DownloadFromAzureBlobAsync(
            string sasUri,
            Stream destination,
            long expectedSize,
            Action<long> onProgress,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, sasUri);
            request.Headers.TryAddWithoutValidation("x-ms-version", "2019-07-07");

            using var response = await _azureBlobClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var src = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[_options.StreamBufferBytes];
            long total = 0;
            int read;
            while ((read = await src.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                total += read;
                onProgress(total);
            }
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (expectedSize > 0 && total != expectedSize)
            {
                throw new InvalidOperationException($"Downloaded {total} bytes from source blob but expected {expectedSize}.");
            }
        }

        private async Task UploadToAzureBlobAsync(
            GraphServiceClient destinationClient,
            IAppContentHandler handler,
            string appId,
            string contentVersionId,
            MobileAppContentFile contentFile,
            string encryptedPath,
            long encryptedSize,
            Action<long> onProgress,
            CancellationToken cancellationToken)
        {
            var sasUri = contentFile.AzureStorageUri!;
            var sasExpiry = contentFile.AzureStorageUriExpirationDateTime?.UtcDateTime;
            var blockIds = new List<string>();

            await using var src = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[_options.UploadChunkSizeBytes];
            long offset = 0;
            int blockIndex = 0;

            while (offset < encryptedSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytesToRead = (int)Math.Min(buffer.Length, encryptedSize - offset);
                var totalReadIntoBuffer = 0;
                while (totalReadIntoBuffer < bytesToRead)
                {
                    var n = await src.ReadAsync(buffer.AsMemory(totalReadIntoBuffer, bytesToRead - totalReadIntoBuffer), cancellationToken).ConfigureAwait(false);
                    if (n == 0) throw new EndOfStreamException("Encrypted staging file ended early during upload.");
                    totalReadIntoBuffer += n;
                }

                // Renew the SAS if it's about to expire (or already has).
                if (sasExpiry.HasValue && sasExpiry.Value - DateTime.UtcNow <= _options.SasRenewalThreshold)
                {
                    contentFile = await RenewSasAsync(destinationClient, handler, appId, contentVersionId, contentFile.Id!, cancellationToken).ConfigureAwait(false);
                    sasUri = contentFile.AzureStorageUri!;
                    sasExpiry = contentFile.AzureStorageUriExpirationDateTime?.UtcDateTime;
                }

                var blockId = MakeBlockId(blockIndex);
                blockIds.Add(blockId);

                await PutBlockAsync(sasUri, blockId, buffer, totalReadIntoBuffer, cancellationToken).ConfigureAwait(false);

                offset += totalReadIntoBuffer;
                blockIndex++;
                onProgress(offset);
            }

            await PutBlockListAsync(sasUri, blockIds, cancellationToken).ConfigureAwait(false);
        }

        private async Task<MobileAppContentFile> RenewSasAsync(
            GraphServiceClient client,
            IAppContentHandler handler,
            string appId,
            string contentVersionId,
            string fileId,
            CancellationToken cancellationToken)
        {
            await handler.RenewUploadAsync(client, appId, contentVersionId, fileId, cancellationToken).ConfigureAwait(false);

            var refreshed = await WaitForUploadStateAsync(
                client,
                handler,
                appId,
                contentVersionId,
                fileId,
                MobileAppContentFileUploadState.AzureStorageUriRenewalSuccess,
                new[] { MobileAppContentFileUploadState.AzureStorageUriRenewalFailed, MobileAppContentFileUploadState.AzureStorageUriRenewalTimedOut },
                cancellationToken).ConfigureAwait(false);

            return refreshed;
        }

        private async Task PutBlockAsync(string sasUri, string blockId, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var blockUri = AppendQuery(sasUri, $"comp=block&blockid={Uri.EscapeDataString(blockId)}");

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Put, blockUri);
                    request.Headers.TryAddWithoutValidation("x-ms-version", "2019-07-07");
                    request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
                    request.Content = new ByteArrayContent(buffer, 0, count);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    using var response = await _azureBlobClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return;
                }
                catch (Exception) when (attempt < _options.MaxChunkRetries)
                {
                    var delayMs = (int)Math.Min(_options.PollMaxDelay.TotalMilliseconds, _options.PollInitialDelay.TotalMilliseconds * Math.Pow(2, attempt));
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task PutBlockListAsync(string sasUri, IReadOnlyList<string> blockIds, CancellationToken cancellationToken)
        {
            var listUri = AppendQuery(sasUri, "comp=blocklist");

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>");
            foreach (var id in blockIds)
            {
                // Block IDs are Base64 strings (a-z, A-Z, 0-9, +, /, =). None
                // of those characters require XML escaping in element text.
                sb.Append("<Latest>").Append(id).Append("</Latest>");
            }
            sb.Append("</BlockList>");

            using var request = new HttpRequestMessage(HttpMethod.Put, listUri);
            request.Headers.TryAddWithoutValidation("x-ms-version", "2019-07-07");
            request.Headers.TryAddWithoutValidation("x-ms-blob-content-type", "application/octet-stream");
            request.Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/xml");

            using var response = await _azureBlobClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private static string MakeBlockId(int index)
        {
            // Azure block IDs must be Base64 strings, all of the same length
            // within a single blob. 16 bytes → 24 base64 chars covers ~10^39
            // blocks; in practice we'll never exceed Azure's 50,000 block cap.
            var raw = new byte[16];
            BitConverter.GetBytes(index).CopyTo(raw, 0);
            return Convert.ToBase64String(raw);
        }

        private static string AppendQuery(string uri, string extra)
        {
            return uri.Contains('?', StringComparison.Ordinal)
                ? uri + "&" + extra
                : uri + "?" + extra;
        }

        // -----------------------------------------------------------------
        //  Misc
        // -----------------------------------------------------------------

        private static async Task<byte[]> ReadMacFromHeaderAsync(string encryptedPath, CancellationToken cancellationToken)
        {
            // MAC lives in the first 32 bytes of the encrypted payload. Pull
            // it back out so we can hand it to Graph in FileEncryptionInfo.Mac
            // (the device side recomputes it after download to detect
            // tampering).
            await using var fs = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buf = new byte[IntuneAppContentCrypto.HmacSize];
            int read = 0;
            while (read < buf.Length)
            {
                var n = await fs.ReadAsync(buf.AsMemory(read), cancellationToken).ConfigureAwait(false);
                if (n == 0) throw new EndOfStreamException("Encrypted staging file is shorter than the MAC header.");
                read += n;
            }
            return buf;
        }

        private static void Report(IProgress<AppTransferProgress>? progress, AppTransferPhase phase, string name, long bytes = 0, long total = 0)
        {
            progress?.Report(new AppTransferProgress(phase, name, bytes, total));
        }

        private static void TryDeleteStagingFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup; the OS temp sweeper will get it */ }
        }
    }
}
