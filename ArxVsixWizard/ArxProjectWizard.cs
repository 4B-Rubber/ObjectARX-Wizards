// Copyright (c) Autodesk, Inc. All rights reserved.
// Visual Studio IWizard implementation shared by the ObjectARX and OMF project
// templates. Shows the WPF option page, renders the vcxproj parameters, and
// post-renders every source file with the classic [!if]/[!output] directives.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using ArxVsixWizard.Engine;
using ArxVsixWizard.Models;
using ArxVsixWizard.UI;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;

namespace ArxVsixWizard
{
    public class ArxProjectWizard : IWizard
    {
        readonly WizardKind _kind;
        ProjectModel _model;
        static readonly AnsiHolder Ansi = new AnsiHolder();

        // Phase timings for the CreationSummary line: when the run started, how long the option page
        // was up, and when the options were known (the template engine works between that moment and
        // ProjectFinishedGenerating).
        DateTime _runStartedAt = DateTime.UtcNow;
        DateTime _optionsReadyAt = DateTime.UtcNow;
        long _dialogMs;

        public ArxProjectWizard() : this(WizardKind.ArxApp) { }

        protected ArxProjectWizard(WizardKind kind) { _kind = kind; }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind, object[] customParams)
        {
            try
            {
                _runStartedAt = DateTime.UtcNow;
                // $projectname$ is the raw name; root.* files are renamed using it.
                string rawName;
                if (!replacementsDictionary.TryGetValue("$projectname$", out rawName) || string.IsNullOrWhiteSpace(rawName))
                    rawName = replacementsDictionary["$safeprojectname$"];

                WizardDiagnostics.LogRunContext("RunStarted");
                WizardDiagnostics.Log("RunStarted", "project=" + rawName + " runKind=" + runKind
                    + " kind=" + _kind + " automation=" + (automationObject == null ? "null" : automationObject.GetType().FullName));

                WizardOptions options = AskOptions();
                _optionsReadyAt = DateTime.UtcNow;
                _model = ProjectModel.Build(options, rawName);
                WizardDiagnostics.Log("ModelBuilt", "name=" + _model.ProjectName
                    + " files=" + _model.Files.Count
                    + " rds=" + options.Rds
                    + " years=" + string.Join(",", new List<string>(options.SelectedYears).ToArray())
                    + " mfc=" + options.AcadMfcExtension + " atl=" + options.AcadAtlExtension);

                // Custom parameters for the vcxproj skeleton. The keys must carry the
                // surrounding '$' delimiters: the template engine replaces the literal
                // key text, so a bare key would only match the inner name and leave the
                // two '$' behind ("$    <ProjectConfiguration ..." -> invalid vcxproj).
                foreach (var kv in _model.BuildReplacementParameters())
                    replacementsDictionary["$" + kv.Key + "$"] = kv.Value;
            }
            catch (WizardCancelledException)
            {
                throw;
            }
            catch (WizardOptionException ex)
            {
                MessageBox.Show(ex.Message, "ObjectARX Wizard", MessageBoxButton.OK, MessageBoxImage.Warning);
                throw new WizardCancelledException();
            }
        }

