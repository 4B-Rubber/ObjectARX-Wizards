// ArxWizCustomObject: ObjectDBX custom object (custom entity) class wizard.
//
// The base class list comes from DbxObjects.xml; the chosen entry drives the include
// header, the template pair and the protocol matrix, exactly like OnBase/SetSymbols in
// ArxWizCustomObject\HTML\1033\default.htm:529-552.
using System.Collections.Generic;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class CustomObjectItemModel : ItemModel
    {
        const string CatalogResource = "ArxWizData.DbxObjects.xml";

        readonly List<CatalogEntry> _catalog = new List<CatalogEntry>();
        readonly List<string> _derivable = new List<string>();

        public CustomObjectItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "ObjectDBX Custom Object Class";
            Description = "Creates a class derived from an ObjectDBX class (the basis of a custom "
                        + "entity). The protocol level is taken from the chosen base class.";

            foreach (var entry in ItemCatalog.Load(CatalogResource))
            {
                _catalog.Add(entry);
                if (!string.Equals(entry.Flag, "X", System.StringComparison.OrdinalIgnoreCase))
                    _derivable.Add(entry.Name);
            }

            AddField(new ItemField
            {
                Symbol = "CLASS_NAME",
                Label = "Class name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Name of the new ObjectDBX class.",
            });
            AddField(new ItemField
            {
                Symbol = "BASE_CLASS",
                Label = "Base class",
                Kind = ItemFieldKind.Combo,
                Choices = _derivable,
                Default = _derivable.Count > 0 ? _derivable[0] : "AcDbObject",
                Required = true,
                ToolTip = "ObjectDBX class the new class derives from.",
            });
            AddField(new ItemField
            {
                Symbol = "DXFNAME",
                Label = "DXF name",
                Required = true,
                DerivedFrom = "CLASS_NAME",
                StripLeadingC = true,
                Uppercase = true,
                ToolTip = "DXF record name registered by ACRX_DXF_DEFINE_MEMBERS.",
            });
            AddField(new ItemField
            {
                Symbol = "APPNAME",
                Label = "Logical application name",
                Required = true,
                ToolTip = "Used to register the DXF record with the application.",
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
                Symbol = "DWG_PROTOCOL",
                Label = "Object is writable to DWG files (dwgOut/dwgIn)",
                Kind = ItemFieldKind.Bool,
                Default = "true",
            });
            AddField(new ItemField { Symbol = "DXF_PROTOCOL", Label = "Object participates in DXF in/out", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OSNAP_PROTOCOL", Label = "Object provides object snap points", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "GRIPPOINT_PROTOCOL", Label = "Object provides grip points", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "IMPL_VIEWPORT", Label = "Object is viewport dependent", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "CURVE_PROTOCOL", Label = "Object implements the curve protocol", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "PERSISTENT_REACTOR", Label = "Persistent reactor support", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "SELF_REACTOR", Label = "Self reactor support", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "DEEPCLONE", Label = "Deep clone support", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "WBLOCKCLONE", Label = "WBLOCK clone support", Kind = ItemFieldKind.Bool });

            AddSymbolicFile("Wrap0.txt", "TEMPLATE_HEADER", "Object.h", "HEADER_FILE");
            AddSymbolicFile("Wrap1.txt", "TEMPLATE_IMPL", "Object.cpp", "IMPL_FILE");

            Initialize();
        }

        public override void OnFieldsChanged()
        {
            base.OnFieldsChanged();

            var entry = FindEntry(Symbols.GetString("BASE_CLASS"));
            if (entry != null)
            {
                Symbols.Set("INCLUDE_HEADER", entry.Header);
                Symbols.Set("TEMPLATE_HEADER", "ArxWizCustomObject." + entry.Template + ".h");
                Symbols.Set("TEMPLATE_IMPL", "ArxWizCustomObject." + entry.Template + ".cpp");

                // PROTOCOLS 1/2/3 -> AcDbObject / AcDbEntity / AcDbCurve, cumulative (default.js:29-51).
                int protocols = entry.Int("protocol", 1);
                Symbols.Set("PROTOCOLS", protocols.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Symbols.Set("ACDBOBJECT_PROTOCOLS", protocols >= 1);
                Symbols.Set("ACDBENTITY_PROTOCOLS", protocols >= 2);
                Symbols.Set("ACDBCURVE_PROTOCOLS", protocols >= 3);
            }

            // The old wizard forced the automation option to "none" (default.htm:531).
            Symbols.Set("AUTOMATION", false);

            if (string.IsNullOrWhiteSpace(Symbols.GetString("APPNAME")))
                Symbols.Set("APPNAME", Symbols.GetString("UPPER_CASE_SAFE_PROJECT_NAME") + "APP");
        }

        public override string Validate()
        {
            string error = base.Validate();
            if (error != null) return error;
            if (FindEntry(Symbols.GetString("BASE_CLASS")) == null)
                return "Select a base class.";
            return null;
        }

        CatalogEntry FindEntry(string name)
        {
            foreach (var entry in _catalog)
                if (string.Equals(entry.Name, name, System.StringComparison.Ordinal))
                    return entry;
            return null;
        }
    }
}