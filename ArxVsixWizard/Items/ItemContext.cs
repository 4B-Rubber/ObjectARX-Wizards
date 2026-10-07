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