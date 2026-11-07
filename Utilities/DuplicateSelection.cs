namespace InToolz.Utilities
{
    /// <summary>
    /// What counts as a duplicate on the Cleanup page, and which duplicates the "Select older" and
    /// "Select unassigned" buttons pre-select for deletion. The selections feed a permanent delete, so
    /// they are pulled out of the page to be tested, and the scan and both buttons use one definition
    /// of "the same item" instead of three hand-written copies of it (#116).
    /// </summary>
    public static class DuplicateSelection
    {
        /// <summary>Fewer items than this with the same key are not duplicates of anything.</summary>
        public const int MinimumGroupSize = 2;

        /// <summary>
        /// The identity of an item for duplicate detection: its name (ignoring case and surrounding
        /// whitespace), its exact content type, and its platform (ignoring surrounding whitespace).
        /// Items with equal keys are duplicates of each other.
        /// </summary>
        public readonly record struct DuplicateKey(string Name, string Type, string Platform);

        public static DuplicateKey KeyOf(string? name, string? type, string? platform) =>
            new(name?.Trim().ToUpperInvariant() ?? string.Empty,
                type ?? string.Empty,
                platform?.Trim() ?? string.Empty);

        /// <summary>
        /// "Select older": in every group of duplicates, everything except the newest. An item with no
        /// creation date counts as the oldest. If dates tie (or none are known) the item that comes first
        /// in <paramref name="items"/> is the one kept, so a group is never selected in full.
        /// </summary>
        public static List<DuplicateContentInfo> SelectOlder(IEnumerable<DuplicateContentInfo> items)
        {
            var selected = new List<DuplicateContentInfo>();
            foreach (var group in DuplicateGroups(items))
            {
                selected.AddRange(group
                    .OrderByDescending(i => i.CreatedDateTime ?? DateTimeOffset.MinValue)
                    .Skip(1));
            }
            return selected;
        }

        /// <summary>
        /// "Select unassigned": in every group of duplicates that has at least one assigned item, the
        /// items known to have no assignments. An item whose assignments could not be checked (null) is
        /// never selected, and a group with nothing assigned selects nothing, because there is no
        /// assigned item to keep.
        /// </summary>
        public static List<DuplicateContentInfo> SelectUnassigned(IEnumerable<DuplicateContentInfo> items)
        {
            var selected = new List<DuplicateContentInfo>();
            foreach (var group in DuplicateGroups(items).Where(g => g.Any(i => i.HasAssignments == true)))
            {
                selected.AddRange(group.Where(i => i.HasAssignments == false));
            }
            return selected;
        }

        private static IEnumerable<IGrouping<DuplicateKey, DuplicateContentInfo>> DuplicateGroups(IEnumerable<DuplicateContentInfo> items) =>
            items
                .GroupBy(i => KeyOf(i.ContentName, i.ContentType, i.ContentPlatform))
                .Where(g => g.Count() >= MinimumGroupSize);
    }
}
