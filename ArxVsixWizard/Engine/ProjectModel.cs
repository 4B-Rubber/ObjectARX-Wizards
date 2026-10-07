// Builds the wizard symbol table, the file generation plan, and the vcxproj
// ItemGroup XML - C# port of the two default.js OnFinish() implementations.
using System;
using System.Collections.Generic;
using System.Text;
using ArxVsixWizard.Models;

namespace ArxVsixWizard.Engine
{
    public enum ItemKind { Compile, Include, Resource, Midl, None }

    public sealed class PlanEntry
    {
        public string ResourceName; // embedded resource logical name
        public string TargetName;   // file name created in the project folder
        public ItemKind Kind;
        public bool CreatePch;
        public bool NoPch;
    }

    public sealed class ProjectModel
    {
        public WizardOptions Options;
        public string ProjectName;      // raw name typed in the New Project dialog
        public string SafeName;
        public SymbolTable Symbols = new SymbolTable();
        public List<PlanEntry> Files = new List<PlanEntry>();
        public VersionFragments Versions;

        public const string ResourcePrefixArx = "ArxApp.";
        public const string ResourcePrefixOmf = "OmfApp.";
        string Prefix => Options.Kind == WizardKind.ArxApp ? ResourcePrefixArx : ResourcePrefixOmf;

        public static ProjectModel Build(WizardOptions options, string projectName)
        {
            var m = new ProjectModel { Options = options, ProjectName = projectName };
            m.SafeName = Names.SafeName(projectName);
            m.BuildSymbols();
            m.BuildPlan();
            return m;
        }

        void Bool(string name, bool v) => Symbols.Set(name, v);
        void Str(string name, string v) => Symbols.Set(name, v ?? "");

        void BuildSymbols()
        {
            var o = Options;
            string p = ProjectName, safe = SafeName;

            Str("PROJECT_NAME", p);
            Str("UPPER_CASE_PROJECT_NAME", p.ToUpperInvariant());
            Str("SAFE_PROJECT_NAME", safe);
            Str("UPPER_CASE_SAFE_PROJECT_NAME", safe.ToUpperInvariant());
            Str("RDS_SYMB", o.Rds ?? "");
            Bool("UNICODE", true);

            // Application type
            Bool("APP_ARX_TYPE", o.AppType == AppType.Arx);
            Bool("APP_DBX_TYPE", o.AppType == AppType.Dbx);
            Bool("APP_CRX_TYPE", o.AppType == AppType.Crx);

            // MFC
            Bool("NO_MFC", o.Mfc == MfcSupport.None);
            Bool("MFC_REG_STATIC", o.Mfc == MfcSupport.RegularStatic);
            Bool("MFC_REG_SHARED", o.Mfc == MfcSupport.RegularShared);
            Bool("MFC_EXT_SHARED", o.Mfc == MfcSupport.ExtensionShared);
            Bool("ACAD_EXT", o.AcadMfcExtension);

            // COM
            Bool("NO_COM_SERVER", o.ComServer == ComServer.None);
            Bool("STD_COM_SERVER", o.ComServer == ComServer.Standard);
            Bool("ATL_COM_SERVER", o.ComServer == ComServer.Atl);
            Bool("ACAD_ATL_EXT", o.AcadAtlExtension);
            Bool("NO_COM_IMPORT", o.ComImport == ComImport.None);
            Bool("DBX_COM_IMPORT", o.ComImport == ComImport.Dbx);
            Bool("ACAD_COM_IMPORT", o.ComImport == ComImport.Acad);

            // .NET mixed module
            Bool("DOTNET_MODULE", o.DotNetModule);
            Bool("DOTNET_ACA", o.DotNetAca);
            Bool("DOTNET_MEP", o.DotNetMep);

            // OMF verticals / fixed legacy symbols
            Bool("OMF_APP", o.OmfApp);
            Bool("OMF_APP60", o.OmfApp);
            Bool("MAP_API", o.MapApi);
            Bool("MAP_API_2010", o.MapApi);
            Bool("MAP_API_2007", false);
            Bool("MAP_API_2008", false);
            Bool("SUPPORT_COMPONENT_REGISTRAR", true);
            Bool("ATTRIBUTED", false);
            Bool("IMPL_DEBUG", o.ImplementDebug);

            bool mfcSupport = o.Mfc != MfcSupport.None;
            Str("ARX_MFC_SUPPORT", mfcSupport ? "Dynamic" : "false");
            Str("ARX_ATL_SUPPORT", o.ComServer == ComServer.Atl ? "Dynamic" : "false");

            string prjType;
            if (o.Kind == WizardKind.ArxApp)
            {
                if (o.DotNetModule)
                    prjType = o.AppType == AppType.Arx ? "arxnet"
                            : o.AppType == AppType.Dbx ? "dbxnet" : "crxnet";
                else
                    prjType = o.AppType == AppType.Arx ? "arx"
                            : o.AppType == AppType.Dbx ? "dbx" : "crx";
            }
            else
            {
                prjType = o.AppType == AppType.Arx
                    ? (o.DotNetModule ? "arxnet" : "arx")
                    : (o.DotNetModule ? "dbxnet" : "dbx");
            }
            Str("PRJ_TYPE_APP", prjType);

            if (o.Kind == WizardKind.OmfApp && o.OmfApp)
            {
                Str("ARX_OMF_DEFS", o.AppType == AppType.Arx ? "_OMFAPP;USE_ACAD_MODELER;" : "_OMFAPP;USE_ACAD_MODELER;DBX;");
                Str("ARX_OMF_ZM", " -Zm1000");
            }
            else
            {
                Str("ARX_OMF_DEFS", "");
                Str("ARX_OMF_ZM", "");
            }

            // Multi-year configuration fragments
            Versions = VersionXml.Build(o.SelectedYears, o.DotNetModule);

            // GUIDs: FormatGuid(guid,0) renders bare uppercase registry format
            Str("PROJECT_GUID", Guid.NewGuid().ToString("D").ToUpperInvariant());
            Str("PROJECT_RES_GUID", Guid.NewGuid().ToString("D").ToUpperInvariant());
            Str("LIBID_REGISTRY_FORMAT", Guid.NewGuid().ToString("D").ToUpperInvariant());
            Str("APPID_REGISTRY_FORMAT", Guid.NewGuid().ToString("D").ToUpperInvariant());
            Str("COMPREG_REGISTRY_FORMAT", Guid.NewGuid().ToString("D").ToUpperInvariant());

            Str("ARX_PROPS_DIR", ArxVersionTable.PropsDir);
        }

