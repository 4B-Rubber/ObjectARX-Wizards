// ArxWizMFCSupport: ObjectARX MFC class wizard.
//
// The base class list comes from MfcSupport.xml; the chosen entry drives the include
// header, the template pair and the resource/child-dialog behaviour, exactly like
// OnBase/OnClass/SetSymbols in ArxWizMFCSupport\HTML\1033\default.htm (589-706).
using System;
using System.Collections.Generic;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class MfcSupportItemModel : ItemModel
    {
        const string CatalogResource = "ArxWizData.MfcSupport.xml";

        readonly List<CatalogEntry> _entries = new List<CatalogEntry>();
        readonly List<string> _baseNames = new List<string>();
        readonly List<string> _filters = new List<string>();

        public MfcSupportItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "ObjectARX MFC Class";
            Description = "Creates a class derived from an ObjectARX AdUi/AcUi MFC class. "
                        + "Resource based base classes can inject a dialog resource into the project.";

            foreach (var entry in ItemCatalog.Load(CatalogResource))
            {
                if (string.Equals(entry.ElementName, "Filter", StringComparison.OrdinalIgnoreCase))
                    _filters.Add(entry.Name);
                else if (string.Equals(entry.ElementName, "Entry", StringComparison.OrdinalIgnoreCase))
                {
                    _entries.Add(entry);
                    _baseNames.Add(entry.Name);
                }
            }
            if (_filters.Count == 0) _filters.Add("All");

            AddField(new ItemField
            {
                Symbol = "CLASS_NAME",
                Label = "Class name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Name of the new ObjectARX MFC class.",
            });
            AddField(new ItemField
            {
                Symbol = "BASE_CLASS",
                Label = "Base class",
                Kind = ItemFieldKind.Combo,
                Choices = new List<string>(_baseNames),
                // The list is rebuilt by the dialog whenever the filter changes.
                ChoicesFrom = "FILTER_BASE",
                Default = _baseNames.Count > 0 ? _baseNames[0] : "",
                Required = true,
                ToolTip = "ObjectARX MFC class the new class derives from.",
            });
            AddField(new ItemField
            {
                Symbol = "FILTER_BASE",
                Label = "Filter base classes by",
                Kind = ItemFieldKind.Combo,
                Choices = new List<string>(_filters),
                Default = _filters[0],
                ToolTip = "Restricts the base class list to one functional group.",
            });
            AddField(new ItemField
            {
                Symbol = "IDD_DIALOG",
                Label = "Dialog ID",
                ToolTip = "Dialog resource ID. Required for resource based base classes and "
                        + "injected into the project's .rc/Resource.h files.",
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
            AddField(new ItemField
            {
                Symbol = "CREATE_DIALOG",
                Label = "Create dialog resource in the project",
                Kind = ItemFieldKind.Bool,
                ToolTip = "Adds the dialog resource to the project's .rc and Resource.h files. The "
                        + "wizard does this automatically when the id is not defined yet, so the "
                        + "generated class always compiles.",
            });

            // Order matters: it must match the <ProjectItem> order in MfcSupport.vstemplate.
            AddSymbolicFile("Wrap0.txt", "TEMPLATE_HEADER", "Dialog.h", "HEADER_FILE");
            AddSymbolicFile("Wrap1.txt", "TEMPLATE_IMPL", "Dialog.cpp", "IMPL_FILE");
            AddFile("Wrap2.txt", "ArxWizMfcSupport.ChildDialog.h", "ChildDlg.h",
                "CHILDHEADER_FILE", "CHILD_DIALOG_NEEDED");
            AddFile("Wrap3.txt", "ArxWizMfcSupport.ChildDialog.cpp", "ChildDlg.cpp",
                "CHILDIMPL_FILE", "CHILD_DIALOG_NEEDED");

            Initialize();
        }

        public override void OnFieldsChanged()
        {
            base.OnFieldsChanged();

            // The generic dialog cannot repopulate a combo live, so the filter only clamps the
            // current selection into the requested group (0 = All keeps every base class).
            ApplyFilter();

            var entry = FindEntry(Symbols.GetString("BASE_CLASS"));
            if (entry != null)
            {
                Symbols.Set("INCLUDE_HEADER", entry.Header);
                Symbols.Set("TEMPLATE_HEADER", "ArxWizMfcSupport." + entry.Template + ".h");
                Symbols.Set("TEMPLATE_IMPL", "ArxWizMfcSupport." + entry.Template + ".cpp");

                // Flag semantics (OnBase, default.htm:589-628): "C" needs a child dialog and
                // falls through to "R"; "RCW" needs the child dialog *resource* instead.
                string flag = entry.Flag ?? "";
                bool hasId = !string.IsNullOrWhiteSpace(Symbols.GetString("IDD_DIALOG"));

                bool childDlg = string.Equals(flag, "C", StringComparison.OrdinalIgnoreCase) && hasId;
                bool childRes = string.Equals(flag, "RCW", StringComparison.OrdinalIgnoreCase);

                // The string value drives ShouldGenerate (SkipSymbol); the bool drives [!if].
                Symbols.Set("CHILD_DIALOG_NEEDED", childDlg ? "1" : "");
                Symbols.Set("CHILD_DIALOG_NEEDED", childDlg);
                Symbols.Set("CHILD_RESOURCE_NEEDED", childRes ? "1" : "");
                Symbols.Set("CHILD_RESOURCE_NEEDED", childRes);
            }
            else
            {
                Symbols.Set("CHILD_DIALOG_NEEDED", "");
                Symbols.Set("CHILD_DIALOG_NEEDED", false);
                Symbols.Set("CHILD_RESOURCE_NEEDED", "");
                Symbols.Set("CHILD_RESOURCE_NEEDED", false);
            }

            // default.htm:548 keeps the class name as typed, but the child dialog *files* drop the
            // leading C (default.htm:566-581), just like the header/implementation pair above.
            string className = Symbols.GetString("CLASS_NAME").Trim();
            string classNameRoot = className;
            if (classNameRoot.Length > 0 && (classNameRoot[0] == 'C' || classNameRoot[0] == 'c'))
                classNameRoot = classNameRoot.Substring(1);
            Symbols.Set("CHILDCLASS_NAME", className + "ChildDlg");
            Symbols.Set("CHILDHEADER_FILE", classNameRoot + "ChildDlg.h");
            Symbols.Set("CHILDIMPL_FILE", classNameRoot + "ChildDlg.cpp");

            // default.htm:568/576 - the dialog id is pre-filled from the class name when empty.
            if (string.IsNullOrWhiteSpace(Symbols.GetString("IDD_DIALOG")) && classNameRoot.Length > 0)
                Symbols.Set("IDD_DIALOG", "IDD_" + classNameRoot.ToUpperInvariant());

            // CreateSafeName() minus the leading C (SetSymbols, default.htm:701-705).
            string root = Names.SafeName(className);
            if (root.Length > 0 && (root[0] == 'C' || root[0] == 'c')) root = root.Substring(1);
            Symbols.Set("CLASS_NAME_ROOT", root);

            // The renderer has no "!=" operator, so the FileDialog template branch
            // ([!if CLASS_NAME != CAdUiFileDialog]) is precomputed here.
            Symbols.Set("NOT_CADUI_FILEDIALOG",
                !string.Equals(className, "CAdUiFileDialog", StringComparison.Ordinal));
        }

        public override string Validate()
        {
            string error = base.Validate();
            if (error != null) return error;

            var entry = FindEntry(Symbols.GetString("BASE_CLASS"));
            if (entry == null) return "Select a base class.";

            bool hasId = !string.IsNullOrWhiteSpace(Symbols.GetString("IDD_DIALOG"));
            string flag = entry.Flag ?? "";
            bool resourceBased = string.Equals(flag, "R", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(flag, "RCW", StringComparison.OrdinalIgnoreCase);
            if (resourceBased && !hasId)
                return "Dialog ID is required for this base class.";
            if (Symbols.GetBool("CREATE_DIALOG") && !hasId)
                return "Enter a dialog ID so the resource can be created.";
            return null;
        }

        /// <summary>Keeps BASE_CLASS inside the group selected by FILTER_BASE (0 = All).</summary>
        void ApplyFilter()
        {
            var allowed = AllowedBaseClasses();
            if (allowed.Count == 0) return;

            // FilterLevel is 1:1 with the filter index (1..11), 0 selects every entry.
            if (!allowed.Contains(Symbols.GetString("BASE_CLASS")))
                Symbols.Set("BASE_CLASS", allowed[0]);
        }

        /// <summary>Base classes belonging to the group selected by FILTER_BASE.</summary>
        List<string> AllowedBaseClasses()
        {
            int index = FilterIndex();
            var allowed = new List<string>();
            foreach (var entry in _entries)
                if (index == 0 || entry.Int("FilterLevel", 0) == index)
                    allowed.Add(entry.Name);
            return allowed;
        }

        int FilterIndex()
        {
            string filter = Symbols.GetString("FILTER_BASE");
            for (int i = 0; i < _filters.Count; i++)
                if (string.Equals(_filters[i], filter, StringComparison.Ordinal))
                    return i;
            return 0;
        }

        /// <summary>The dialog rebuilds the base class combo from this whenever the filter changes.</summary>
        public override List<string> GetChoices(ItemField field)
        {
            if (!string.Equals(field.Symbol, "BASE_CLASS", StringComparison.Ordinal))
                return base.GetChoices(field);
            var allowed = AllowedBaseClasses();
            return allowed.Count > 0 ? allowed : base.GetChoices(field);
        }

        CatalogEntry FindEntry(string name)
        {
            foreach (var entry in _entries)
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            return null;
        }
    }
}