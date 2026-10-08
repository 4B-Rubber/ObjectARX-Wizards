using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ArxGenProps
{
    /// <summary>
    /// arx-genprops — generates / cleans up the per-year ObjectARX MSBuild property sheets outside
    /// of MSI. The MSI line drives the same logic through ArxWizCustomAction's ArxProps custom
    /// actions; this EXE is what the Inno Setup line runs from [Run]. The year table and the three
    /// skeletons are the files under tools\arx-props, embedded verbatim, so both lines share one
    /// source of truth.
    ///
    /// Usage:
    ///   arx-genprops generate --props-dir &lt;dir&gt; [--sdk-root &lt;dir&gt;] [--acad-root &lt;dir&gt;]
    ///                         [--years 2020,2024,...] [--log &lt;file&gt;]
    ///   arx-genprops cleanup  --props-dir &lt;dir&gt; [--keep 2020,...] [--log &lt;file&gt;]
    ///   arx-genprops remove   --props-dir &lt;dir&gt; [--include-shared] [--log &lt;file&gt;]
    ///
    /// Exit codes: 0 success, 1 fatal error, 2 bad command line.
    /// </summary>
    public static class Program
    {
        const string ResTable = "ArxGenProps.table.json";
        const string ResNormal = "ArxGenProps.props-template.props";
        const string ResNetFx = "ArxGenProps.props-net-fx-template.props";
        const string ResNetCore = "ArxGenProps.props-net-core-template.props";

        static readonly Regex YearInName = new Regex(@"^Autodesk\.arx-(\d{4})", RegexOptions.IgnoreCase);

        /// <summary>Shared props the installer payload drops next to the generated ones.</summary>
        static readonly string[] SharedPropsPatterns =
        {
            "ObjectARX.*.props", "ObjectDBX.*.props", "ObjectGRX.*.props",
            "ObjectZRX.*.props", "HCSoft.*.props", "ZWSoft.*.props"
        };

        static string _logFile;

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Usage();
                    return 2;
                }

                string command = args[0].TrimStart('-', '/').ToLowerInvariant();
                var opt = ParseOptions(args, 1);
                _logFile = Get(opt, "log", null);

                switch (command)
                {
                    case "generate": return Generate(opt);
                    case "cleanup": return Cleanup(opt);
                    case "remove": return Remove(opt);
                    default:
                        Log("unknown command: " + args[0]);
                        Usage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Log("fatal: " + ex);
                return 1;
            }
        }

        static void Usage()
        {
            Console.Error.WriteLine("usage: arx-genprops generate --props-dir <dir> [--sdk-root <dir>] [--acad-root <dir>] [--years y1,y2,...] [--log <file>]");
            Console.Error.WriteLine("       arx-genprops cleanup  --props-dir <dir> [--keep y1,y2,...] [--log <file>]");
            Console.Error.WriteLine("       arx-genprops remove   --props-dir <dir> [--include-shared] [--log <file>]");
        }

        // ---- commands ----

        static int Generate(Dictionary<string, string> opt)
        {
            var table = PropsTable.Load();
            string dir = Get(opt, "props-dir", null);
            if (string.IsNullOrEmpty(dir))
            {
                Log("generate: --props-dir is required");
                return 2;
            }

            string sdkRoot = Get(opt, "sdk-root", table.DefaultRoot);
            string acadRoot = Get(opt, "acad-root", table.DefaultRoot);
            // An explicit empty --years means "nothing" (that is what the installer passes when the
            // user unticked every year); omitting the switch altogether means "all of them".
            bool yearsGiven = Has(opt, "years");
            var selected = SelectYears(table, Get(opt, "years", ""), !yearsGiven);

            if (selected.Count == 0)
            {
                Log("generate: no year selected, nothing to generate");
                return 0;
            }

            Directory.CreateDirectory(dir);
            Log("generate: dir=" + dir + " sdkRoot=" + sdkRoot + " acadRoot=" + acadRoot +
                " selected=" + string.Join(",", new List<string>(selected).ToArray()));

            foreach (var entry in table.Years)
            {
                if (!selected.Contains(entry.Year)) continue;
                WriteFile(dir, "Autodesk.arx-" + entry.Year + ".props",
                          Expand(ReadResource(ResNormal), BuildMap(table, entry, sdkRoot, null)));
                if (!string.IsNullOrEmpty(entry.CompatToolset))
                    WriteFile(dir, "Autodesk.arx-" + entry.Year + "-Compat.props",
                              Expand(ReadResource(ResNormal), BuildMap(table, entry, sdkRoot, entry.CompatToolset)));
                WriteFile(dir, "Autodesk.arx-" + entry.Year + "-net.props",
                          Expand(ReadResource(entry.NetKind == "core" ? ResNetCore : ResNetFx),
                                 BuildMap(table, entry, sdkRoot, null)));
            }

            WriteUserProps(dir, sdkRoot, acadRoot, table.DefaultRoot);
            return 0;
        }

        static int Cleanup(Dictionary<string, string> opt)
        {
            string dir = Get(opt, "props-dir", null);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

            // --keep with an empty value keeps nothing, which is what an empty year selection needs:
            // every generated sheet of this folder goes away. Omitting the switch keeps everything.
            bool keepGiven = Has(opt, "keep");
            var selected = SelectYears(PropsTable.Load(), Get(opt, "keep", ""), !keepGiven);
            foreach (var path in Directory.GetFiles(dir, "Autodesk.arx-*.props"))
            {
                var m = YearInName.Match(Path.GetFileName(path));
                if (!m.Success) continue;
                if (selected.Contains(m.Groups[1].Value)) continue;
                TryDelete(path, "cleanup: removed ");
            }
            return 0;
        }

        static int Remove(Dictionary<string, string> opt)
        {
            string dir = Get(opt, "props-dir", null);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;

            DeleteGenerated(dir, "remove: removed ", Has(opt, "include-shared"));
            return 0;
        }

        // ---- generation helpers (mirrors ArxWizCustomAction's ArxProps CA) ----

        static HashSet<string> SelectYears(PropsTable table, string years, bool emptyMeansAll)
        {
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(years))
            {
                if (emptyMeansAll)
                    foreach (var entry in table.Years) selected.Add(entry.Year);
                return selected;
            }
            foreach (var part in years.Split(','))
            {
                string y = part.Trim();
                if (y.Length == 0) continue;
                foreach (var entry in table.Years)
                    if (string.Equals(entry.Year, y, StringComparison.OrdinalIgnoreCase)) selected.Add(entry.Year);
            }
            return selected;
        }

        static void WriteFile(string dir, string name, string content)
        {
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, content.Replace("\n", "\r\n"), new UTF8Encoding(false));
            Log("generate: wrote " + path);
        }

        /// <summary>
        /// Writes the user-overridable roots file. Never clobbers user edits: existing values win and
        /// only missing entries are appended.
        /// </summary>
        static void WriteUserProps(string dir, string sdkRoot, string acadRoot, string defaultRoot)
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
                Log("generate: wrote " + path);
                return;
            }

            string text = File.ReadAllText(path);
            string updated = EnsureProperty(text, "ArxSdkRoot", sdkRoot ?? defaultRoot);
            updated = EnsureProperty(updated, "AcadRoot", acadRoot ?? defaultRoot);
            if (!string.Equals(text, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(path, updated, new UTF8Encoding(false));
                Log("generate: completed missing roots in " + path);
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

        /// <summary>
        /// Removes every file this tool generates from <paramref name="dir"/>. With
        /// <paramref name="includeShared"/> it also drops the shared payload props, which is what a
        /// moved props folder needs: the installer keeps those permanently, so uninstall would
        /// otherwise strand them at the old location.
        /// </summary>
        static void DeleteGenerated(string dir, string reason, bool includeShared = false)
        {
            foreach (var path in Directory.GetFiles(dir, "Autodesk.arx-*.props"))
                TryDelete(path, reason);

            string user = Path.Combine(dir, "ObjectARX.User.props");
            if (File.Exists(user)) TryDelete(user, reason);

            if (!includeShared) return;
            foreach (var pattern in SharedPropsPatterns)
                foreach (var path in Directory.GetFiles(dir, pattern))
                    TryDelete(path, reason);
        }

        static void TryDelete(string path, string reason)
        {
            try { File.Delete(path); Log(reason + path); }
            catch (Exception ex) { Log(reason + "cannot delete " + path + " (" + ex.Message + ")"); }
        }

        // ---- plumbing ----

        static Dictionary<string, string> ParseOptions(string[] args, int start)
        {
            var opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = start; i < args.Length; i++)
            {
                string a = args[i];
                if (a.Length == 0 || (a[0] != '-' && a[0] != '/')) continue;
                string key = a.TrimStart('-', '/');
                int eq = key.IndexOf('=');
                if (eq > 0)
                {
                    opt[key.Substring(0, eq)] = key.Substring(eq + 1);
                }
                else if (i + 1 < args.Length && args[i + 1].Length > 0 && args[i + 1][0] != '-' && args[i + 1][0] != '/')
                {
                    opt[key] = args[++i];
                }
                else
                {
                    opt[key] = "";
                }
            }
            return opt;
        }

        static string Get(Dictionary<string, string> opt, string key, string fallback)
        {
            string v;
            return opt.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }

        static bool Has(Dictionary<string, string> opt, string key)
        {
            return opt.ContainsKey(key);
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

        static void Log(string message)
        {
            Console.WriteLine("[arx-genprops] " + message);
            if (string.IsNullOrEmpty(_logFile)) return;
            try { File.AppendAllText(_logFile, DateTime.Now.ToString("s") + " " + message + Environment.NewLine); }
            catch { }
        }

        sealed class YearEntry
        {
            public string Year, ProjectFileVersion, SdkVersion, Toolset, ToolsVersion, CompatToolset;
            public string NetKind, NetTfVersion, NetTargetFramework, NetForAcad;
            public bool LegacyV35, HasWin32, Win32X86, HasCrx, DebugComments, No32Comment;
            public bool LibPathArxLibIncs, NetTfComment;
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