        /// <summary>
        /// The option page (WizardDialog) or, with %TEMP%\ArxVsixWizard\no-ui.flag present, the same
        /// defaults without any WPF window; the offline smoke test drives the wizards that way.
        /// </summary>
        WizardOptions AskOptions()
        {
            var defaults = new WizardOptions
            {
                Kind = _kind,
                Rds = _kind == WizardKind.OmfApp ? "asdk" : "ADSK",
                // The page clears both AutoCAD extension flags while MFC/COM stand at "none",
                // which is the state it opens in.
                AcadMfcExtension = false,
                AcadAtlExtension = false,
            };

            if (WizardDiagnostics.NoUi)
            {
                // Keep only the years that are really installed, like the check boxes on the page do.
                var installed = new List<string>();
                foreach (var year in defaults.SelectedYears)
                    if (ArxVersionTable.IsInstalled(year)) installed.Add(year);
                if (installed.Count > 0)
                {
                    defaults.SelectedYears.Clear();
                    foreach (var year in installed) defaults.SelectedYears.Add(year);
                }
                return defaults;
            }

            var dialog = new WizardDialog(_kind);
            IntPtr owner = IntPtr.Zero;
            try
            {
                // Visual Studio's own main window - never GetForegroundWindow() alone, which
                // handed back another application's window whenever VS was not in the foreground.
                owner = WizardDiagnostics.PreferredOwner();
                if (owner != IntPtr.Zero)
                {
                    WizardDiagnostics.Log("WizardDialog", "owner " + WizardDiagnostics.DescribeWindow(owner));
                    new WindowInteropHelper(dialog).Owner = owner;
                }
            }
            catch (Exception ex) { WizardDiagnostics.Log("WizardDialog", "owner failed: " + ex.Message); }

            WizardDiagnostics.Log("WizardDialog", "showing " + WizardDiagnostics.WindowState());
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool? accepted = dialog.ShowDialog();
            clock.Stop();
            _dialogMs = clock.ElapsedMilliseconds;
            WizardDiagnostics.Log("WizardDialog", "closed result=" + accepted + " ms=" + clock.ElapsedMilliseconds
                + " owner=" + (owner == IntPtr.Zero ? "none" : WizardDiagnostics.DescribeWindow(owner))
                + " " + WizardDiagnostics.WindowState());

            if (accepted != true)
                throw new WizardCancelledException();

            WizardOptions chosen = dialog.Options;
            WizardDiagnostics.Log("WizardDialog", "options kind=" + chosen.Kind + " rds=" + chosen.Rds
                + " years=" + string.Join(",", new List<string>(chosen.SelectedYears).ToArray())
                + " mfc=" + chosen.AcadMfcExtension + " atl=" + chosen.AcadAtlExtension);
            return chosen;
        }

