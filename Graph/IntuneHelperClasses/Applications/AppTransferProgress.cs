namespace IntuneTools.Graph.IntuneHelperClasses.Applications
{
    /// <summary>
    /// Phase the engine is currently in for a single application transfer.
    /// Surfaced via <see cref="System.IProgress{T}"/> so the UI can render a
    /// useful status string without knowing about the engine internals.
    /// </summary>
    public enum AppTransferPhase
    {
        FetchingMetadata,
        Downloading,
        CreatingDestinationApp,
        CreatingContentVersion,
        WaitingForUploadUri,
        Uploading,
        Committing,
        WaitingForCommit,
        Finalizing,
        Done,
    }

    /// <summary>
    /// Progress payload reported from <see cref="IntuneContentEngine"/> during
    /// an application transfer. <see cref="BytesProcessed"/> /
    /// <see cref="BytesTotal"/> are populated for the download and upload
    /// phases; for the others they may be zero.
    /// </summary>
    public sealed record AppTransferProgress(
        AppTransferPhase Phase,
        string AppDisplayName,
        long BytesProcessed = 0,
        long BytesTotal = 0,
        string? Message = null);
}
