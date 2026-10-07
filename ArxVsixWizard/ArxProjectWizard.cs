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

        public ArxProjectWizard() : this(WizardKind.ArxApp) { }

        protected ArxProjectWizard(WizardKind kind) { _kind = kind; }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind, object[] customParams)
        {
            try
            {
                // $projectname$ is the raw name; root.* files are renamed using it.
                string rawName;
                if (!replacementsDictionary.TryGetValue("$projectname$", out rawName) || string.IsNullOrWhiteSpace(rawName))
                    rawName = replacementsDictionary["$safeprojectname$"];

                var dialog = new WizardDialog(_kind);
                try
                {
                    var hwnd = NativeMethods.GetForegroundWindow();
                    if (hwnd != IntPtr.Zero) new WindowInteropHelper(dialog).Owner = hwnd;
                }
                catch { /* owner is optional */ }

                bool? ok = dialog.ShowDialog();
                if (ok != true)
                    throw new WizardCancelledException();

                _model = ProjectModel.Build(dialog.Options, rawName);

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

                // Render every planned source file into the project folder. The vcxproj skeleton
                // already lists them as ClCompile/ClInclude/ResourceCompile/Midl items (including
                // StdAfx.cpp Create-PCH and AssemblyInfo.cpp NotUsing-PCH metadata), so no
                // AddFile call is needed - adding would duplicate the existing items.
                foreach (var entry in _model.Files)
                {
                    try
                    {
                        string target = Path.Combine(dir, entry.TargetName);
                        string text = TemplateRenderer.Render(ReadResource(entry.ResourceName), _model.Symbols);
                        File.WriteAllText(target, text, EntryEncoding(entry.TargetName));
                    }
                    catch (Exception ex) { LogError("write " + entry.TargetName, ex); }
                }

                // Filters file (written next to the project, picked up on next solution load)
                try
                {
                    string filtersPath = projPath + ".filters";
                    File.WriteAllText(filtersPath, _model.BuildFiltersXml(), new UTF8Encoding(false));
                }
                catch (Exception ex) { LogError("filters", ex); }

                if (_kind == WizardKind.OmfApp && _model.Options.OmfApp)
                    CreateOmfResourceProject(project, dir);

                project.Save();
            }
            catch (Exception ex)
            {
                LogError("ProjectFinishedGenerating", ex);
            }
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
        public void RunFinished() { }

        static void LogError(string stage, Exception ex)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "ArxVsixWizard");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "wizard.log"),
                    DateTime.Now.ToString("s") + " [" + stage + "] " + ex + "\r\n");
            }
            catch { }
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
