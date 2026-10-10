// Locates and parses the item .vstemplate files deployed with the VSIX, so the picker
// dialog lists exactly what the installed extension ships - adding a template to
// Packaging\ItemTemplates automatically adds it to the dialog, nothing else to update.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ArxVsixWizard.Commands
{
    // Public because the (public) picker dialog exposes it; the catalog itself stays internal.
    public sealed class ArxItemTemplateInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string DefaultName { get; set; } = "";
        public string IconPath { get; set; }
        public string VsTemplatePath { get; set; } = "";
        public int SortOrder { get; set; } = 1000;
    }

    internal static class ArxItemTemplateCatalog
    {
        /// <summary>Directory the VSIX payload was installed into (the assembly sits at its root).</summary>
        public static string ExtensionDir
            => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>Every item template under <extensionDir>\ItemTemplates, sorted for display.</summary>
        public static List<ArxItemTemplateInfo> Load()
        {
            var list = new List<ArxItemTemplateInfo>();
            try
            {
                string root = Path.Combine(ExtensionDir, "ItemTemplates");
                if (Directory.Exists(root))
                    foreach (var path in Directory.GetFiles(root, "*.vstemplate", SearchOption.AllDirectories))
                    {
                        var info = Parse(path);
                        if (info != null) list.Add(info);
                    }
            }
            catch (Exception ex)
            {
                WizardDiagnostics.Log("TemplateCatalog", "scan failed: " + ex.Message);
            }
            list.Sort((a, b) => a.SortOrder != b.SortOrder
                ? a.SortOrder - b.SortOrder
                : string.CompareOrdinal(a.Name, b.Name));
            WizardDiagnostics.Log("TemplateCatalog", "templates=" + list.Count);
            return list;
        }

        static ArxItemTemplateInfo Parse(string path)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("vt", "http://schemas.microsoft.com/developer/vstemplate/2005");
                var root = doc.SelectSingleNode("/vt:VSTemplate", nsmgr);
                if (root == null) return null;
                if (!string.Equals(root.Attributes?["Type"]?.Value, "Item", StringComparison.OrdinalIgnoreCase))
                    return null;

                var data = root.SelectSingleNode("vt:TemplateData", nsmgr);
                if (data == null) return null;

                var info = new ArxItemTemplateInfo
                {
                    VsTemplatePath = path,
                    Name = Text(data, nsmgr, "vt:Name"),
                    Description = Text(data, nsmgr, "vt:Description"),
                    DefaultName = Text(data, nsmgr, "vt:DefaultName"),
                };
                if (int.TryParse(Text(data, nsmgr, "vt:SortOrder"), out int order))
                    info.SortOrder = order;

                string icon = Text(data, nsmgr, "vt:Icon");
                if (!string.IsNullOrEmpty(icon))
                {
                    string iconPath = Path.Combine(Path.GetDirectoryName(path), icon);
                    if (File.Exists(iconPath)) info.IconPath = iconPath;
                }
                return string.IsNullOrEmpty(info.Name) ? null : info;
            }
            catch (Exception ex)
            {
                WizardDiagnostics.Log("TemplateCatalog", "parse failed " + Path.GetFileName(path) + ": " + ex.Message);
                return null;
            }
        }

        static string Text(XmlNode parent, XmlNamespaceManager nsmgr, string xpath)
            => parent.SelectSingleNode(xpath, nsmgr)?.InnerText.Trim() ?? "";
    }
}
