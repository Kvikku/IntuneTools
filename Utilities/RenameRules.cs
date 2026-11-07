namespace InToolz.Utilities
{
    /// <summary>
    /// The rules for the Renaming page that don't need a page to answer: which mode a ComboBox
    /// item stands for, and whether the inputs are enough to start. Kept out of code-behind so
    /// there is one copy of each, and so the rules can be tested (#116).
    /// </summary>
    public static class RenameRules
    {
        /// <summary>
        /// Reads a rename mode from a ComboBoxItem's Tag. Only an exact enum member name counts
        /// ("Prefix", "FindAndReplace", ...); anything else, including null, an unknown name and a
        /// number, is not a mode. That is deliberate: the mode decides what gets written to every
        /// selected item in the tenant, so an unrecognised selection has to stop the rename rather
        /// than quietly become a default.
        /// </summary>
        public static bool TryParseMode(string? tag, out Variables.RenameMode mode)
        {
            foreach (var candidate in Enum.GetValues<Variables.RenameMode>())
            {
                if (string.Equals(tag, candidate.ToString(), StringComparison.Ordinal))
                {
                    mode = candidate;
                    return true;
                }
            }

            mode = default;
            return false;
        }

        /// <summary>
        /// Whether the mode needs text in the name box. Remove Prefix does not (blank means
        /// "remove a bracket-style prefix automatically") and Remove Description takes no input.
        /// Every other mode, Remove Suffix included, has nothing to do without it.
        /// </summary>
        public static bool RequiresNewName(Variables.RenameMode mode)
        {
            return mode != Variables.RenameMode.RemovePrefix
                && mode != Variables.RenameMode.RemoveDescription;
        }

        /// <summary>
        /// Checks whether a rename can start. Returns what to tell the user, or null when the
        /// inputs are fine.
        /// </summary>
        /// <param name="mode">The chosen rename mode.</param>
        /// <param name="newName">The text in the name box (prefix, suffix, find text, description...).</param>
        /// <param name="prefixSymbolSelected">Whether a bracket style ( ) [ ] { } is chosen; only Prefix mode needs one.</param>
        public static string? Validate(Variables.RenameMode mode, string? newName, bool prefixSymbolSelected)
        {
            if (RequiresNewName(mode) && string.IsNullOrWhiteSpace(newName))
            {
                return mode == Variables.RenameMode.FindAndReplace
                    ? "Please enter the text to find."
                    : "Please enter a new name.";
            }

            if (mode == Variables.RenameMode.Prefix && !prefixSymbolSelected)
            {
                return "Please select a prefix option.";
            }

            return null;
        }
    }
}
