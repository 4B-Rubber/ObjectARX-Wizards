// ArxAtlWizDynProp: AutoCAD object dynamic property wizard.
//
// Port of ArxAtlWizDynProp\HTML\1033\default.htm (OnShortName/OnCoclass) and
// Scripts\1033\default.js. Same name rules as the COM wrapper wizard, plus the OPM
// (dynamic property) support flags that drive dynprop.h/dynprop.cpp.
using System;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class AtlDynPropItemModel : ItemModel
    {
        // Embedded resource names registered by the csproj from Items\AtlDynProp\Templates\*.
        internal const string HeaderResource = "ArxWizAtlDynProp.dynprop.h";
        internal const string ImplResource = "ArxWizAtlDynProp.dynprop.cpp";
        internal const string RgsResource = "ArxWizAtlDynProp.dynprop.rgs";
        internal const string ConnPtResource = "ArxWizAtlDynProp.connpt.h";
        internal const string InterfaceIdlResource = "ArxWizAtlDynProp.dynpropint.idl";
        internal const string CoclassIdlResource = "ArxWizAtlDynProp.dynpropco.idl";

        // Priority order used to collapse the five threading check boxes to one value: apartment
        // is the default and always yields, so anything else the user just ticked wins.
        static readonly string[] ThreadingPriority =
        {
            "THREADING_APARTMENT", "THREADING_SINGLE", "THREADING_BOTH", "THREADING_FREE", "THREADING_NEUTRAL",
        };

        readonly string _clsid = NewGuid();
        readonly string _interfaceIid = NewGuid();
        readonly string _connectionPointIid = NewGuid();
        readonly string _libid = NewGuid();
        readonly string _appid = NewGuid();

        string _lastShortName;
        string _lastCoclass;
        string _lastProjectName;

        public AtlDynPropItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "AutoCAD Object Dynamic Property";
            Description = "Creates an ATL COM object that implements IDynamicProperty2 so the "
                        + "AutoCAD Properties palette shows your custom (dynamic) property. "
                        + "Class = C + Short, CoClass = Short, Interface = I + Short.";

            AddField(new ItemField
            {
                Symbol = "SHORT_NAME",
                Label = "Short name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Base for all other names (the value you would put after the leading C/I).",
            });
            AddField(new ItemField
            {
                Symbol = "CLASS_NAME",
                Label = "Class",
                Required = true,
                DerivedFrom = "SHORT_NAME",
                ToolTip = "Name of the new ATL class (C + short name).",
            });
            AddField(new ItemField
            {
                Symbol = "COCLASS",
                Label = "Coclass",
                DerivedFrom = "SHORT_NAME",
                ToolTip = "Name of the coclass in the .idl file.",
            });
            AddField(new ItemField
            {
                Symbol = "INTERFACE_NAME",
                Label = "Interface",
                DerivedFrom = "SHORT_NAME",
                ToolTip = "Name of the interface this class implements (I + short name).",
            });
            AddField(new ItemField
            {
                Symbol = "HEADER_FILE",
                Label = ".h file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "SHORT_NAME",
                DerivedSuffix = ".h",
                ToolTip = "Header file where the class is declared.",
            });
            AddField(new ItemField
            {
                Symbol = "IMPL_FILE",
                Label = ".cpp file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "SHORT_NAME",
                DerivedSuffix = ".cpp",
                ToolTip = "Implementation file for the class.",
            });
            AddField(new ItemField
            {
                Symbol = "TYPE_NAME",
                Label = "Type",
                DerivedFrom = "COCLASS",
                DerivedSuffix = " Class",
                ToolTip = "Help string for the coclass.",
            });
            AddField(new ItemField
            {
                Symbol = "VERSION_INDEPENDENT_PROGID",
                Label = "ProgID",
                DerivedFrom = "COCLASS",
                ToolTip = "Version independent programmatic ID (SafeProjectName up to 30 chars + . + coclass, "
                        + "then truncated to 39).",
            });
            AddField(new ItemField
            {
                Symbol = "PROGID",
                Label = "Versioned ProgID",
                DerivedFrom = "VERSION_INDEPENDENT_PROGID",
                ToolTip = "Versioned programmatic ID (version independent ProgID truncated to 37 + \".1\").",
            });
            AddField(new ItemField
            {
                Symbol = "ARX_CLASS_NAME",
                Label = "ObjectARX class for the property",
                Required = true,
                ToolTip = "The ObjectARX/Dbx class this dynamic property applies to "
                        + "(used by OPM_DYNPROP_OBJECT_ENTRY_AUTO).",
            });

            AddField(new ItemField { Symbol = "ATTRIBUTED", Label = "Attributed", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "CONNECTION_POINTS", Label = "Connection points", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "SUPPORT_ERROR_INFO", Label = "Support error info", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "FREE_THREADED_MARSHALER", Label = "Free threaded marshaler", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OBJECT_WITH_SITE", Label = "Object with site", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AGGREGATION_NO", Label = "Not aggregatable", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AGGREGATION_ONLY", Label = "Only aggregatable", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AUTOMATION", Label = "Automation (oleautomation)", Kind = ItemFieldKind.Bool });

            // Dynamic property (OPM) extras.
            AddField(new ItemField { Symbol = "OPM_CAT", Label = "Categorized property (ICategorizeProperties)", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OPM_DYNENUM", Label = "Enumerated property (IDynamicEnumProperty)", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OPM_DYNDLG", Label = "Custom dialog property (IDynamicDialogProperty)", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OPM_NOADDSUPP", Label = "No additional property interface", Kind = ItemFieldKind.Bool, Default = "true" });

            AddField(new ItemField { Symbol = "THREADING_SINGLE", Label = "Threading: single", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_APARTMENT", Label = "Threading: apartment", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "THREADING_BOTH", Label = "Threading: both", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_FREE", Label = "Threading: free", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_NEUTRAL", Label = "Threading: neutral", Kind = ItemFieldKind.Bool });

            AddField(new ItemField { Symbol = "INTERFACE_DUAL", Label = "Dual interface", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "INTERFACE_CUSTOM", Label = "Custom interface", Kind = ItemFieldKind.Bool });

            AddField(new ItemField { Symbol = "TYPELIB_VERSION_MAJOR", Label = "Type library major version", Default = "1" });
            AddField(new ItemField { Symbol = "TYPELIB_VERSION_MINOR", Label = "Type library minor version", Default = "0" });

            // Project items, order must match AtlDynProp.vstemplate (Wrap0..Wrap3).
            AddFile("Wrap0.txt", HeaderResource, "DynProp.h", "HEADER_FILE");
            AddFile("Wrap1.txt", ImplResource, "DynProp.cpp", "IMPL_FILE");
            AddFile("Wrap2.txt", RgsResource, "DynProp.rgs", "RGS_FILE");
            AddFile("Wrap3.txt", ConnPtResource, "DynProp_Events_CP.h", "CONNPT_FILE", "CONNPT_SKIP");

            Symbols.Set("CLSID_REGISTRY_FORMAT", _clsid);
            Symbols.Set("INTERFACE_IID", _interfaceIid);
            Symbols.Set("CONNECTION_POINT_IID", _connectionPointIid);
            Symbols.Set("LIBID_REGISTRY_FORMAT", _libid);
            Symbols.Set("APPID_REGISTRY_FORMAT", _appid);
            Symbols.Set("DLL_APP", true);
            Symbols.Set("APPID_EXIST", false);

            Initialize();
        }

        public override void OnFieldsChanged()
        {
            base.OnFieldsChanged();

            string shortName = (Symbols.GetString("SHORT_NAME") ?? "").Trim();
            string projectName = ProjectName ?? "";
            bool shortChanged = !string.Equals(shortName, _lastShortName, StringComparison.Ordinal);
            bool projectChanged = !string.Equals(projectName, _lastProjectName, StringComparison.Ordinal);

            if (shortChanged || string.IsNullOrEmpty(Symbols.GetString("CLASS_NAME")))
                Symbols.Set("CLASS_NAME", shortName.Length > 0 ? "C" + shortName : "");
            if (shortChanged || string.IsNullOrEmpty(Symbols.GetString("COCLASS")))
                Symbols.Set("COCLASS", shortName);
            if (shortChanged || string.IsNullOrEmpty(Symbols.GetString("INTERFACE_NAME")))
                Symbols.Set("INTERFACE_NAME", shortName.Length > 0 ? "I" + shortName : "");

            Symbols.Set("UPPER_SHORT_NAME", shortName.ToUpperInvariant());
            Symbols.Set("RGS_ID", "IDR_" + shortName.ToUpperInvariant());
            Symbols.Set("RGS_FILE", shortName.Length > 0 ? shortName + ".rgs" : "");

            string interfaceName = Symbols.GetString("INTERFACE_NAME");
            Symbols.Set("CONNPT_FILE", shortName.Length > 0 ? "_" + interfaceName + "Events_CP.h" : "");
            Symbols.Set("CONNPT_SKIP", Symbols.GetBool("CONNECTION_POINTS") ? "1" : "");

            string coclass = (Symbols.GetString("COCLASS") ?? "").Trim();
            bool coclassChanged = !string.Equals(coclass, _lastCoclass, StringComparison.Ordinal);
            if (coclass.Length == 0)
            {
                Symbols.Set("TYPE_NAME", "");
                Symbols.Set("VERSION_INDEPENDENT_PROGID", "");
                Symbols.Set("PROGID", "");
            }
            else if (coclassChanged || projectChanged
                     || string.IsNullOrEmpty(Symbols.GetString("VERSION_INDEPENDENT_PROGID")))
            {
                Symbols.Set("TYPE_NAME", coclass + " Class");
                string safeProject = Truncate(Names.SafeName(projectName), 30);
                string vip = Truncate(safeProject + "." + coclass, 39);
                Symbols.Set("VERSION_INDEPENDENT_PROGID", vip);
                Symbols.Set("PROGID", Truncate(vip, 37) + ".1");
            }

            Symbols.Set("LIB_NAME", Names.SafeName(projectName) + "Lib");

            NormalizeThreading();
            NormalizeInterface();

            _lastShortName = shortName;
            _lastCoclass = coclass;
            _lastProjectName = projectName;
        }

        public override string Validate()
        {
            string error = base.Validate();
            if (error != null) return error;
            if (string.IsNullOrWhiteSpace(Symbols.GetString("ARX_CLASS_NAME")))
                return "Specify the ObjectARX class the dynamic property applies to.";
            return null;
        }

        /// <summary>
        /// Exactly one of the five threading check boxes stays set (they were radios in the old
        /// wizard). The dialog does not refresh check boxes, so a deterministic priority is used:
        /// the highest entry that is set wins, and apartment is the fallback.
        /// </summary>
        void NormalizeThreading()
        {
            string keep = null;
            foreach (string model in ThreadingPriority)
                if (Symbols.GetBool(model)) keep = model;
            if (keep == null) keep = "THREADING_APARTMENT";

            foreach (string model in ThreadingPriority)
                Symbols.Set(model, model == keep);
        }

        void NormalizeInterface()
        {
            bool dual = Symbols.GetBool("INTERFACE_DUAL");
            bool custom = Symbols.GetBool("INTERFACE_CUSTOM");
            if (custom) dual = false;
            else if (!dual) dual = true;
            Symbols.Set("INTERFACE_DUAL", dual);
            Symbols.Set("INTERFACE_CUSTOM", custom);
        }

        static string Truncate(string value, int length)
            => value.Length <= length ? value : value.Substring(0, length);

        static string NewGuid()
            => Guid.NewGuid().ToString("D").ToUpperInvariant();
    }
}