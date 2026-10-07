// ArxAtlWizComWrapper: AutoCAD COM wrapper object wizard.
//
// Port of ArxAtlWizComWrapper\HTML\1033\default.htm (OnShortName/OnCoclass) and
// Scripts\1033\default.js (CreateGUIDs + the file/resource side effects that now
// live in Items\AtlCommon\AtlItemSupport.cs). The class names all follow SHORT_NAME:
//   CLASS_NAME = "C" + SHORT_NAME, COCLASS = SHORT_NAME, INTERFACE_NAME = "I" + SHORT_NAME.
using System;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class AtlComWrapperItemModel : ItemModel
    {
        // Embedded resource names registered by the csproj from Items\AtlComWrapper\Templates\*.
        internal const string HeaderResource = "ArxWizAtlComWrapper.object.h";
        internal const string ImplResource = "ArxWizAtlComWrapper.object.cpp";
        internal const string RgsResource = "ArxWizAtlComWrapper.object.rgs";
        internal const string ConnPtResource = "ArxWizAtlComWrapper.connpt.h";
        internal const string CoclassIdlResource = "ArxWizAtlComWrapper.objco.idl";

        // Priority order used to collapse the five threading check boxes to one value: apartment
        // is the default and always yields, so anything else the user just ticked wins.
        static readonly string[] ThreadingPriority =
        {
            "THREADING_APARTMENT", "THREADING_SINGLE", "THREADING_BOTH", "THREADING_FREE", "THREADING_NEUTRAL",
        };

        // GUIDS are created once per model (default.js CreateGUIDs) so they stay stable while
        // the user edits the dialog. Format "D" upper case: no braces, as the ATL templates
        // add the braces where they need them (object.rgs: "{[!output CLSID_REGISTRY_FORMAT]}").
        readonly string _clsid = NewGuid();
        readonly string _interfaceIid = NewGuid();
        readonly string _connectionPointIid = NewGuid();
        readonly string _libid = NewGuid();
        readonly string _appid = NewGuid();

        string _lastShortName;
        string _lastCoclass;
        string _lastProjectName;

        public AtlComWrapperItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "AutoCAD COM Wrapper Object";
            Description = "Wraps an ObjectARX/DBX class in an ATL COM coclass so that automation "
                        + "clients (VBA, .NET, COM) can create and use it. The names all follow the "
                        + "short name: Class = C + Short, CoClass = Short, Interface = I + Short.";

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
                Label = "ObjectARX class to be wrapped",
                Required = true,
                ToolTip = "The ObjectARX/Dbx class that the COM wrapper creates and exposes.",
            });

            AddField(new ItemField { Symbol = "ATTRIBUTED", Label = "Attributed", Kind = ItemFieldKind.Bool,
                ToolTip = "Generate attribute based declarations instead of a registry resource." });
            AddField(new ItemField { Symbol = "CONNECTION_POINTS", Label = "Connection points", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "SUPPORT_ERROR_INFO", Label = "Support error info", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "FREE_THREADED_MARSHALER", Label = "Free threaded marshaler", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OBJECT_WITH_SITE", Label = "Object with site", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AGGREGATION_NO", Label = "Not aggregatable", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AGGREGATION_ONLY", Label = "Only aggregatable", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "AUTOMATION", Label = "Automation (oleautomation)", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "ACAD_ENTITY_INTERFACE", Label = "Acad entity interface (IAcadEntity)", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OPM_PROPERTY_EXTENSION", Label = "OPM property extension", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "OPM_PROPERTY_EXPANDER", Label = "OPM property expander", Kind = ItemFieldKind.Bool });

            // ItemField has no radio kind, so the five threading models are exclusive check boxes
            // kept consistent by OnFieldsChanged (last checked wins).
            AddField(new ItemField { Symbol = "THREADING_SINGLE", Label = "Threading: single", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_APARTMENT", Label = "Threading: apartment", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "THREADING_BOTH", Label = "Threading: both", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_FREE", Label = "Threading: free", Kind = ItemFieldKind.Bool });
            AddField(new ItemField { Symbol = "THREADING_NEUTRAL", Label = "Threading: neutral", Kind = ItemFieldKind.Bool });

            AddField(new ItemField { Symbol = "INTERFACE_DUAL", Label = "Dual interface", Kind = ItemFieldKind.Bool, Default = "true" });
            AddField(new ItemField { Symbol = "INTERFACE_CUSTOM", Label = "Custom interface", Kind = ItemFieldKind.Bool });

            AddField(new ItemField { Symbol = "TYPELIB_VERSION_MAJOR", Label = "Type library major version", Default = "1" });
            AddField(new ItemField { Symbol = "TYPELIB_VERSION_MINOR", Label = "Type library minor version", Default = "0" });

            // Project items, order must match AtlComWrapper.vstemplate (Wrap0..Wrap3).
            AddFile("Wrap0.txt", HeaderResource, "Object.h", "HEADER_FILE");
            AddFile("Wrap1.txt", ImplResource, "Object.cpp", "IMPL_FILE");
            AddFile("Wrap2.txt", RgsResource, "Object.rgs", "RGS_FILE");
            AddFile("Wrap3.txt", ConnPtResource, "Object_Events_CP.h", "CONNPT_FILE", "CONNPT_SKIP");

            // Auto generated GUIDs and fixed flags (default.js CreateGUIDs:99-119).
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

            // OnShortName(): the class names and file names all follow SHORT_NAME. A manually
            // edited value survives until SHORT_NAME itself changes (the old wizard did the same).
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

            // OnCoclass(): TYPE_NAME / VERSION_INDEPENDENT_PROGID / PROGID follow COCLASS.
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
                return "Specify the ObjectARX class to be wrapped.";
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

        /// <summary>Dual and custom are exclusive; at least one is always selected.</summary>
        void NormalizeInterface()
        {
            bool dual = Symbols.GetBool("INTERFACE_DUAL");
            bool custom = Symbols.GetBool("INTERFACE_CUSTOM");
            if (custom) dual = false;             // a freshly picked custom interface wins
            else if (!dual) dual = true;          // never leave both unselected
            Symbols.Set("INTERFACE_DUAL", dual);
            Symbols.Set("INTERFACE_CUSTOM", custom);
        }

        static string Truncate(string value, int length)
            => value.Length <= length ? value : value.Substring(0, length);

        static string NewGuid()
            => Guid.NewGuid().ToString("D").ToUpperInvariant();
    }
}