// Project level side effects shared by the two ATL item wizards (COM wrapper and
// dynamic property). The old HTML/JS wizards used the VS CodeModel to edit the
// project: they inserted the rendered coclass/interface fragments into the main
// .idl and registered the generated .rgs file as a REGISTRY resource in the .rc
// file (ArxAtlWizComWrapper\Scripts\1033\default.js:64-80).
//
// This is a text level port. It is deliberately defensive: the DTE project may
// not be available and any of the files may be missing, in which case only a log
// entry is written and the wizard keeps going.
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ArxVsixWizard.Items
{
    internal static class AtlItemSupport
    {
        /// <summary>
        /// Inserts <paramref name="fragment"/> (a rendered .idl snippet) into the project's
        /// main .idl, right before the file's last "};" (normally the end of the library
        /// block). Does nothing when a coclass/interface of the same name is already there.
        /// </summary>
        public static void InsertIdl(ItemContext context, string projectName, string fragment,
            string coclass, string interfaceName)
        {
            try
            {
                string dir = ProjectDir(context);
                if (dir == null) { Note("InsertIdl skipped: project directory unavailable"); return; }

                string idlPath = LocateIdl(dir, projectName);
                if (idlPath == null) { Note("InsertIdl skipped: no main .idl found in " + dir); return; }

                string text = File.ReadAllText(idlPath);
                if (Mentions(text, "coclass", coclass) || Mentions(text, "interface", interfaceName))
                {
                    Note("InsertIdl skipped: " + coclass + "/" + interfaceName + " already present in " + idlPath);
                    return;
                }

                // Anchor: the last "};" of the file (the closing brace of the library block),
                // otherwise append at the very end.
                int anchor = text.LastIndexOf("};", StringComparison.Ordinal);
                string insert = "\r\n" + fragment + "\r\n";
                text = anchor >= 0
                    ? text.Substring(0, anchor) + insert + text.Substring(anchor)
                    : text + insert;

                File.WriteAllText(idlPath, text);
                Note("InsertIdl: " + coclass + " inserted into " + idlPath);
            }
            catch (Exception ex) { ItemContext.Log("AtlItemSupport.InsertIdl", ex); }
        }

        /// <summary>
        /// Makes sure the generated .rgs file exists in the project directory and references it
        /// as a REGISTRY resource: appends "IDR_XXX REGISTRY DISCARDABLE \"Xxx.rgs\"" to the .rc
        /// and "#define IDR_XXX &lt;next id&gt;" to Resource.h (bumping _APS_NEXT_RESOURCE_VALUE).
        /// Missing files only produce a log entry, the .rgs itself is always left on disk.
        /// </summary>
        public static void AttachRgs(ItemContext context, string rgsFileName, string rgsId, string rgsContent)
        {
            try
            {
                string dir = ProjectDir(context);
                if (dir == null) { Note("AttachRgs skipped: project directory unavailable"); return; }
                if (string.IsNullOrWhiteSpace(rgsFileName) || string.IsNullOrWhiteSpace(rgsId))
                {
                    Note("AttachRgs skipped: rgs file name / id is empty");
                    return;
                }

                // The vstemplate ProjectItem already writes the .rgs, but re-create it if VS put
                // it somewhere else so the .rc reference below is always valid.
                string rgsPath = Path.Combine(dir, rgsFileName);
                if (!File.Exists(rgsPath) && rgsContent != null)
                    File.WriteAllText(rgsPath, rgsContent);

                string resPath = LocateResourceHeader(dir);
                if (resPath != null)
                    EnsureResourceDefine(resPath, rgsId);
                else
                    Note("AttachRgs: Resource.h not found; " + rgsFileName + " left on disk only");

                string rcPath = LocateSingle(dir, "*.rc");
                if (rcPath != null)
                    EnsureRegistryLine(rcPath, rgsId, rgsFileName);
                else
                    Note("AttachRgs: .rc not found; " + rgsFileName + " left on disk only");
            }
            catch (Exception ex) { ItemContext.Log("AtlItemSupport.AttachRgs", ex); }
        }

        // ---- helpers ----------------------------------------------------------------------

        static string ProjectDir(ItemContext context)
        {
            if (context == null || context.Project == null) return null;
            string dir = context.TargetDir;
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : null;
        }

        /// <summary>&lt;ProjectName&gt;.idl, or the only *.idl in the project directory.</summary>
        static string LocateIdl(string dir, string projectName)
        {
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                string preferred = Path.Combine(dir, projectName + ".idl");
                if (File.Exists(preferred)) return preferred;
            }
            var idls = Directory.GetFiles(dir, "*.idl", SearchOption.TopDirectoryOnly);
            return idls.Length == 1 ? idls[0] : null;
        }

        static string LocateSingle(string dir, string pattern)
        {
            var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
            if (files.Length > 0) return files[0];
            files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        static string LocateResourceHeader(string dir)
        {
            string direct = Path.Combine(dir, "Resource.h");
            if (File.Exists(direct)) return direct;
            return LocateSingle(dir, "resource.h");
        }

        static bool Mentions(string text, string keyword, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return Regex.IsMatch(text, @"\b" + keyword + @"\s+" + Regex.Escape(name) + @"\b",
                RegexOptions.CultureInvariant);
        }

        static void EnsureResourceDefine(string resPath, string rgsId)
        {
            string text = File.ReadAllText(resPath);
            if (Regex.IsMatch(text, @"#define\s+" + Regex.Escape(rgsId) + @"\b")) return;

            var m = Regex.Match(text, @"(_APS_NEXT_RESOURCE_VALUE\s+)(\d+)");
            if (!m.Success) { Note("AttachRgs: _APS_NEXT_RESOURCE_VALUE not found in " + resPath); return; }

            int id = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            string bumped = m.Groups[1].Value + (id + 1).ToString(CultureInfo.InvariantCulture);
            text = text.Substring(0, m.Index) + bumped + text.Substring(m.Index + m.Length);

            text = text.TrimEnd() + "\r\n#define " + rgsId + "  " + id.ToString(CultureInfo.InvariantCulture) + "\r\n";
            File.WriteAllText(resPath, text);
            Note("AttachRgs: #define " + rgsId + " " + id + " added to " + resPath);
        }

        static void EnsureRegistryLine(string rcPath, string rgsId, string rgsFileName)
        {
            string text = File.ReadAllText(rcPath);
            if (Regex.IsMatch(text, @"\b" + Regex.Escape(rgsId) + @"\s+REGISTRY\b")) return;

            string line = rgsId + " REGISTRY DISCARDABLE \"" + rgsFileName + "\"\r\n";
            File.AppendAllText(rcPath, line);
            Note("AttachRgs: " + rgsId + " REGISTRY added to " + rcPath);
        }

        /// <summary>ItemContext.Log only accepts exceptions, so informational notes ride in one.</summary>
        static void Note(string message) => ItemContext.Log("AtlItemSupport", new InvalidOperationException(message));
    }
}