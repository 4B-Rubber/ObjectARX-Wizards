using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;


//Written by Madhukar Moogala ADN

namespace ArxWizCustomAction
{
    /// <summary>Path helpers shared by the two custom action classes below.</summary>
    static class Paths
    {
        /// <summary>
        /// The Autodesk root is used as a *prefix*: the generated property sheets concatenate it with
        /// the year ("$(AcadRoot)AutoCAD <year>\"). The shipped defaults end in a backslash, but a path
        /// typed into the wizard need not, and a missing one turns the value into
        /// "...\AutodeskAutoCAD <year>\". Normalising here is the same guard the Inno line applies.
        /// </summary>
        internal static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.TrimEnd('\\') + "\\";
        }
    }

    public static class CustomActions
    {

        /// <summary>
        /// The Objective of this custom action is, we will replace [TARGETDIRECTORY] which is retrieved at time of Installation session in to
        /// several *.vsz file.
        /// We will not replace Wizversion, as every year MSVS comes with some new directory change, fix the [WIZVERSION] on several *.vsz files is becoming complex.
        /// We will change the WIZVERSION to hardcode value start copying.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult PatchVSFiles(Session session)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            session.Log("Begin PatchVSFiles");
            string TARGETDIR = session["TARGETDIR"];
            //This gives us the right folder where the wizard files are sitting.
            string vcFolder = session["D_VS2022VCFOLDER"];

            char[] delimiterChars = { ' ', ',', ';', ':', '\t' };

            //ArxAppWiz;ArxAppWiz182;ArxAtlWizComWrapper;ArxAtlWizDynProp;ArxWizCustomObject;ArxWizJig;ArxWizMFCSupport;ArxWizNETWrapper;ArxWizReactors
            string pArxWizList = session["ArxWizList"];
            session.Log(" >> PatchVSFiles: ArxWizList = " + pArxWizList);

            string[] ArxWizList = pArxWizList.Split(delimiterChars);
            List<string> lArxWizList = ArxWizList.ToList();

            session.Log(" >> PatchVSFiles:   Processing = " + vcFolder);
            DirectoryInfo di = new DirectoryInfo(vcFolder);
            FileInfo[] files = di.GetFiles("*.vsz", SearchOption.AllDirectories)
                                 .Where(p => lArxWizList.Contains(Path.GetFileNameWithoutExtension(p.Name))).ToArray();
            session.Log(" >> PatchVSFiles:   DirectoryInfo = " + files.Length.ToString());
            foreach (FileInfo file in files)
            {
                try
                {
                    session.Log(" >> PatchVSFiles:   ArxWizList =>> " + file.FullName);
                    string szData = File.ReadAllText(file.FullName);
                    szData = szData.Replace("[TARGETDIR]", TARGETDIR);
                    File.WriteAllText(file.FullName, szData);
                    session.Log("\n" + szData);
                }
                catch (Exception ex)
                {
                    session.Log(ex.Message);
                    return ActionResult.Failure;
                }
            }
            session.Log("Ending PatchVSFiles");
            return (ActionResult.Success);
        }


        [CustomAction]
        public static ActionResult PatchHTMLWizFiles(Session session)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            session.Log("Begin PatchHTMLWizFiles");
            string TARGETDIR = session["TARGETDIR"];
            string RDS = String.IsNullOrEmpty(session["RDS"]) ? "ADSK" : session["RDS"];
            session.Log(" >> PatchHTMLWizFiles: RDS = " + RDS + " / TARGETDIR = " + TARGETDIR);

            DirectoryInfo di = new DirectoryInfo(TARGETDIR);
            FileInfo[] files = di.GetFiles("default.htm", SearchOption.AllDirectories)
                             .Where(p => p.DirectoryName.Contains("AppWiz")).ToArray();
            session.Log(" >> PatchHTMLWizFiles:   DirectoryInfo = " + files.Length.ToString());
            foreach (FileInfo file in files)
            {
                try
                {
                    session.Log(" >> PatchHTMLWizFiles:   =>> " + file.FullName);
                    string szData = File.ReadAllText(file.FullName);
                    szData = szData.Replace("ADSK", RDS);
                    File.WriteAllText(file.FullName, szData);
                }
                catch (Exception ex)
                {
                    session.Log(ex.Message);
                    return ActionResult.Failure;
                }
            }

            session.Log("Ending PatchHTMLWizFiles");
            return (ActionResult.Success);
        }
        /// <summary>
        /// The legacy HTML wizard keeps the props folder in a hardcoded JS constant. Point it at the
        /// folder the user chose so the old and the new wizards resolve the same place.
        /// </summary>
        [CustomAction]
        public static ActionResult PatchArxCommonJsFiles(Session session)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            session.Log("Begin PatchArxCommonJsFiles");
            try
            {
                string targetDir = session["TARGETDIR"];
                string propsDir = session["ARXPROPSDIR"];
                session.Log(" >> PatchArxCommonJsFiles: propsDir = " + propsDir + " / TARGETDIR = " + targetDir);
                if (string.IsNullOrEmpty(targetDir) || string.IsNullOrEmpty(propsDir) || !Directory.Exists(targetDir))
                {
                    session.Log(" >> PatchArxCommonJsFiles: nothing to patch");
                    return ActionResult.Success;
                }

                // JavaScript string literal: every backslash has to be doubled.
                string jsPath = propsDir.TrimEnd('\\').Replace("\\", "\\\\") + "\\\\";
                foreach (string file in Directory.GetFiles(targetDir, "arxCommon.js", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    string updated = Regex.Replace(text, "var ARX_PROPS_DIR\\s*=\\s*\"[^\"]*\"\\s*;",
                                                   m => "var ARX_PROPS_DIR =\"" + jsPath + "\" ;");
                    if (updated != text)
                    {
                        File.WriteAllText(file, updated);
                        session.Log(" >> PatchArxCommonJsFiles: patched " + file);
                    }
                }
            }
            catch (Exception ex)
            {
                session.Log(ex.Message);
                return ActionResult.Failure;
            }
            session.Log("Ending PatchArxCommonJsFiles");
            return (ActionResult.Success);
        }

    }

    /// <summary>
    /// Generates the per-year ObjectARX property sheets at install time instead of shipping all 33 of
    /// them as MSI payload. The year table and the three skeletons embedded in this assembly are the
    /// very files under tools\arx-props, so the generator and this CA share one source of truth.
    ///
    /// The generated files are not tracked by the MSI File table, so removal and repair are handled
    /// here as well (RemoveArxProps / CleanupUnselectedArxProps).
    /// </summary>
    public static class ArxProps
    {
        const string PropsDirDefault = @"C:\Program Files\Autodesk\ObjectARX Props";

        /// <summary>
        /// Years a first install ticks, and the fallback when nothing can be detected. Read from the
        /// year table's "default" flags instead of being repeated here, so the JSON stays the one
        /// place a year is added - tools\arx-props\test-years-consistency.ps1 checks the installer-side
        /// lists against that same file.
        /// </summary>
        static IEnumerable<string> DefaultYears()
        {
            return PropsTable.Load().Years.Where(y => y.Default).Select(y => y.Year);
        }

        const string ResTable   = "ArxWizCustomAction.ArxProps.table.json";
        const string ResNormal  = "ArxWizCustomAction.ArxProps.props-template.props";
        const string ResNetFx   = "ArxWizCustomAction.ArxProps.props-net-fx-template.props";
        const string ResNetCore = "ArxWizCustomAction.ArxProps.props-net-core-template.props";

        static readonly Regex YearInName = new Regex(@"^Autodesk\.arx-(\d{4})", RegexOptions.IgnoreCase);

        /// <summary>
        /// First install only: tick DefaultYears. On an upgrade (PREV_PROPSDIR is set) the previous
        /// selection is restored by the year detection in the UI instead, so nothing gets unioned in.
        /// A selection passed on the command line also wins.
        /// </summary>
        [CustomAction]
        public static ActionResult DefaultArxYears(Session session)
        {
            try
            {
                if (!string.IsNullOrEmpty(session["PREV_PROPSDIR"]))
                {
                    Log(session, "DefaultArxYears: upgrade, keeping the previous year selection");
                    return ActionResult.Success;
                }
                foreach (var entry in PropsTable.Load().Years)
                {
                    if (string.IsNullOrEmpty(session["YEAR_" + entry.Year])) continue;
                    Log(session, "DefaultArxYears: explicit selection, leaving it alone");
                    return ActionResult.Success;
                }
                foreach (var year in DefaultYears())
                    session["YEAR_" + year] = "1";
                Log(session, "DefaultArxYears: first install, ticked the default years");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "DefaultArxYears failed: " + ex);
                return ActionResult.Success;
            }
        }

        /// <summary>
        /// Backs MaintenanceForm's "change the target years" option. That path reaches SdkForm
        /// without passing ObjectARXForm, so the year auto-tick never ran and every box would start
        /// empty - and confirming an empty selection deletes the props of every year. Seed the boxes
        /// with the years whose props were detected, or the first-install default when there are none.
        /// </summary>
        [CustomAction]
        public static ActionResult TickDetectedArxYears(Session session)
        {
            try
            {
                var table = PropsTable.Load();
                bool any = false;
                foreach (var entry in table.Years)
                {
                    if (string.IsNullOrEmpty(session["DET_YEAR_" + entry.Year])) continue;
                    session["YEAR_" + entry.Year] = "1";
                    any = true;
                }
                if (any)
                {
                    Log(session, "TickDetectedArxYears: ticked the years already on disk");
                    return ActionResult.Success;
                }
                foreach (var year in DefaultYears())
                    session["YEAR_" + year] = "1";
                Log(session, "TickDetectedArxYears: nothing detected, ticked the default years");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "TickDetectedArxYears failed: " + ex);
                return ActionResult.Success;
            }
        }

        /// <summary>
        /// Fills ARX_YEARS_SUMMARY from the current ticks. SdkForm's Next runs this before showing
        /// YearSummaryForm, which is the only page that can re-read the properties: MSI binds a check
        /// box to its property when the dialog is created and never re-reads it in place, so the
        /// selection has to be reported as text somewhere else.
        ///
        /// An empty YEAR_* means "not ticked" - MSI clears a check box's property when the box is
        /// cleared, and only "1" counts as ticked, here and in the props generator.
        /// </summary>
        [CustomAction]
        public static ActionResult BuildYearsSummary(Session session)
        {
            try
            {
                var ticked = new List<string>();
                foreach (var entry in PropsTable.Load().Years)
                    if (session["YEAR_" + entry.Year] == "1") ticked.Add(entry.Year);
                session["ARX_YEARS_SUMMARY"] = ticked.Count == 0
                    ? "无。不生成任何属性表，并且会把以前生成的清掉。"
                    : string.Join(", ", ticked.ToArray());
                Log(session, "BuildYearsSummary: " + ticked.Count + " ticked");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "BuildYearsSummary failed: " + ex);
                return ActionResult.Success;
            }
        }

        /// <summary>
        /// Deferred custom actions only receive CustomActionData, and a type-51 property-set action is
        /// capped at 255 characters in the MSI CustomAction table - far too small for four folder paths
        /// plus 16 year flags. Assemble the data here instead: this immediate action runs during script
        /// generation, so the values are in place before the deferred actions are scheduled.
        /// </summary>
        [CustomAction]
        public static ActionResult PrepArxPropsData(Session session)
        {
            try
            {
                var years = new StringBuilder();
                foreach (var entry in PropsTable.Load().Years)
                {
                    years.Append("YEAR_").Append(entry.Year).Append('=')
                         .Append(session["YEAR_" + entry.Year] ?? "").Append(';');
                }

                string propsDir = session["ARXPROPSDIR"] ?? "";
                session["CA_CREATEARXPROPS"] =
                    "PROPSDIR=" + propsDir +
                    ";PREVDIR=" + (session["PROPSDIR_PROBE"] ?? "") +
                    ";ARXROOT=" + Paths.EnsureTrailingSlash(session["ARXROOT"]) + ";" + years;
                session["CA_CLEANUPUNSELECTEDARXPROPS"] = "PROPSDIR=" + propsDir + ";" + years;
                session["CA_REMOVEARXPROPS"] = "PROPSDIR=" + propsDir;

                Log(session, "PrepArxPropsData: propsDir=" + propsDir + " sdkRoot=" + session["ARXROOT"]);
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "PrepArxPropsData failed: " + ex);
                return ActionResult.Success;
            }
        }

        [CustomAction]
        public static ActionResult CreateArxProps(Session session)
        {
            try
            {
                var data = ParseData(session["CustomActionData"]);
                var table = PropsTable.Load();
                string dir = Value(data, "PROPSDIR", PropsDirDefault);
                // One Autodesk root for both: the SDK and AutoCAD live under it, and the year is
                // appended by each generated sheet rather than by this installer.
                string sdkRoot = Value(data, "ARXROOT", table.DefaultRoot);
                string acadRoot = sdkRoot;
                var selected = SelectedYears(data, table);

                if (selected.Count == 0)
                {
                    Log(session, "CreateArxProps: no year selected, nothing to generate");
                    return ActionResult.Success;
                }

                // If the folder moved (the user picked a new one, or a previous install used another),
                // the old generated files are not known to the File table, so drop them here.
                string prevDir = Value(data, "PREVDIR", null);
                if (!string.IsNullOrEmpty(prevDir) &&
                    !string.Equals(prevDir.TrimEnd('\\'), dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    Log(session, "CreateArxProps: props folder moved, cleaning " + prevDir);
                    DeleteOurFiles(session, prevDir, "CreateArxProps: removed stale ");
                }

                Directory.CreateDirectory(dir);
                Log(session, "CreateArxProps: dir=" + dir + " sdkRoot=" + sdkRoot + " acadRoot=" + acadRoot +
                             " selected=" + string.Join(",", selected.ToArray()));

                foreach (var entry in table.Years)
                {
                    if (!selected.Contains(entry.Year)) continue;
                    WriteFile(session, dir, "Autodesk.arx-" + entry.Year + ".props",
                              Expand(ReadResource(ResNormal), BuildMap(table, entry, sdkRoot, null)));
                    if (!string.IsNullOrEmpty(entry.CompatToolset))
                        WriteFile(session, dir, "Autodesk.arx-" + entry.Year + "-Compat.props",
                                  Expand(ReadResource(ResNormal), BuildMap(table, entry, sdkRoot, entry.CompatToolset)));
                    WriteFile(session, dir, "Autodesk.arx-" + entry.Year + "-net.props",
                              Expand(ReadResource(entry.NetKind == "core" ? ResNetCore : ResNetFx),
                                     BuildMap(table, entry, sdkRoot, null)));
                }

                WriteUserProps(session, dir, sdkRoot, acadRoot, table.DefaultRoot);
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "CreateArxProps failed: " + ex);
                return ActionResult.Success; // sequenced with Return="ignore"; never abort the install
            }
        }

        /// <summary>Deletes the generated props of every year the user did NOT tick (upgrades leave stale ones).</summary>
        [CustomAction]
        public static ActionResult CleanupUnselectedArxProps(Session session)
        {
            try
            {
                var data = ParseData(session["CustomActionData"]);
                var table = PropsTable.Load();
                string dir = Value(data, "PROPSDIR", PropsDirDefault);
                if (!Directory.Exists(dir)) return ActionResult.Success;

                var selected = SelectedYears(data, table);
                foreach (var path in Directory.GetFiles(dir, "Autodesk.arx-*.props"))
                {
                    var m = YearInName.Match(Path.GetFileName(path));
                    if (!m.Success) continue;
                    if (selected.Contains(m.Groups[1].Value)) continue;
                    try { File.Delete(path); Log(session, "CleanupUnselectedArxProps: removed " + path); }
                    catch (Exception ex) { Log(session, "CleanupUnselectedArxProps: cannot delete " + path + " (" + ex.Message + ")"); }
                }
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "CleanupUnselectedArxProps failed: " + ex);
                return ActionResult.Success;
            }
        }

        /// <summary>Removes everything we generated — the MSI does not know these files exist.</summary>
        [CustomAction]
        public static ActionResult RemoveArxProps(Session session)
        {
            try
            {
                var data = ParseData(session["CustomActionData"]);
                DeleteOurFiles(session, Value(data, "PROPSDIR", PropsDirDefault), "RemoveArxProps: removed ");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                Log(session, "RemoveArxProps failed: " + ex);
                return ActionResult.Success;
            }
        }

        /// <summary>
        /// Removes every file this CA generates from <paramref name="dir"/>. Nothing else in that
        /// folder is ours: the MSI ships no property sheet, so whatever else lives there belongs to
        /// the machine and is left alone.
        /// </summary>
        static void DeleteOurFiles(Session session, string dir, string reason)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var path in Directory.GetFiles(dir, "Autodesk.arx-*.props"))
            {
                try { File.Delete(path); Log(session, reason + path); }
                catch (Exception ex) { Log(session, reason + "cannot delete " + path + " (" + ex.Message + ")"); }
            }
            string user = Path.Combine(dir, "ObjectARX.User.props");
            if (File.Exists(user))
            {
                try { File.Delete(user); Log(session, reason + user); }
                catch (Exception ex) { Log(session, reason + "cannot delete " + user + " (" + ex.Message + ")"); }
            }
        }

        // ---- generation helpers (mirrors tools\arx-props\gen-arx-props.ps1) ----

        static HashSet<string> SelectedYears(Dictionary<string, string> data, PropsTable table)
        {
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in table.Years)
            {
                string key = "YEAR_" + entry.Year;
                if (data.ContainsKey(key) && data[key] == "1") selected.Add(entry.Year);
            }
            return selected;
        }

        static void WriteFile(Session session, string dir, string name, string content)
        {
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, content.Replace("\n", "\r\n"), new UTF8Encoding(false));
            Log(session, "CreateArxProps: wrote " + path);
        }

        /// <summary>
        /// Writes the user-overridable roots file. Never clobbers user edits: existing values win and
        /// only missing entries are appended.
        /// </summary>
        static void WriteUserProps(Session session, string dir, string sdkRoot, string acadRoot, string defaultRoot)
        {
            string path = Path.Combine(dir, "ObjectARX.User.props");
            if (!File.Exists(path))
            {
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
                sb.Append("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n");
                sb.Append("  <!-- Roots used by the generated Autodesk.arx-<year>.props. Edit these values and\r\n");
                sb.Append("       rebuild to relocate the SDK / AutoCAD without reinstalling. -->\r\n");
                sb.Append("  <PropertyGroup>\r\n");
                sb.Append("    <ArxSdkRoot>" + (sdkRoot ?? defaultRoot) + "</ArxSdkRoot>\r\n");
                sb.Append("    <AcadRoot>" + (acadRoot ?? defaultRoot) + "</AcadRoot>\r\n");
                sb.Append("  </PropertyGroup>\r\n");
                sb.Append("</Project>\r\n");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Log(session, "CreateArxProps: wrote " + path);
                return;
            }

            string text = File.ReadAllText(path);
            string updated = EnsureProperty(text, "ArxSdkRoot", sdkRoot ?? defaultRoot);
            updated = EnsureProperty(updated, "AcadRoot", acadRoot ?? defaultRoot);
            if (!string.Equals(text, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(path, updated, new UTF8Encoding(false));
                Log(session, "CreateArxProps: completed missing roots in " + path);
            }
        }

        static string EnsureProperty(string text, string name, string value)
        {
            if (text.IndexOf("<" + name, StringComparison.OrdinalIgnoreCase) >= 0) return text;
            string line = "  <" + name + ">" + value + "</" + name + ">\r\n";
            int idx = text.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? text + "\r\n" + line : text.Substring(0, idx) + line + text.Substring(idx);
        }

        static Dictionary<string, string> BuildMap(PropsTable table, YearEntry y, string root, string toolsetOverride)
        {
            var m = new Dictionary<string, string>(StringComparer.Ordinal);
            m["YEAR"] = y.Year;
            m["PFV"] = y.ProjectFileVersion;
            m["SDKVERSION"] = y.SdkVersion;
            m["TOOLSET"] = toolsetOverride ?? y.Toolset;
            m["TOOLSVERSION"] = y.ToolsVersion;
            m["CFGB"] = y.Year + "B";
            m["CFGS"] = y.Year + "S";
            m["CFGD"] = y.Year + "d";
            m["SDKROOT"] = root;
            m["ACADROOT"] = root;
            m["WIN32ROOT"] = y.Win32X86 ? table.Win32RootX86 : root;
            m["NETTFV"] = y.NetTfVersion ?? "";
            m["NETTF"] = y.NetTargetFramework ?? "";
            m["CRXNETCOND"] = y.HasCrx ? "'$(ArxAppType)'=='crxnet' or " : "";
            m["ARXLIBINCS"] = y.LibPathArxLibIncs ? "$(ArxLibIncs);" : "";
            m["TFCOMMENT"] = y.NetTfComment
                ? " <!-- That will force Platform Toolset to vc9 in Visual Studio 2010 -->" : "";

            // Whole-line blocks; the value keeps its own indentation and trailing newline.
            m["USERIMPORT"] = "\t<Import Project=\"$(MSBuildThisFileDirectory)ObjectARX.User.props\" " +
                              "Condition=\"Exists('$(MSBuildThisFileDirectory)ObjectARX.User.props')\" />\n";
            m["NO32COMMENT"] = y.No32Comment
                ? "\t<!--There is No 32 Bit AutoCAD Starting From AutoCAD 2020-->\n" : "";
            m["WIN32ACAD"] = y.HasWin32
                ? "\t\t<AcadDir Condition=\"'$(Platform)'=='Win32' And '$(AcadDir)' == ''\">" +
                  "@@WIN32ROOT@@AutoCAD @@YEAR@@\\</AcadDir>\n" : "";
            m["ACADEXE"] = y.HasCrx
                ? "\t\t<AcadExe Condition=\"'$(ArxAppType)'=='dbx' or '$(ArxAppType)'=='dbxnet' or " +
                  "'$(ArxAppType)'=='arx' or '$(ArxAppType)'=='arxnet'\">acad.exe</AcadExe>\n" +
                  "\t\t<AcadExe Condition=\"'$(ArxAppType)'=='crx' or '$(ArxAppType)'=='crxnet'\">" +
                  "accoreconsole.exe</AcadExe>\n"
                : "";
            m["SDKINCS_WIN32"] = y.HasWin32
                ? "\t\t<ArxSdkIncs Condition=\"'$(Platform)'=='Win32'\">" +
                  "$(ArxSdkDir)\\inc;$(ArxSdkDir)\\inc-win32</ArxSdkIncs>\n" : "";
            m["SDKLIBSS_WIN32"] = y.HasWin32
                ? "\t\t<ArxSdkLibs Condition=\"'$(Platform)'=='Win32'\">" +
                  "$(ArxSdkDir)\\lib-win32</ArxSdkLibs>\n" : "";
            m["CRXIMPORT"] = y.HasCrx
                ? "\t\t<Import Condition=\"'$(ArxAppType)'=='crx' or '$(ArxAppType)'=='crxnet'\" " +
                  "Project=\"$(ArxSdkDir)\\inc\\crx.props\" />\n" : "";
            m["TF35BLOCK"] = y.LegacyV35
                ? "\t<PropertyGroup>\n\t\t<TargetFrameworkVersion>v3.5</TargetFrameworkVersion> " +
                  "<!-- That will force Platform Toolset to vc9 in Visual Studio 2010 -->\n\t</PropertyGroup>\n" : "";
            m["DBGCMD"] = y.LegacyV35
                ? "\t\t<LocalDebuggerCommand Condition=\"'$(AcadDir)' != ''\">$(AcadDir)\\acad.exe</LocalDebuggerCommand>\n"
                : "\t\t<LocalDebuggerCommand>$(AcadDir)$(AcadExe)</LocalDebuggerCommand>\n";
            m["DBGCOMMENT"] = y.DebugComments
                ? "\t\t<!-- LocalDebuggerMergeEnvironment>true</LocalDebuggerMergeEnvironment -->\n" +
                  "\t\t<!-- LocalDebuggerAttach>False</LocalDebuggerAttach -->\n" +
                  "\t\t<!-- LocalDebuggerSQLDebugging>False</LocalDebuggerSQLDebugging -->\n" : "";
            m["CRXDEF"] = y.HasCrx
                ? "\t\t\t<PreprocessorDefinitions Condition=\"'$(ArxAppType)'=='crx' or " +
                  "'$(ArxAppType)'=='crxnet'\">_CRXAPP;%(PreprocessorDefinitions)</PreprocessorDefinitions>\n" : "";
            m["TMWIN32"] = y.HasWin32
                ? "\t\t\t<TargetMachine Condition=\"'$(Platform)'=='Win32'\">MachineX86</TargetMachine>\n" : "";
            m["FORACAD"] = string.IsNullOrEmpty(y.NetForAcad) ? "" : "\t\t<!--  " + y.NetForAcad + " -->\n";
            return m;
        }

        static string Expand(string text, Dictionary<string, string> map)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                string before = text;
                foreach (var kv in map)
                {
                    string v = kv.Value ?? "";
                    text = Regex.Replace(text, "(?m)^[ \t]*@@" + Regex.Escape(kv.Key) + "@@[ \t]*\n", m => v);
                }
                foreach (var kv in map)
                {
                    string v = kv.Value ?? "";
                    text = Regex.Replace(text, "@@" + Regex.Escape(kv.Key) + "@@", m => v);
                }
                if (string.Equals(before, text, StringComparison.Ordinal)) break;
            }
            return text;
        }

        static string ReadResource(string logicalName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream(logicalName))
            {
                if (s == null)
                    throw new InvalidOperationException("embedded resource not found: " + logicalName);
                using (var r = new StreamReader(s, Encoding.UTF8))
                    return r.ReadToEnd().Replace("\r\n", "\n");
            }
        }

        static Dictionary<string, string> ParseData(string data)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(data)) return d;
            foreach (var part in data.Split(';'))
            {
                int i = part.IndexOf('=');
                if (i > 0) d[part.Substring(0, i).Trim()] = part.Substring(i + 1);
            }
            return d;
        }

        static string Value(Dictionary<string, string> data, string key, string fallback)
        {
            string v;
            return data.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        static void Log(Session session, string message)
        {
            try { session.Log("ArxProps: " + message); } catch { }
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "ObjectARXWizardsInstaller.log"),
                                   DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        sealed class YearEntry
        {
            public string Year, ProjectFileVersion, SdkVersion, Toolset, ToolsVersion, CompatToolset;
            public string NetKind, NetTfVersion, NetTargetFramework, NetForAcad;
            public bool LegacyV35, HasWin32, Win32X86, HasCrx, DebugComments, No32Comment;
            public bool LibPathArxLibIncs, NetTfComment;
            /// <summary>Ticked on a first install (the JSON's "default" flag).</summary>
            public bool Default;
        }

        /// <summary>Reader for tools\arx-props\arx-props-table.json (embedded verbatim).</summary>
        sealed class PropsTable
        {
            public string DefaultRoot;
            public string Win32RootX86;
            readonly List<YearEntry> _years = new List<YearEntry>();

            public IEnumerable<YearEntry> Years { get { return _years; } }

            public static PropsTable Load()
            {
                var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(ReadResource(ResTable));
                var table = new PropsTable
                {
                    DefaultRoot = (string)root["defaultRoot"],
                    Win32RootX86 = (string)root["win32RootX86"],
                };
                foreach (Dictionary<string, object> y in (object[])root["years"])
                {
                    table._years.Add(new YearEntry
                    {
                        Year = Str(y, "year"),
                        ProjectFileVersion = Str(y, "projectFileVersion"),
                        SdkVersion = Str(y, "sdkVersion"),
                        Toolset = Str(y, "toolset"),
                        ToolsVersion = Str(y, "toolsVersion"),
                        CompatToolset = Str(y, "compatToolset"),
                        NetKind = Str(y, "netKind"),
                        NetTfVersion = Str(y, "netTfVersion"),
                        NetTargetFramework = Str(y, "netTargetFramework"),
                        NetForAcad = Str(y, "netForAcad"),
                        LegacyV35 = Bool(y, "legacyV35"),
                        HasWin32 = Bool(y, "hasWin32"),
                        Win32X86 = Bool(y, "win32X86"),
                        HasCrx = Bool(y, "hasCrx"),
                        DebugComments = Bool(y, "debugComments"),
                        No32Comment = Bool(y, "no32Comment"),
                        LibPathArxLibIncs = Bool(y, "libPathArxLibIncs"),
                        NetTfComment = Bool(y, "netTfComment"),
                        Default = Bool(y, "default"),
                    });
                }
                return table;
            }

            static string Str(Dictionary<string, object> d, string key)
            {
                object v;
                return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
            }

            static bool Bool(Dictionary<string, object> d, string key)
            {
                object v;
                return d.TryGetValue(key, out v) && v != null && Convert.ToBoolean(v);
            }
        }
    }
}