        // The vstemplate carries no <ProjectItem>; the generated vcxproj already declares every
        // source file (see ProjectModel.BuildItemsXml), so nothing is copied by VS. These two
        // callbacks are therefore never invoked for source files; files are written in
        // ProjectFinishedGenerating instead.
        public bool ShouldAddProjectItem(string filePath) => true;

        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }

        public void ProjectFinishedGenerating(Project project)
        {
            try
            {
                string projPath = project.FullName;
                string dir = Path.GetDirectoryName(projPath);
                // The template engine is done at this point: everything before it was our dialog.
                DateTime projectReadyAt = DateTime.UtcNow;
                WizardDiagnostics.Log("ProjectFinished", "project=" + projPath + " " + WizardDiagnostics.WindowState());

                long writeMs = WriteGeneratedFiles(dir, projPath);

                if (_kind == WizardKind.OmfApp && _model.Options.OmfApp)
                    CreateOmfResourceProject(project, dir);

                var save = System.Diagnostics.Stopwatch.StartNew();
                project.Save();
                save.Stop();
                long saveMs = save.ElapsedMilliseconds;
                WizardDiagnostics.Log("CreationDone", "files=" + _model.Files.Count
                    + " writeMs=" + writeMs + " saveMs=" + saveMs
                    + " " + WizardDiagnostics.WindowState());
                // "How long does a successful creation take, and when was it" in one line, with the
                // same phases the rest of the log records separately.
                WizardDiagnostics.NoteCreationDone();
                WizardDiagnostics.LogCreationSummary(Path.GetFileNameWithoutExtension(projPath),
                    _model.Files.Count, _dialogMs,
                    (long)(projectReadyAt - _optionsReadyAt).TotalMilliseconds,
                    writeMs, saveMs,
                    (long)(DateTime.UtcNow - _runStartedAt).TotalMilliseconds);
                // From here on the user is on their own in Visual Studio's dialogs, so the sampler
                // takes over until a few minutes after the last creation run.
                WizardDiagnostics.StartWatch("created");
            }
            catch (Exception ex)
            {
                LogError("ProjectFinishedGenerating", ex);
            }
        }

        /// <summary>
        /// Renders every planned source file into the project folder and writes the filters file next
        /// to the project. The vcxproj skeleton already lists the files as ClCompile/ClInclude/
        /// ResourceCompile/Midl items (including StdAfx.cpp Create-PCH and AssemblyInfo.cpp
        /// NotUsing-PCH metadata), so no AddFile call is needed - adding would duplicate the existing
        /// items. Returns the milliseconds spent writing.
        /// </summary>
        long WriteGeneratedFiles(string dir, string projectPath)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var entry in _model.Files)
            {
                try
                {
                    string target = Path.Combine(dir, entry.TargetName);
                    string text = TemplateRenderer.Render(ReadResource(entry.ResourceName), _model.Symbols);
                    File.WriteAllText(target, text, EntryEncoding(entry.TargetName));
                    // Per-file timing: with the late order these writes are what keeps the project
                    // system busy right after the wizard returns, which is when the user opens
                    // "Add New Item".
                    WizardDiagnostics.Log("WriteFile", entry.TargetName + " bytes=" + text.Length
                        + " t=" + clock.ElapsedMilliseconds + "ms");
                }
                catch (Exception ex) { LogError("write " + entry.TargetName, ex); }
            }

            if (!string.IsNullOrEmpty(projectPath))
            {
                try
                {
                    string filtersPath = projectPath + ".filters";
                    File.WriteAllText(filtersPath, _model.BuildFiltersXml(), new UTF8Encoding(false));
                }
                catch (Exception ex) { LogError("filters", ex); }
            }
            return clock.ElapsedMilliseconds;
        }

        /// <summary>OMF: resource-only DLL sub-project under Enu\ plus a solution build dependency.</summary>
        void CreateOmfResourceProject(Project project, string dir)
        {
            string name = _model.ProjectName;
            string enuDir = Path.Combine(dir, "Enu");
            Directory.CreateDirectory(enuDir);

            WriteRendered(ProjectModel.ResourcePrefixOmf + "OmfEnuRes.h", Path.Combine(enuDir, "Resource.h"));
            WriteRendered(ProjectModel.ResourcePrefixOmf + "OmfEnuRes.rc", Path.Combine(enuDir, name + "Enu.rc"));
            string enuProj = Path.Combine(enuDir, name + "Enu.vcxproj");
            WriteRendered(ProjectModel.ResourcePrefixOmf + "OmfEnuRes.vcxproj", enuProj, utf8: true);
            WriteRendered(ProjectModel.ResourcePrefixOmf + "OmfEnuRes.vcxproj.filters", enuProj + ".filters", utf8: true);

            var dte = project.DTE;
            Project resProject = dte.Solution.AddFromFile(enuProj, false);

            // Solution-level build dependency: main project depends on the resource DLL
            BuildDependencies deps = dte.Solution.SolutionBuild.BuildDependencies;
            for (int i = 1; i <= deps.Count; i++)
            {
                BuildDependency dep = deps.Item(i);
                if (string.Equals(dep.Project.UniqueName, project.UniqueName, StringComparison.OrdinalIgnoreCase))
                {
                    dep.AddProject(resProject.UniqueName);
                    break;
                }
            }
        }

        void WriteRendered(string resource, string target, bool utf8 = false)
        {
            string text = TemplateRenderer.Render(ReadResource(resource), _model.Symbols);
            File.WriteAllText(target, text, utf8 ? (Encoding)new UTF8Encoding(false) : Ansi.Encoding);
        }

        static Encoding EntryEncoding(string targetName)
            => targetName.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase)
               || targetName.EndsWith(".filters", StringComparison.OrdinalIgnoreCase)
                ? (Encoding)new UTF8Encoding(false)
                : new AnsiHolder().Encoding;

        static string ReadResource(string logicalName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(logicalName))
            {
                if (s == null)
                    throw new InvalidOperationException("Embedded template resource not found: " + logicalName);
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                // The old templates were ANSI (Windows-1252) files.
                return new AnsiHolder().Encoding.GetString(bytes);
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void RunFinished()
        {
            WizardDiagnostics.Log("RunFinished", "project=" + (_model == null ? "?" : _model.ProjectName)
                + " " + WizardDiagnostics.WindowState());
            WizardDiagnostics.StartWatch("projectDone");
        }

        static void LogError(string stage, Exception ex)
        {
            WizardDiagnostics.Log("ERROR/" + stage, ex.ToString());
        }

        // Lazily obtain the Windows-1252 encoding (registered via CodePagesEncodingProvider on .NET Core;
        // on net472 it is available directly).
        sealed class AnsiHolder
        {
            public Encoding Encoding { get; } = TryGet();
            static Encoding TryGet()
            {
                try { return Encoding.GetEncoding(1252); }
                catch { return Encoding.Default; }
            }
        }
    }

    public sealed class OmfProjectWizard : ArxProjectWizard
    {
        public OmfProjectWizard() : base(WizardKind.OmfApp) { }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
