// Loads the XML tables shipped with the wizards (reactors.xml, DbxObjects.xml,
// MfcSupport.xml) and exposes them as a flat list of attribute bags. The three files
// use different element/attribute spellings, so lookups are case-insensitive and every
// attribute is kept.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ArxVsixWizard.Items
{
    public sealed class CatalogEntry
    {
        readonly Dictionary<string, string> _attributes;

        public CatalogEntry(string elementName, Dictionary<string, string> attributes)
        {
            ElementName = elementName;
            _attributes = attributes;
        }

        public string ElementName { get; }

        public string Get(params string[] names)
        {
            foreach (var name in names)
                if (_attributes.TryGetValue(name, out string v))
                    return v;
            return "";
        }

        public string Name => Get("name");
        public string Header => Get("header");
        public string Template => Get("templatefile", "template");
        public string Flag => Get("flag");

        public bool IsTrue(string name)
        {
            string v = Get(name);
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        public int Int(string name, int fallback)
            => int.TryParse(Get(name), out int v) ? v : fallback;

        public override string ToString() => Name;
    }

    public static class ItemCatalog
    {
        static readonly Dictionary<string, List<CatalogEntry>> Cache =
            new Dictionary<string, List<CatalogEntry>>(StringComparer.Ordinal);

        /// <summary>Parses an embedded XML resource, returning every element that has a "name" attribute.</summary>
        public static List<CatalogEntry> Load(string logicalName)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(logicalName, out var cached)) return cached;
                var entries = Parse(ReadResource(logicalName));
                Cache[logicalName] = entries;
                return entries;
            }
        }

        static List<CatalogEntry> Parse(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var list = new List<CatalogEntry>();
            Walk(doc.DocumentElement, list);
            return list;
        }

        static void Walk(XmlElement element, List<CatalogEntry> list)
        {
            if (element == null) return;
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlAttribute a in element.Attributes) attributes[a.Name] = a.Value;
            if (attributes.ContainsKey("name"))
                list.Add(new CatalogEntry(element.Name, attributes));
            foreach (XmlNode child in element.ChildNodes)
                if (child is XmlElement childElement)
                    Walk(childElement, list);
        }

        static string ReadResource(string logicalName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(logicalName))
            {
                if (s == null)
                    throw new InvalidOperationException("Embedded data resource not found: " + logicalName);
                using (var reader = new StreamReader(s, System.Text.Encoding.GetEncoding(1252)))
                    return reader.ReadToEnd();
            }
        }
    }
}