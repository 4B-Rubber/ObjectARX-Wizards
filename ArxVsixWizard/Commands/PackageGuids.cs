// Shared GUID/ID constants for the package and its command set. The VSCT references the
// same values symbolically, so keep the two files in step when one changes.
using System;

namespace ArxVsixWizard.Commands
{
    internal static class PackageGuids
    {
        public const string PackageGuidString = "9E4F2C71-3B5A-4D68-8F1C-2A7B6D0E5F93";
        public const string CmdSetGuidString = "A1B7C4D2-6E8F-4A3B-9C5D-1E2F8A7B4C60";

        public static readonly Guid CmdSetGuid = new Guid(CmdSetGuidString);
    }

    internal static class PackageIds
    {
        public const int AddArxItemCommandId = 0x0100;
    }
}
