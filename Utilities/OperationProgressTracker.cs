namespace IntuneTools.Utilities
{
    /// <summary>
    /// Tracks progress for bulk operations (import, assign, rename, delete, etc.).
    /// Replaces the duplicated _*Total/_*Current/_*SuccessCount/_*ErrorCount fields
    /// found in ImportPage, AssignmentPage, RenamingPage, CleanupPage, and ManageAssignmentsPage.
    /// </summary>
    public class OperationProgressTracker
    {
        /// <summary>Total number of items in the operation.</summary>
        public int Total { get; private set; }

        /// <summary>Current item number (0-based, incremented before processing).</summary>
        public int Current { get; private set; }

        /// <summary>Number of items completed successfully.</summary>
        public int SuccessCount { get; private set; }

        /// <summary>Number of items that failed.</summary>
        public int ErrorCount { get; private set; }

        /// <summary>
        /// Resets all counters and sets the total for a new operation.
        /// </summary>
        public void Reset(int total)
        {
            Total = total;
            Current = 0;
            SuccessCount = 0;
            ErrorCount = 0;
        }

        /// <summary>
        /// Advances to the next item.
        /// </summary>
        public void Advance() => Current++;

        /// <summary>
        /// Records a successful item.
        /// </summary>
        public void RecordSuccess() => SuccessCount++;

        /// <summary>
        /// Records a failed item.
        /// </summary>
        public void RecordError() => ErrorCount++;

        /// <summary>
        /// Returns a summary string like "3 succeeded, 1 failed out of 4".
        /// </summary>
        public string GetSummary()
        {
            return $"{SuccessCount} succeeded, {ErrorCount} failed out of {Total}";
        }
    }
}
