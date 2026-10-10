// Runtime context of an item wizard run: the replacements dictionary supplied by
// VS, the target project captured once VS reports the generated item, and the
// shared log file used by the (try/catch protected) post actions.
using System;
using System.Collections.Generic;
using System.IO;
using EnvDTE;

namespace ArxVsixWizard.Items
{
    public sealed class ItemContext
    {
        public Project Project;
        public string TargetDir;

        /// <summary>First non-empty value among <paramref name="keys"/>.</summary>
        public static string ReadReplacement(Dictionary<string, string> replacements, params string[] keys)
        {
            if (replacements == null) return "";
            foreach (var key in keys)
                if (replacements.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            return "";
        }

        public void CaptureProject(Project project)
        {
            if (project == null) return;
            try
            {
                Project = project;
                TargetDir = Path.GetDirectoryName(project.FullName);
            }
            catch { /* DTE can throw while the project is being created */ }
        }

        /// <summary>
        /// Names of the project items the last item-wizard run generated. Visual Studio reports them
        /// through ProjectItemFinishedGenerating; the "Add ObjectARX Class..." command uses them to
        /// check that the add really reached the project, because the template engine can fail in its
        /// own tail (after the files exist) - see AddArxItemCommand.
        /// </summary>
        static readonly List<string> GeneratedNames = new List<string>();

        public static void ResetGeneratedItems()
        {
            lock (GeneratedNames) GeneratedNames.Clear();
        }

        public static void NoteGeneratedItem(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            lock (GeneratedNames) GeneratedNames.Add(name);
        }

        public static string[] GeneratedItemsSnapshot()
        {
            lock (GeneratedNames) return GeneratedNames.ToArray();
        }

        public static void Log(string stage, Exception ex)
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
    }
}