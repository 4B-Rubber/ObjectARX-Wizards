// Shared IWizard plumbing for every ObjectARX class/item wizard.
//
// Flow:
//   RunStarted  - derive the default name, show UI\ItemDialog, render every
//                 template and inject $ArxWrapNFile$ / $ArxWrapNContent$ into the
//                 replacements dictionary (VS then copies the thin wrapper files
//                 under those names).
//   ShouldAddProjectItem - false for optional files that were not requested.
//   ProjectItemFinishedGenerating - capture the target project (DTE is only safe once
//                 VS reports the generated item).
//   RunFinished - project level side effects (resource/.idl edits, PCH), always logged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public abstract class ArxItemWizardBase : IWizard
    {
        protected ItemModel Model { get; private set; }
        protected ItemContext Context { get; } = new ItemContext();

        /// <summary>Build the field/file model for this wizard.</summary>
        protected abstract ItemModel CreateModel(string suggestedName);

        /// <summary>Project level side effects, executed after the item was added.</summary>
        protected virtual void RunPostActions() { }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind, object[] customParams)
        {
            try
            {
                string suggested = ItemContext.ReadReplacement(replacementsDictionary,
                    "$fileinputname$", "$itemname$", "$safeitemname$", "$rootname$");
                Model = CreateModel(Names.SafeName(Path.GetFileNameWithoutExtension(suggested)));

                // Capture the target project right away: the callbacks that would normally report
                // it (ProjectItemFinishedGenerating) are not guaranteed for item templates, and the
                // post actions need the project directory.
                Project target = FindTargetProject(automationObject);
                string projectName = ItemContext.ReadReplacement(replacementsDictionary,
                    "$projectname$", "$safeprojectname$");
                if (string.IsNullOrWhiteSpace(projectName)) projectName = target?.Name ?? "";
                Model.SetProjectName(projectName);
                Context.CaptureProject(target);

                var dialog = new ItemDialog(Model);
                try
                {
                    var hwnd = NativeMethods.GetForegroundWindow();
                    if (hwnd != IntPtr.Zero) new WindowInteropHelper(dialog).Owner = hwnd;
                }
                catch { /* owner is optional */ }

                if (dialog.ShowDialog() != true)
                    throw new WizardCancelledException();

                InjectFiles(replacementsDictionary);
            }
            catch (WizardCancelledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ItemContext.Log("RunStarted", ex);
                MessageBox.Show(ex.Message, "ObjectARX Wizard", MessageBoxButton.OK, MessageBoxImage.Warning);
                throw new WizardCancelledException();
            }
        }

        void InjectFiles(Dictionary<string, string> replacementsDictionary)
        {
            // Models that derive symbols from the selected values recompute them here, so the
            // rendered text is correct even when the dialog was never shown (offline smoke test).
            Model.OnFieldsChanged();

            for (int i = 0; i < Model.Files.Count; i++)
            {
                var file = Model.Files[i];
                string content = Model.ShouldGenerate(file)
                    ? TemplateRenderer.Render(ReadResource(Model.ResourceNameOf(file)), Model.Symbols)
                    : "";
                // The keys must carry the surrounding '$' delimiters: the engine replaces the
                // literal key text, so a bare key would leave the two '$' behind.
                replacementsDictionary["$ArxWrap" + i + "File$"] = Model.FileNameOf(file);
                replacementsDictionary["$ArxWrap" + i + "Content$"] = TemplateRenderer.EscapeDollars(content);
            }
        }

        public bool ShouldAddProjectItem(string filePath)
        {
            var file = Model?.FindByWrapper(Path.GetFileName(filePath));
            return file == null || Model.ShouldGenerate(file);
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            try { Context.CaptureProject(projectItem?.ContainingProject); }
            catch (Exception ex) { ItemContext.Log("ProjectItemFinishedGenerating", ex); }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }

        public void ProjectFinishedGenerating(Project project) => Context.CaptureProject(project);

        public void RunFinished()
        {
            try { RunPostActions(); }
            catch (Exception ex) { ItemContext.Log("RunFinished", ex); }
        }

        /// <summary>
        /// The project "Add New Item" was invoked on - the one selected in Solution Explorer.
        /// Returns null when DTE is unavailable; the post actions then fall back to the project
        /// captured from ProjectItemFinishedGenerating.
        /// </summary>
        static Project FindTargetProject(object automationObject)
        {
            try
            {
                if (automationObject is DTE dte && dte.ActiveSolutionProjects is Array projects && projects.Length > 0)
                    return (Project)projects.GetValue(0);
            }
            catch (Exception ex)
            {
                ItemContext.Log("FindTargetProject", ex);
            }
            return null;
        }

        internal static string ReadResource(string logicalName)
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
                // The old wizard templates were ANSI (Windows-1252) files.
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
        }
    }
}