// ArxWizReactors: ObjectARX/DBX transient reactor class wizard.
//
// The base class list comes from reactors.xml; the chosen entry drives the include
// header and the template pair, exactly like SetSymbols in
// ArxWizReactors\HTML\1033\default.htm:437-445. Several display names share one
// template (aliases such as AcEditorReactor2/3), so the entry is looked up by name.
using System;
using System.Collections.Generic;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class ReactorsItemModel : ItemModel
    {
        const string CatalogResource = "ArxWizData.reactors.xml";

        readonly List<CatalogEntry> _catalog = new List<CatalogEntry>();

        public ReactorsItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "ObjectARX/DBX Transient Reactors Class";
            Description = "Creates a class that inherits from an ObjectARX/DBX transient reactor class.";

            foreach (var entry in ItemCatalog.Load(CatalogResource))
                _catalog.Add(entry);

            AddField(new ItemField
            {
                Symbol = "CLASS_NAME",
                Label = "Class name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Name of the new reactor class.",
            });
            AddField(new ItemField
            {
                Symbol = "BASE_CLASS",
                Label = "Base class",
                Kind = ItemFieldKind.Combo,
                Choices = BaseClassNames(),
                Default = _catalog.Count > 0 ? _catalog[0].Name : "AcEditorReactor",
                Required = true,
                ToolTip = "ObjectARX/DBX transient reactor class the new class derives from.",
            });
            AddField(new ItemField
            {
                Symbol = "HEADER_FILE",
                Label = ".h file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "CLASS_NAME",
                DerivedSuffix = ".h",
                StripLeadingC = true,
                ToolTip = "Header file where the class is declared.",
            });
            AddField(new ItemField
            {
                Symbol = "IMPL_FILE",
                Label = ".cpp file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "CLASS_NAME",
                DerivedSuffix = ".cpp",
                StripLeadingC = true,
                ToolTip = "Implementation file for the class.",
            });

            AddSymbolicFile("Wrap0.txt", "TEMPLATE_HEADER", "Reactor.h", "HEADER_FILE");
            AddSymbolicFile("Wrap1.txt", "TEMPLATE_IMPL", "Reactor.cpp", "IMPL_FILE");

            Initialize();
        }

        public override void OnFieldsChanged()
        {
            base.OnFieldsChanged();

            // The Combo may not have published BASE_CLASS yet on the first call (its default
            // comes from the dialog), so fall back to the first table entry instead of failing.
            var entry = FindEntry(Symbols.GetString("BASE_CLASS"));
            if (entry == null)
                entry = _catalog.Count > 0 ? _catalog[0] : null;

            if (entry != null)
            {
                Symbols.Set("INCLUDE_HEADER", entry.Header);
                // reactors.xml stores the template base name including its "_tmpl" suffix, so
                // the embedded resource is "ArxWizReactors.<templatefile>.h" (default.htm:441).
                Symbols.Set("TEMPLATE_HEADER", "ArxWizReactors." + entry.Template + ".h");
                Symbols.Set("TEMPLATE_IMPL", "ArxWizReactors." + entry.Template + ".cpp");
            }

            Symbols.Set("CLASS_NAME_ROOT", ClassNameRoot());
        }

        public override string Validate()
        {
            string error = base.Validate();
            if (error != null) return error;
            if (string.IsNullOrWhiteSpace(Symbols.GetString("BASE_CLASS")))
                return "Select a base class.";
            return null;
        }

        /// <summary>Base class names in reactors.xml order (aliases included).</summary>
        List<string> BaseClassNames()
        {
            var names = new List<string>();
            foreach (var entry in _catalog)
                names.Add(entry.Name);
            return names;
        }

        CatalogEntry FindEntry(string name)
        {
            foreach (var entry in _catalog)
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            return null;
        }

        /// <summary>Safe form of the class name the templates declare (default.htm:444).</summary>
        string ClassNameRoot()
        {
            // default.htm:444 - CreateSafeName(CLASS_NAME) with no leading-C removal, so the
            // generated class keeps the name the user typed even when the files drop the "C".
            return Names.SafeName(Symbols.GetString("CLASS_NAME"));
        }
    }
}