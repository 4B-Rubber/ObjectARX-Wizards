// "Add ObjectARX Class..." on the Solution Explorer project context menu.
//
// Why this command exists: Visual Studio's own Add New Item dialog calls
// ServiceHelper.GenerateItemName when a pre-filling template is selected, and that call
// throws inside the IDE while a freshly created ObjectARX project is still warming up
// (802 headers to index); with Visual Assist installed the process dies outright. The
// defect is in Microsoft.VisualStudio.Dialogs, not fixable from here - the only robust
// way out is to never open that dialog. This command shows a small picker of the item
// templates the extension ships, invents the default name itself, and hands the run to
// the template engine through ProjectItems.AddFromTemplate, which drives the same seven
// IWizard implementations the dialog would have (docs\VS-AddNewItem-Crash.md).
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using ArxVsixWizard.UI;
using Task = System.Threading.Tasks.Task;

namespace ArxVsixWizard.Commands
{
    internal sealed class AddArxItemCommand
    {
        // EnvDTE Project.Kind of a VC++ (vcxproj) project.
        const string VcProjectKind = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        // Marker every project our project templates generate carries (ArxProject.vcxproj /
        // OmfProject.vcxproj); the old .vsz wizard's projects have it too.
        const string ArxProjectMarker = "<ArxAppType>";

        readonly AsyncPackage _package;
        readonly DTE _dte;

        // vcxproj path -> (last write, is ObjectARX project); BeforeQueryStatus runs on every
        // context-menu open, so the marker scan result is cached until the file changes.
        static readonly Dictionary<string, Tuple<DateTime, bool>> MarkerCache =
            new Dictionary<string, Tuple<DateTime, bool>>(StringComparer.OrdinalIgnoreCase);

        AddArxItemCommand(AsyncPackage package, DTE dte, OleMenuCommandService menuService)
        {
            _package = package;
            _dte = dte;
            var command = new OleMenuCommand(OnExecute,
                new CommandID(PackageGuids.CmdSetGuid, PackageIds.AddArxItemCommandId));
            command.BeforeQueryStatus += OnBeforeQueryStatus;
            menuService.AddCommand(command);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var menuService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE;
            if (menuService == null || dte == null)
            {
                WizardDiagnostics.Log("AddArxItem", "init failed: menuService=" + (menuService != null)
                    + " dte=" + (dte != null));
                return;
            }
            new AddArxItemCommand(package, dte, menuService);
            WizardDiagnostics.Log("AddArxItem", "command registered");
        }

        void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var command = (OleMenuCommand)sender;
            command.Visible = IsArxProject(SelectedProject());
        }

        void OnExecute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Project project = SelectedProject();
            if (!IsArxProject(project))
            {
                WizardDiagnostics.Log("AddArxItem", "execute ignored: no ObjectARX project selected");
                return;
            }

