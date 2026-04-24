using System;
using System.IO;

namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Tunables for <see cref="IntuneContentEngine"/>. Defaults are sensible
    /// for any LOB app type; expose this from <c>Settings</c> later if users
    /// need to tweak chunk size, polling timeouts, or the temp directory.
    /// </summary>
    public sealed class ContentTransferOptions
    {
        /// <summary>
        /// Block-blob upload chunk size. The Azure block-blob max is 100 MiB,
        /// but Intune's documentation recommends 4 MiB chunks. Smaller chunks
        /// recover faster from transient errors; larger chunks reduce per-chunk
        /// HTTP overhead. Keep ≤ 4 MiB to match what the Intune Content Prep
        /// Tool ships with.
        /// </summary>
        public int UploadChunkSizeBytes { get; init; } = 4 * 1024 * 1024;

        /// <summary>
        /// Read buffer used while streaming blobs to and from disk. Independent
        /// from <see cref="UploadChunkSizeBytes"/> so we never load a multi-GB
        /// payload into memory.
        /// </summary>
        public int StreamBufferBytes { get; init; } = 256 * 1024;

        /// <summary>
        /// Maximum time to wait for the Graph service to finish provisioning an
        /// Azure Storage upload URI or to commit a content file. Large files
        /// can spend several minutes in <c>commitFilePending</c>.
        /// </summary>
        public TimeSpan PollTimeout { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>Initial back-off delay between Graph state polls.</summary>
        public TimeSpan PollInitialDelay { get; init; } = TimeSpan.FromSeconds(2);

        /// <summary>Maximum back-off delay between Graph state polls.</summary>
        public TimeSpan PollMaxDelay { get; init; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// How long before the Azure Storage SAS URL expires we should request
        /// a fresh one via <c>renewUpload</c>. The default leaves a comfortable
        /// margin for slow connections.
        /// </summary>
        public TimeSpan SasRenewalThreshold { get; init; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Temp directory for the on-disk download/encrypt staging file.
        /// Defaults to <see cref="Path.GetTempPath"/>.
        /// </summary>
        public string TempDirectory { get; init; } = Path.GetTempPath();

        /// <summary>
        /// Number of retry attempts for transient HTTP failures (per chunk),
        /// after the initial PUT attempt. For example, a value of 4 allows up
        /// to 5 total attempts for a chunk upload.
        /// </summary>
        public int MaxChunkRetries { get; init; } = 4;
    }
}
