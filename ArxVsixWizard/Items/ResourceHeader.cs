using System;

namespace ArxVsixWizard.Items
{
    /// <summary>
    /// Edits the Resource.h the item wizards generate into the user's project.
    /// </summary>
    static class ResourceHeader
    {
        /// <summary>
        /// The comment Visual Studio's resource editor writes above the bookkeeping block it owns.
        /// New resource ids belong above that block: everything from here down is the editor's own
        /// _APS_NEXT_* bookkeeping, so a #define appended after it sits outside the region the editor
        /// manages and it will not be picked up when the dialog is opened there.
        /// </summary>
        const string BookkeepingMarker = "// Next default values for new objects";

        const string LegacyMarker = "#ifdef APSTUDIO_INVOKED";

        /// <summary>
        /// Inserts <paramref name="definition"/> - a complete "#define ..." line, no newline - above
        /// the bookkeeping block, keeping a single blank line between it and the definitions that were
        /// already there. Falls back to appending when the file has no such block.
        /// </summary>
        internal static string InsertDefine(string text, string definition)
        {
            int at = text.IndexOf(BookkeepingMarker, StringComparison.Ordinal);
            if (at < 0) at = text.IndexOf(LegacyMarker, StringComparison.Ordinal);
            if (at < 0) return text.TrimEnd() + "\r\n" + definition + "\r\n";

            return text.Substring(0, at).TrimEnd() + "\r\n" + definition + "\r\n\r\n" + text.Substring(at);
        }
    }
}