            var templates = ArxItemTemplateCatalog.Load();
            if (templates.Count == 0)
            {
                MessageBox.Show("No ObjectARX item templates were found next to the extension.",
                    "ObjectARX Wizard", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string projectDir;
            try { projectDir = Path.GetDirectoryName(project.FullName); }
            catch (Exception ex) { ItemContextLog("Execute/projectDir", ex); return; }

            var dialog = new AddItemDialog(templates, projectDir);
            try
            {
                IntPtr owner = WizardDiagnostics.PreferredOwner();
                if (owner != IntPtr.Zero) new WindowInteropHelper(dialog).Owner = owner;
            }
            catch (Exception ex) { WizardDiagnostics.Log("AddArxItem", "owner failed: " + ex.Message); }

            if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null)
            {
                WizardDiagnostics.Log("AddArxItem", "picker cancelled");
                return;
            }

            var template = dialog.SelectedTemplate;
            string name = dialog.ItemName;
            WizardDiagnostics.Log("AddArxItem", "template=" + template.Name + " name=" + name
                + " project=" + SafeProjectName(project) + " " + WizardDiagnostics.WindowState());
            Items.ItemContext.ResetGeneratedItems();
            try
            {
                SaveBeforeAdd(project);
                project.ProjectItems.AddFromTemplate(template.VsTemplatePath, name);
                WizardDiagnostics.Log("AddArxItem", "done name=" + name);
                EnsureItemsInProject(project);
            }
            catch (Exception ex)
            {
                // Cancelling the wizard's own option page surfaces here as E_ABORT; that is a
                // normal exit, not an error.
                if (ex.HResult == unchecked((int)0x80004004) /* E_ABORT */)
                {
                    WizardDiagnostics.Log("AddArxItem", "wizard cancelled");
                }
                // OLE_E_PROMPTSAVECANCELLED (0x8004000C): the save prompt the template engine raised
                // while adding was declined. E_FAIL (0x80004005): the same tail step failed without a
                // prompt. Either way the wizard has already generated its files (measured: MyJig.h/.cpp
                // on disk while the vcxproj stayed untouched), so recover the tail here instead of
                // showing the user a raw HRESULT.
                else if (ex.HResult == unchecked((int)0x8004000C) || ex.HResult == unchecked((int)0x80004005))
                {
                    WizardDiagnostics.Log("AddArxItem", "engine tail failed: hr=0x"
                        + ex.HResult.ToString("X8") + "; recovering");
                    EnsureItemsInProject(project);
                }
                else
                {
                    WizardDiagnostics.Log("AddArxItem", "failed: " + ex);
                    MessageBox.Show(ex.Message, "ObjectARX Wizard", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Saves the project (and the solution when it has a path) without any prompt. The template
        /// engine saves as part of adding an item; left to itself that turns unsaved changes into a
        /// modal save prompt whose dismissal surfaces as OLE_E_PROMPTSAVECANCELLED, and the item never
        /// reaches the project. Saving first makes the engine's own save a no-op.
        /// </summary>
        void SaveBeforeAdd(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (!project.Saved)
                {
                    project.Save();
                    WizardDiagnostics.Log("AddArxItem", "project saved before the add");
                }
            }
            catch (Exception ex) { ItemContextLog("SaveBeforeAdd/project", ex); }
            try
            {
                Solution solution = _dte?.Solution;
                if (solution != null && !solution.Saved && !string.IsNullOrEmpty(solution.FullName))
                {
                    solution.SaveAs(solution.FullName);
                    WizardDiagnostics.Log("AddArxItem", "solution saved before the add");
                }
            }
            catch (Exception ex) { ItemContextLog("SaveBeforeAdd/solution", ex); }
        }

        /// <summary>
        /// Makes sure every item the wizard generated is in the project, adding the file directly when
        /// the engine's own add did not get that far, then saving the project. A no-op when the engine
        /// did its job.
        /// </summary>
        void EnsureItemsInProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var generated = Items.ItemContext.GeneratedItemsSnapshot();
                if (generated.Length == 0) return;
                var missing = new List<string>();
                foreach (string itemName in generated)
                {
                    bool present = false;
                    foreach (ProjectItem existing in project.ProjectItems)
                    {
                        if (string.Equals(existing.Name, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            present = true;
                            break;
                        }
                    }
                    if (!present) missing.Add(itemName);
                }
                if (missing.Count == 0)
                {
                    WizardDiagnostics.Log("AddArxItem", "items already in the project: " + generated.Length);
                    return;
                }

                string dir = Path.GetDirectoryName(project.FullName);
                int added = 0;
                foreach (string itemName in missing)
                {
                    string file = Path.Combine(dir ?? "", itemName);
                    if (!File.Exists(file)) continue;
                    project.ProjectItems.AddFromFile(file);
                    added++;
                    WizardDiagnostics.Log("AddArxItem", "added from file: " + itemName);
                }
                if (added > 0)
                {
                    project.Save();
                    WizardDiagnostics.Log("AddArxItem", "project saved after adding " + added + " item(s)");
                }
            }
            catch (Exception ex) { ItemContextLog("EnsureItemsInProject", ex); }
        }

        /// <summary>The project the context menu was opened on (first of the active solution projects).</summary>
        Project SelectedProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (_dte.ActiveSolutionProjects is Array projects && projects.Length > 0)
                    return projects.GetValue(0) as Project;
            }
            catch (Exception ex) { ItemContextLog("SelectedProject", ex); }
            return null;
        }

        /// <summary>True for a VC++ project whose vcxproj carries our ArxAppType marker.</summary>
        static bool IsArxProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null) return false;
            string path;
            try
            {
                if (!string.Equals(project.Kind, VcProjectKind, StringComparison.OrdinalIgnoreCase))
                    return false;
                path = project.FullName;
            }
            catch { return false; }
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) return false;
                if (MarkerCache.TryGetValue(path, out var cached) && cached.Item1 == file.LastWriteTimeUtc)
                    return cached.Item2;
                // The marker sits in the Globals PropertyGroup near the top; 64 KB is plenty and
                // keeps the check cheap on every menu open.
                string head;
                using (var reader = file.OpenText())
                {
                    var buffer = new char[65536];
                    int read = reader.Read(buffer, 0, buffer.Length);
                    head = new string(buffer, 0, read);
                }
                bool isArx = head.IndexOf(ArxProjectMarker, StringComparison.Ordinal) >= 0;
                MarkerCache[path] = Tuple.Create(file.LastWriteTimeUtc, isArx);
                return isArx;
            }
            catch { return false; }
        }

        static string SafeProjectName(Project project)
        {
            try { return project?.Name ?? "?"; }
            catch { return "?"; }
        }

        static void ItemContextLog(string stage, Exception ex)
            => Items.ItemContext.Log(stage, ex);
    }
}