        void Add(string resource, string target, ItemKind kind, bool createPch = false, bool noPch = false)
            => Files.Add(new PlanEntry { ResourceName = Prefix + resource, TargetName = target, Kind = kind, CreatePch = createPch, NoPch = noPch });

        void BuildPlan()
        {
            string p = ProjectName;
            var o = Options;

            Add("StdAfx.cpp", "StdAfx.cpp", ItemKind.Compile, createPch: true);
            Add("StdAfx.h", "StdAfx.h", ItemKind.Include);

            if (o.DotNetModule)
                Add("AssemblyInfo.cpp", "AssemblyInfo.cpp", ItemKind.Compile, noPch: true);

            bool docData = o.Kind == WizardKind.ArxApp
                ? (o.AppType != AppType.Dbx)               // ARX || CRX
                : (o.AppType == AppType.Arx && !o.OmfApp); // ARX && !OMF
            if (docData)
            {
                Add("DocData.cpp", "DocData.cpp", ItemKind.Compile);
                Add("DocData.h", "DocData.h", ItemKind.Include);
            }

            Add("acrxEntryPoint.cpp", "acrxEntryPoint.cpp", ItemKind.Compile);

            if (o.Kind == WizardKind.OmfApp)
            {
                if (o.AppType == AppType.Arx && o.MapApi)
                    Add("mapHeaders.h", "mapHeaders.h", ItemKind.Include);
                if (o.OmfApp && !o.DotNetModule)
                {
                    // OMF GetTargetName only strips the leading "Root"/"omf" case-sensitively;
                    // the source files keep their original "Omf*" names.
                    Add("OmfApp.cpp", "OmfApp.cpp", ItemKind.Compile);
                    Add("OmfApp.h", "OmfApp.h", ItemKind.Include);
                }
                if (o.OmfApp)
                    Add("OmfHeaders.h", "OmfHeaders.h", ItemKind.Include);
            }

            Add("Resource.h", "Resource.h", ItemKind.Include);

            // Root.* files are renamed to <project>.* (raw project name, like the old wizard)
            Add(o.Kind == WizardKind.ArxApp ? "root.cpp" : "Root.cpp", p + ".cpp", ItemKind.Compile);
            Add(o.Kind == WizardKind.ArxApp ? "root.rc" : "Root.rc", p + ".rc", ItemKind.Resource);

            if (o.ComServer != ComServer.None)
                Add(o.Kind == WizardKind.ArxApp ? "root.idl" : "Root.idl", p + ".idl", ItemKind.Midl);
            if (o.ComServer == ComServer.Atl)
                Add(o.Kind == WizardKind.ArxApp ? "root.rgs" : "Root.rgs", p + ".rgs", ItemKind.None);

            if (o.Kind == WizardKind.ArxApp)
                Add("ReadMe.txt", "ReadMe.txt", ItemKind.None);
        }

        /// <summary>Custom template parameters (without surrounding $) for the vcxproj skeleton.</summary>
        public Dictionary<string, string> BuildReplacementParameters()
        {
            string netRefXml = Options.DotNetModule
                ? "\r\n  <!-- .NET Framework mixed modules (up to 2024): the Autodesk net props do not\r\n"
                + "       reference System.Core, required by mgdinterop.h (System.Dynamic). -->\r\n"
                + "  <ItemGroup Condition=\"" + Versions.NetFrameworkCondition + "\">\r\n"
                + "    <Reference Include=\"System.Core\" />\r\n  </ItemGroup>\r\n"
                : "";

            return new Dictionary<string, string>
            {
                ["ArxConfigXml"] = Versions.ConfigXml,
                ["ArxYearRegex"] = Versions.YearRegex,
                ["ArxToolsetXml"] = Versions.ToolsetXml,
                ["ArxClrXml"] = Versions.ClrXml,
                ["ArxNetRefXml"] = netRefXml,
                ["ArxItemsXml"] = BuildItemsXml(),
                ["ArxOmfDefs"] = Symbols.GetString("ARX_OMF_DEFS"),
                ["ArxOmfZm"] = Symbols.GetString("ARX_OMF_ZM"),
                ["ArxMfc"] = Symbols.GetString("ARX_MFC_SUPPORT"),
                ["ArxAtl"] = Symbols.GetString("ARX_ATL_SUPPORT"),
                ["ArxPrjType"] = Symbols.GetString("PRJ_TYPE_APP"),
                ["ArxRds"] = Symbols.GetString("RDS_SYMB"),
                ["ArxPropsDir"] = ArxVersionTable.PropsDir,
                ["ArxProjectGuid"] = Symbols.GetString("PROJECT_GUID"),
            };
        }

        string ItemTag(PlanEntry e)
        {
            switch (e.Kind)
            {
                case ItemKind.Compile: return "ClCompile";
                case ItemKind.Include: return "ClInclude";
                case ItemKind.Resource: return "ResourceCompile";
                case ItemKind.Midl: return "Midl";
                default: return "None";
            }
        }

        public string BuildItemsXml()
        {
            var sb = new StringBuilder();
            foreach (var e in Files)
            {
                string tag = ItemTag(e);
                bool needsMetadata = e.CreatePch || e.NoPch;
                sb.Append("    <").Append(tag).Append(" Include=\"").Append(e.TargetName).Append('"');
                if (!needsMetadata) { sb.Append(" />\r\n"); continue; }
                sb.Append(">\r\n");
                if (e.CreatePch) sb.Append("      <PrecompiledHeader>Create</PrecompiledHeader>\r\n");
                if (e.NoPch) sb.Append("      <PrecompiledHeader>NotUsing</PrecompiledHeader>\r\n");
                sb.Append("    </").Append(tag).Append(">\r\n");
            }
            return sb.ToString();
        }

        public string BuildFiltersXml()
        {
            const string sourceFilter = "{4FC737F1-C7A5-4376-A066-2A32D752A2FF}";
            const string headerFilter = "{93995380-89BD-4b04-88EB-625FBE52EBFB}";
            const string resourceFilter = "{67DA6AB4-FEE4-482A-A7E9-A3F38417069D}";

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n");
            sb.Append("<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n");
            sb.Append("  <ItemGroup>\r\n");
            sb.Append("    <Filter Include=\"Source Files\"><UniqueIdentifier>").Append(sourceFilter).Append("</UniqueIdentifier></Filter>\r\n");
            sb.Append("    <Filter Include=\"Header Files\"><UniqueIdentifier>").Append(headerFilter).Append("</UniqueIdentifier></Filter>\r\n");
            sb.Append("    <Filter Include=\"Resource Files\"><UniqueIdentifier>").Append(resourceFilter).Append("</UniqueIdentifier></Filter>\r\n");
            sb.Append("  </ItemGroup>\r\n  <ItemGroup>\r\n");
            foreach (var e in Files)
            {
                string tag = ItemTag(e);
                string filter;
                switch (e.Kind)
                {
                    case ItemKind.Include: filter = "Header Files"; break;
                    case ItemKind.Resource:
                    case ItemKind.Midl:
                    case ItemKind.None: filter = "Resource Files"; break;
                    default: filter = "Source Files"; break;
                }
                sb.Append("    <").Append(tag).Append(" Include=\"").Append(e.TargetName)
                  .Append("\"><Filter>").Append(filter).Append("</Filter></").Append(tag).Append(">\r\n");
            }
            sb.Append("  </ItemGroup>\r\n</Project>\r\n");
            return sb.ToString();
        }
    }
}
