// End-to-end test: render the full project (vcxproj + files) to a temp folder
// for several option sets, then verify the project file is well-formed XML.
// (Actual compilation is done outside this EXE.)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using ArxVsixWizard.Engine;
using ArxVsixWizard.Models;

class Program
{
    static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : @"D:\Demo\wiztest\vsix_gen";
        var asm = Assembly.LoadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArxVsixWizard.dll"));
        Directory.CreateDirectory(outDir);
        int projFailures = 0;

        var sets = GetSets();
        foreach (var set in sets)
        {
            var opts = set.Opts;
            var model = ProjectModel.Build(opts, set.Name);
            var dir = Path.Combine(outDir, set.Name);
            Directory.CreateDirectory(dir);

            // Write files
            foreach (var f in model.Files)
            {
                string src;
                using (var s = asm.GetManifestResourceStream(f.ResourceName))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    src = Encoding.GetEncoding(1252).GetString(ms.ToArray());
                }
                string text = TemplateRenderer.Render(src, model.Symbols);
                File.WriteAllText(Path.Combine(dir, f.TargetName), text, Encoding.UTF8);
            }

            // Build vcxproj from skeleton (embedded resource)
            string skelName = model.Options.Kind == WizardKind.ArxApp
                ? "ArxApp.Skeleton.vcxproj"
                : "OmfApp.Skeleton.vcxproj";
            string skeleton;
            using (var s = asm.GetManifestResourceStream(skelName))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                skeleton = Encoding.UTF8.GetString(ms.ToArray());
            }
            string projText = skeleton;
            foreach (var kv in model.BuildReplacementParameters())
                projText = projText.Replace("$" + kv.Key + "$", kv.Value);
            string projPath = Path.Combine(dir, set.Name + ".vcxproj");
            File.WriteAllText(projPath, projText, new UTF8Encoding(false));

            // SDK props may be missing when the installer did not generate that year: the import must
            // be Exists()-guarded and a Chinese error must replace the raw MSB4019 failure.
            Check(ref projFailures,
                projText.Contains("Exists(") && projText.Contains("Autodesk.arx-$(ArxYear).props')"),
                set.Name + " -> props Import guarded by Exists()");
            Check(ref projFailures, projText.Contains("Name=\"CheckArxSdkProps\""),
                set.Name + " -> missing-props target CheckArxSdkProps present");
            File.WriteAllText(projPath + ".filters", model.BuildFiltersXml(), new UTF8Encoding(false));

            // OMF: resource subproject
            if (set.Opts.Kind == WizardKind.OmfApp && set.Opts.OmfApp)
            {
                string enu = Path.Combine(dir, "Enu");
                Directory.CreateDirectory(enu);
                RenderTo(asm, "OmfApp.OmfEnuRes.h", Path.Combine(enu, "Resource.h"), model.Symbols);
                RenderTo(asm, "OmfApp.OmfEnuRes.rc", Path.Combine(enu, set.Name + "Enu.rc"), model.Symbols);
                RenderTo(asm, "OmfApp.OmfEnuRes.vcxproj", Path.Combine(enu, set.Name + "Enu.vcxproj"), model.Symbols, true);
                RenderTo(asm, "OmfApp.OmfEnuRes.vcxproj.filters", Path.Combine(enu, set.Name + "Enu.vcxproj.filters"), model.Symbols, true);
            }

            try { new XmlDocument().Load(projPath); Console.WriteLine("OK  xml " + set.Name); }
            catch (Exception ex) { Console.WriteLine("XML FAIL " + set.Name + ": " + ex.Message); }
        }

        int failures = projFailures;
        failures += RunInlineDirectiveChecks();
        failures += RunItemTemplateChecks(outDir);
        Console.WriteLine(failures == 0
            ? "ALL CHECKS PASSED"
            : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    // ---- Phase 1: item wizard model <-> embedded resources <-> .vstemplate cross check ----
    static int RunItemTemplateChecks(string outDir)
    {
        int failures = 0;
        var asm = typeof(ArxVsixWizard.Items.NetWrapperItemWizard).Assembly;
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string itemsDir = Path.Combine(root, "ArxVsixWizard", "Packaging", "ItemTemplates");

        var resources = new HashSet<string>(asm.GetManifestResourceNames());

        // One representative model per packaged item wizard, keyed by its folder name.
        var models = new Dictionary<string, ArxVsixWizard.Items.ItemModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["NetWrapper"] = new ArxVsixWizard.Items.NetWrapperItemModel("MyWrapper"),
            ["Jig"] = new ArxVsixWizard.Items.JigItemModel("CMyJig"),
            ["CustomObject"] = new ArxVsixWizard.Items.CustomObjectItemModel("CMyObject"),
            ["Reactors"] = new ArxVsixWizard.Items.ReactorsItemModel("CMyReactor"),
            ["MfcSupport"] = new ArxVsixWizard.Items.MfcSupportItemModel("CMyMfcClass"),
            ["AtlComWrapper"] = new ArxVsixWizard.Items.AtlComWrapperItemModel("MyComObject"),
            ["AtlDynProp"] = new ArxVsixWizard.Items.AtlDynPropItemModel("MyDynProp"),
        };

        foreach (var pair in models)
        {
            var model = pair.Value;
            model.OnFieldsChanged();

            foreach (var file in model.Files)
            {
                string res = model.ResourceNameOf(file);
                Check(ref failures, resources.Contains(res),
                    pair.Key + " -> embedded resource present: " + res);
                if (!resources.Contains(res)) continue;

                // Render offline: no residual directive, no unsubstituted injection key.
                string text = TemplateRenderer.Render(ReadResource(asm, res), model.Symbols);
                Check(ref failures, !text.Contains("[!"), pair.Key + "/" + file.DefaultFileName + " -> no residual [! directive");
                Check(ref failures, !text.Contains("$ArxWrap"), pair.Key + "/" + file.DefaultFileName + " -> no residual $ArxWrap key");
            }
        }

        // Jig: the multi-line symbols must follow NUMBER_OF_INPUTS.
        var jig = new ArxVsixWizard.Items.JigItemModel("CMyJig");
        jig.Symbols.Set("NUMBER_OF_INPUTS", "3");
        jig.OnFieldsChanged();
        string jigCpp = TemplateRenderer.Render(ReadResource(asm, jig.ResourceNameOf(jig.Files[1])), jig.Symbols);
        Check(ref failures, jigCpp.Contains("AcString inputPrompts [3]"), "Jig -> prompt array sized 3");
        Check(ref failures, jigCpp.Contains("case 3:"), "Jig -> sampler/update switch has case 3");
        Check(ref failures, jigCpp.Contains("CMyJig::CMyJig ()"), "Jig -> class name substituted");
        string jigH = TemplateRenderer.Render(ReadResource(asm, jig.ResourceNameOf(jig.Files[0])), jig.Symbols);
        Check(ref failures, jigH.Contains("class CMyJig : public AcEdJig"), "Jig -> header declares the class");
        Check(ref failures, jigH.Contains("//- MyJig.h"), "Jig -> derived header file name");

        var jigClamped = new ArxVsixWizard.Items.JigItemModel("CMyJig");
        jigClamped.Symbols.Set("NUMBER_OF_INPUTS", "99");
        jigClamped.OnFieldsChanged();
        Check(ref failures, jigClamped.Symbols.GetString("NUMBER_OF_INPUTS") == "20",
            "Jig -> NUMBER_OF_INPUTS clamped to 20");

        // Reactors: the base class selects the include header and the template pair.
        var rea = new ArxVsixWizard.Items.ReactorsItemModel("CMyReactor");
        Check(ref failures, rea.Symbols.GetString("BASE_CLASS") == "AcApProfileManagerReactor",
            "Reactors -> first base class from reactors.xml");
        Check(ref failures, rea.Symbols.GetString("INCLUDE_HEADER") == "acprofile.h",
            "Reactors -> include header from reactors.xml");

        rea.Symbols.Set("BASE_CLASS", "AcDbDatabaseReactor");
        rea.OnFieldsChanged();
        Check(ref failures, rea.Symbols.GetString("INCLUDE_HEADER") == "dbmain.h",
            "Reactors -> switching the base class switches the include header");
        Check(ref failures, rea.Symbols.GetString("TEMPLATE_HEADER") == "ArxWizReactors.AcDbDatabaseReactor_tmpl.h",
            "Reactors -> template resource follows the alias table");
        string reaH = TemplateRenderer.Render(ReadResource(asm, rea.ResourceNameOf(rea.Files[0])), rea.Symbols);
        Check(ref failures, reaH.Contains("class /*DLLIMPEXP*/ CMyReactor : public AcDbDatabaseReactor"),
            "Reactors -> class name kept as typed, base class substituted");

        // CustomObject: DbxObjects.xml drives the include header and the protocol matrix.
        var obj = new ArxVsixWizard.Items.CustomObjectItemModel("CMyObject");
        Check(ref failures, obj.Symbols.GetString("BASE_CLASS") == "AcDbObject",
            "CustomObject -> first derivable base class is AcDbObject");
        Check(ref failures, obj.Symbols.GetString("INCLUDE_HEADER") == "dbmain.h",
            "CustomObject -> include header from the catalogue");
        Check(ref failures, obj.Symbols.GetBool("ACDBOBJECT_PROTOCOLS")
                           && !obj.Symbols.GetBool("ACDBENTITY_PROTOCOLS")
                           && !obj.Symbols.GetBool("ACDBCURVE_PROTOCOLS"),
            "CustomObject -> AcDbObject gives the object protocol only");

        obj.Symbols.Set("BASE_CLASS", "AcDbCurve");
        obj.OnFieldsChanged();
        Check(ref failures, obj.Symbols.GetBool("ACDBOBJECT_PROTOCOLS")
                           && obj.Symbols.GetBool("ACDBENTITY_PROTOCOLS")
                           && obj.Symbols.GetBool("ACDBCURVE_PROTOCOLS"),
            "CustomObject -> AcDbCurve opens all three protocols");
        Check(ref failures, obj.Symbols.GetString("INCLUDE_HEADER") == "dbcurve.h",
            "CustomObject -> AcDbCurve include header");

        string objCpp = TemplateRenderer.Render(ReadResource(asm, obj.ResourceNameOf(obj.Files[1])), obj.Symbols);
        Check(ref failures, objCpp.Contains("MYOBJECT"), "CustomObject -> DXF name derived upper case");
        Check(ref failures, objCpp.Contains("AcDbCurve"), "CustomObject -> base class substituted");

        // MfcSupport: the class table selects the template pair, and the Flag decides whether the
        // child dialog pair joins the file set.
        var mfc = new ArxVsixWizard.Items.MfcSupportItemModel("CMyMfcClass");
        Check(ref failures, mfc.Symbols.GetString("BASE_CLASS") == "CAdUiBaseDialog",
            "MfcSupport -> first class from MfcSupport.xml");
        Check(ref failures, mfc.Symbols.GetString("TEMPLATE_HEADER") == "ArxWizMfcSupport.Dialog.h",
            "MfcSupport -> template pair from the class table");
        Check(ref failures, mfc.Symbols.GetString("INCLUDE_HEADER") == "adui.h",
            "MfcSupport -> include header from the class table");
        Check(ref failures, !mfc.ShouldGenerate(mfc.Files[2]),
            "MfcSupport -> child dialog file skipped by default");
        Check(ref failures, mfc.Files.Count == 4, "MfcSupport -> four planned files");

        mfc.Symbols.Set("BASE_CLASS", "CAdUiDockControlBar");
        // The bool overload, exactly like the dialog's CollectValues() does for a checkbox.
        mfc.Symbols.Set("CREATE_DIALOG", true);
        mfc.Symbols.Set("IDD_DIALOG", "IDD_MYDLG");
        mfc.OnFieldsChanged();
        Check(ref failures, mfc.Symbols.GetBool("CHILD_DIALOG_NEEDED"),
            "MfcSupport -> Flag C with a dialog id needs the child dialog");
        Check(ref failures, mfc.ShouldGenerate(mfc.Files[2]),
            "MfcSupport -> child dialog file generated for Flag C");
        Check(ref failures, mfc.Symbols.GetString("CHILDHEADER_FILE") == "MyMfcClassChildDlg.h",
            "MfcSupport -> child dialog file drops the leading C");
        Check(ref failures, mfc.Symbols.GetString("CHILDCLASS_NAME") == "CMyMfcClassChildDlg",
            "MfcSupport -> child dialog class keeps the leading C");
        Check(ref failures, mfc.Symbols.GetString("TEMPLATE_HEADER") == "ArxWizMfcSupport.DockControlBar.h",
            "MfcSupport -> switching the base class switches the template pair");

        // DockControlBar.cpp is the file that embeds the dialog id.
        string mfcCpp = TemplateRenderer.Render(ReadResource(asm, mfc.ResourceNameOf(mfc.Files[1])), mfc.Symbols);
        Check(ref failures, mfcCpp.Contains("IDD_MYDLG"), "MfcSupport -> dialog id substituted");
        Check(ref failures, !mfcCpp.Contains("[!"), "MfcSupport -> no residual [! directive after a switch");

        failures += RunResourceInjectionCheck(outDir);

        // ATL wizards: name derivation, GUID format, PROGID truncation and the .rgs resource id.
        var com = new ArxVsixWizard.Items.AtlComWrapperItemModel("MyComObject");
        Check(ref failures, com.Symbols.GetString("SHORT_NAME") == "MyComObject",
            "AtlComWrapper -> SHORT_NAME defaults to the item name");
        Check(ref failures, com.Symbols.GetString("CLASS_NAME") == "CMyComObject",
            "AtlComWrapper -> CLASS_NAME is C + SHORT_NAME");
        Check(ref failures, com.Symbols.GetString("INTERFACE_NAME") == "IMyComObject",
            "AtlComWrapper -> INTERFACE_NAME is I + SHORT_NAME");
        Check(ref failures, com.Symbols.GetString("HEADER_FILE") == "MyComObject.h",
            "AtlComWrapper -> header file follows SHORT_NAME");
        Check(ref failures, com.Symbols.GetString("RGS_ID") == "IDR_MYCOMOBJECT",
            "AtlComWrapper -> RGS_ID is IDR_ + upper case SHORT_NAME");
        Check(ref failures, Guid.TryParse(com.Symbols.GetString("INTERFACE_IID"), out _),
            "AtlComWrapper -> INTERFACE_IID is a parseable GUID");
        Check(ref failures, !com.Symbols.GetString("INTERFACE_IID").Contains("{"),
            "AtlComWrapper -> GUIDs are emitted without braces");
        string vip = com.Symbols.GetString("VERSION_INDEPENDENT_PROGID");
        string progid = com.Symbols.GetString("PROGID");
        Check(ref failures, vip.Length <= 39, "AtlComWrapper -> VERSION_INDEPENDENT_PROGID capped at 39");
        Check(ref failures, progid.EndsWith(".1") && progid.Length <= 41,
            "AtlComWrapper -> PROGID is 37 chars plus .1");
        Check(ref failures, !com.ShouldGenerate(com.Files[3]),
            "AtlComWrapper -> connection point header skipped without the option");
        com.Symbols.Set("CONNECTION_POINTS", true);
        com.OnFieldsChanged();
        Check(ref failures, com.ShouldGenerate(com.Files[3]),
            "AtlComWrapper -> connection point header generated when requested");
        Check(ref failures, com.Symbols.GetString("CONNPT_FILE") == "_IMyComObjectEvents_CP.h",
            "AtlComWrapper -> connection point file name");

        string comH = TemplateRenderer.Render(ReadResource(asm, com.ResourceNameOf(com.Files[0])), com.Symbols);
        Check(ref failures, comH.Contains("DECLARE_REGISTRY_RESOURCEID(IDR_MYCOMOBJECT)"),
            "AtlComWrapper -> registry resource id substituted in the header");
        Check(ref failures, comH.Contains("COM_INTERFACE_ENTRY(IConnectionPointContainer)"),
            "AtlComWrapper -> connection point block rendered");

        var dyn = new ArxVsixWizard.Items.AtlDynPropItemModel("MyDynProp");
        Check(ref failures, dyn.Symbols.GetString("CLASS_NAME") == "CMyDynProp",
            "AtlDynProp -> CLASS_NAME is C + SHORT_NAME");
        Check(ref failures, Guid.TryParse(dyn.Symbols.GetString("CLSID_REGISTRY_FORMAT"), out _),
            "AtlDynProp -> CLSID is a parseable GUID");
        dyn.Symbols.Set("ARX_CLASS_NAME", "AcDbMyEntity");
        dyn.OnFieldsChanged();
        string dynH = TemplateRenderer.Render(ReadResource(asm, dyn.ResourceNameOf(dyn.Files[0])), dyn.Symbols);
        Check(ref failures, dynH.Contains("OPM_DYNPROP_OBJECT_ENTRY_AUTO(CMyDynProp, AcDbMyEntity)"),
            "AtlDynProp -> OPM entry macro rendered");

        // NetWrapper specifics.
        var net = new ArxVsixWizard.Items.NetWrapperItemModel("MyWrapper");
        string netHeader = TemplateRenderer.Render(ReadResource(asm, net.ResourceNameOf(net.Files[0])), net.Symbols);
        Check(ref failures, netHeader.Contains("namespace ObjectARX"), "NetWrapper -> company namespace");
        Check(ref failures, netHeader.Contains("ref class MyWrapper"), "NetWrapper -> wrapper class name");
        Check(ref failures, TemplateRenderer.EscapeDollars(netHeader) == netHeader, "NetWrapper -> no '$' to escape");

        // Every packaged .vstemplate must line up with one of the models.
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var templates = Directory.GetFiles(itemsDir, "*.vstemplate", SearchOption.AllDirectories);
        Check(ref failures, templates.Length > 0, "at least one item .vstemplate packaged");
        Check(ref failures, templates.Length == models.Count,
            "one .vstemplate per known item wizard (" + templates.Length + " found, " + models.Count + " expected)");
        foreach (var path in templates)
        {
            string tag = Path.GetFileNameWithoutExtension(path);
            var doc = new XmlDocument();
            doc.Load(path);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("v", "http://schemas.microsoft.com/developer/vstemplate/2005");

            string dir = Path.GetDirectoryName(path);
            Check(ref failures, Attr(doc, ns, "/v:VSTemplate/v:TemplateData/v:Name").Length > 0, tag + " -> Name");
            Check(ref failures, Attr(doc, ns, "/v:VSTemplate/@Type") == "Item", tag + " -> Type=Item");
            Check(ref failures, Attr(doc, ns, "/v:VSTemplate/v:TemplateData/v:TemplateGroupID").Length > 0,
                tag + " -> TemplateGroupID");

            // Item templates must ask for a prefilled name: the wizard's preset (<DefaultName>) is
            // what the user should see in the Name box. The price is that selecting a template makes
            // Visual Studio call Microsoft.VisualStudio.Dialogs.ServiceHelper.GenerateItemName, and
            // that call fails while a freshly created project is still being parsed (UIA
            // ElementNotAvailable on 18.10, E_FAIL on 17.14 - identical stack). With Visual Assist
            // installed it becomes a devenv crash, without it the dialog closes silently.
            // Practical rule: after creating a project, wait for the status bar to show "Ready"
            // (「就绪」) before adding an item. See docs\VS-AddNewItem-Crash.md.
            Check(ref failures, Attr(doc, ns, "/v:VSTemplate/v:TemplateData/v:ProvideDefaultName") == "true",
                tag + " -> ProvideDefaultName=true (the wizard's preset name is pre-filled)");

            string id = Attr(doc, ns, "/v:VSTemplate/v:TemplateData/v:TemplateID");
            Check(ref failures, id.Length > 0 && seenIds.Add(id), tag + " -> unique TemplateID (" + id + ")");

            // A stub icon (the 60-byte placeholder that used to sit next to every template) makes the
            // whole list look alike in VS, which no automated check noticed. Size is a crude but
            // sufficient test: the shipped wizard icons are 766 bytes and up.
            string icon = Attr(doc, ns, "/v:VSTemplate/v:TemplateData/v:Icon");
            string iconPath = Path.Combine(dir, icon);
            Check(ref failures, icon.Length > 0 && File.Exists(iconPath), tag + " -> icon " + icon);
            Check(ref failures, File.Exists(iconPath) && new FileInfo(iconPath).Length > 200,
                tag + " -> icon is a real icon, not a stub (" +
                (File.Exists(iconPath) ? new FileInfo(iconPath).Length.ToString() : "missing") + " bytes)");

            string cls = Attr(doc, ns, "/v:VSTemplate/v:WizardExtension/v:FullClassName");
            var type = asm.GetType(cls, false);
            Check(ref failures, type != null && typeof(ArxVsixWizard.Items.ArxItemWizardBase).IsAssignableFrom(type),
                tag + " -> wizard class " + cls);

            models.TryGetValue(tag, out var model);
            Check(ref failures, model != null, tag + " -> has a matching ItemModel in the smoke test");

            var nodes = doc.SelectNodes("/v:VSTemplate/v:TemplateContent/v:ProjectItem", ns);
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nodes.Count; i++)
            {
                string src = nodes[i].InnerText.Trim();
                referenced.Add(src);
                Check(ref failures, File.Exists(Path.Combine(dir, src)), tag + " -> wrapper file " + src);
                Check(ref failures,
                    nodes[i].Attributes?["TargetFileName"]?.Value == "$ArxWrap" + i + "File$",
                    tag + " -> ProjectItem[" + i + "] TargetFileName=$ArxWrap" + i + "File$");
                if (model != null && i < model.Files.Count)
                    Check(ref failures, string.Equals(src, model.Files[i].WrapperName, StringComparison.OrdinalIgnoreCase),
                        tag + " -> ProjectItem[" + i + "] order matches ItemModel.Files");
            }
            if (model != null)
                Check(ref failures, nodes.Count == model.Files.Count,
                    tag + " -> ProjectItem count matches ItemModel.Files");

            foreach (var txt in Directory.GetFiles(dir, "*.txt"))
                Check(ref failures, referenced.Contains(Path.GetFileName(txt)),
                    tag + " -> " + Path.GetFileName(txt) + " is referenced");
        }

        return failures;
    }

    // ---- MfcSupport: the dialog id must really land in the project's resource files ----
    static int RunResourceInjectionCheck(string outDir)
    {
        int failures = 0;
        string dir = Path.Combine(outDir, "_rescheck");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        // A miniature of what the project wizard produces.
        File.WriteAllText(Path.Combine(dir, "Resource.h"),
            "#define IDR_MAINFRAME 128\r\n#define _APS_NEXT_RESOURCE_VALUE 102\r\n"
            + "#define _APS_NEXT_COMMAND_VALUE 32771\r\n");
        File.WriteAllText(Path.Combine(dir, "MyProj.rc"), "IDR_MAINFRAME ICON \"MyProj.ico\"\r\n");

        var model = new ArxVsixWizard.Items.MfcSupportItemModel("CMyMfcClass");
        model.Symbols.Set("BASE_CLASS", "CAdUiDockControlBar");
        model.Symbols.Set("IDD_DIALOG", "IDD_TESTDLG");
        model.OnFieldsChanged();

        var context = new ArxVsixWizard.Items.ItemContext { TargetDir = dir };
        ArxVsixWizard.Items.MfcResourceEditor.Apply(model, context);

        string res = File.ReadAllText(Path.Combine(dir, "Resource.h"));
        string rc = File.ReadAllText(Path.Combine(dir, "MyProj.rc"));
        Check(ref failures, res.Contains("#define IDD_TESTDLG 102"),
            "MfcResourceEditor -> dialog id defined with the next free value");
        Check(ref failures, res.Contains("#define _APS_NEXT_RESOURCE_VALUE 103"),
            "MfcResourceEditor -> _APS_NEXT_RESOURCE_VALUE bumped");
        Check(ref failures, rc.Contains("IDD_TESTDLG DIALOG"),
            "MfcResourceEditor -> DIALOG block appended to the project .rc");
        Check(ref failures, res.Contains("#define IDR_MAINFRAME 128"),
            "MfcResourceEditor -> existing defines untouched");

        // Running twice must not duplicate anything.
        ArxVsixWizard.Items.MfcResourceEditor.Apply(model, context);
        int occurrences = System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(Path.Combine(dir, "Resource.h")), "#define IDD_TESTDLG").Count;
        Check(ref failures, occurrences == 1, "MfcResourceEditor -> second run does not duplicate the id");

        // The real Resource.h carries the bookkeeping block the resource editor owns; a new id has to
        // land above it, not after it, or the editor never sees it. This is the shape the wizard
        // actually writes (see Templates\ArxApp\Resource.h).
        string dir2 = Path.Combine(outDir, "_rescheck2");
        if (Directory.Exists(dir2)) Directory.Delete(dir2, true);
        Directory.CreateDirectory(dir2);
        File.WriteAllText(Path.Combine(dir2, "Resource.h"),
            "//{{NO_DEPENDENCIES}}\r\n#define IDS_PROJNAME 100\r\n#define IDR_MAINFRAME 101\r\n\r\n"
            + "// Next default values for new objects\r\n//\r\n#ifdef APSTUDIO_INVOKED\r\n"
            + "#ifndef APSTUDIO_READONLY_SYMBOLS\r\n#define _APS_NEXT_RESOURCE_VALUE 102\r\n"
            + "#define _APS_NEXT_COMMAND_VALUE 32768\r\n#define _APS_NEXT_CONTROL_VALUE 100\r\n"
            + "#define _APS_NEXT_SYMED_VALUE 102\r\n#endif\r\n#endif\r\n");
        File.WriteAllText(Path.Combine(dir2, "MyProj.rc"), "IDR_MAINFRAME ICON \"MyProj.ico\"\r\n");

        var model2 = new ArxVsixWizard.Items.MfcSupportItemModel("CMyMfcClass");
        model2.Symbols.Set("BASE_CLASS", "CAdUiDockControlBar");
        model2.Symbols.Set("IDD_DIALOG", "IDD_MYMFCCLASS");
        model2.OnFieldsChanged();
        ArxVsixWizard.Items.MfcResourceEditor.Apply(model2, new ArxVsixWizard.Items.ItemContext { TargetDir = dir2 });

        string res2 = File.ReadAllText(Path.Combine(dir2, "Resource.h"));
        int idAt = res2.IndexOf("#define IDD_MYMFCCLASS", StringComparison.Ordinal);
        int blockAt = res2.IndexOf("// Next default values for new objects", StringComparison.Ordinal);
        Check(ref failures, idAt > 0 && blockAt > 0 && idAt < blockAt,
            "MfcResourceEditor -> the new id is defined above the resource editor's bookkeeping block");
        Check(ref failures, res2.Contains("#define IDS_PROJNAME 100"),
            "MfcResourceEditor -> IDS_PROJNAME survives the insertion");

        return failures;
    }

    static string Attr(XmlDocument doc, XmlNamespaceManager ns, string xpath)
    {
        var node = doc.SelectSingleNode(xpath, ns);
        if (node == null) return "";
        return node is XmlAttribute ? node.Value : node.InnerText.Trim();
    }

    static string ReadResource(Assembly asm, string logicalName)
    {
        using (var s = asm.GetManifestResourceStream(logicalName))
        using (var ms = new MemoryStream())
        {
            if (s == null) throw new InvalidOperationException("missing resource " + logicalName);
            s.CopyTo(ms);
            return Encoding.GetEncoding(1252).GetString(ms.ToArray());
        }
    }

    // ---- Phase 0: same-line [!if]/[!else]/[!endif] support (used by the ATL item templates) ----
    static int RunInlineDirectiveChecks()
    {
        int failures = 0;
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string comDir = Path.Combine(root, "ArxAtlWizComWrapper", "Templates", "1033");
        string dynDir = Path.Combine(root, "ArxAtlWizDynProp", "Templates", "1033");

        // --- object.h / dynprop.h: inline "[!if X]\tdual,[!endif]" triple + nested inline ---
        foreach (var pair in new[]
        {
            new KeyValuePair<string, string>("object.h", Path.Combine(comDir, "object.h")),
            new KeyValuePair<string, string>("dynprop.h", Path.Combine(dynDir, "dynprop.h")),
        })
        {
            string src = ReadAnsi(pair.Value);
            string tag = pair.Key;

            var dual = BaseSymbols();
            dual.Set("ATTRIBUTED", true); dual.Set("INTERFACE_DUAL", true); dual.Set("AUTOMATION", true);
            string both = TemplateRenderer.Render(src, dual);
            Check(ref failures, Contains(both, "dual,"), tag + " DUAL+AUTOMATION -> dual,");
            Check(ref failures, Contains(both, "oleautomation,"), tag + " DUAL+AUTOMATION -> oleautomation,");
            Check(ref failures, Contains(both, "nonextensible,"), tag + " DUAL+AUTOMATION -> nonextensible,");
            Check(ref failures, Contains(both, "IFoo : IDispatch"), tag + " DUAL -> IDispatch base");
            Check(ref failures, !Contains(both, "[!"), tag + " DUAL+AUTOMATION -> no residual [! directive");

            var noDual = BaseSymbols();
            noDual.Set("ATTRIBUTED", true); noDual.Set("INTERFACE_DUAL", false); noDual.Set("AUTOMATION", true);
            string onlyAuto = TemplateRenderer.Render(src, noDual);
            Check(ref failures, !Contains(onlyAuto, "dual,"), tag + " AUTOMATION only -> no dual,");
            Check(ref failures, Contains(onlyAuto, "oleautomation,"), tag + " AUTOMATION only -> oleautomation,");
            Check(ref failures, !Contains(onlyAuto, "nonextensible,"), tag + " AUTOMATION only -> no nonextensible,");
            Check(ref failures, Contains(onlyAuto, "IFoo : IUnknown"), tag + " no DUAL -> IUnknown base");
            Check(ref failures, !Contains(onlyAuto, "[!"), tag + " AUTOMATION only -> no residual [! directive");

            var plain = BaseSymbols();
            plain.Set("ATTRIBUTED", false);
            string nonAttr = TemplateRenderer.Render(src, plain);
            Check(ref failures, Contains(nonAttr, "DECLARE_REGISTRY_RESOURCEID"),
                tag + " !ATTRIBUTED -> DECLARE_REGISTRY_RESOURCEID");
            Check(ref failures, !Contains(nonAttr, "[!"), tag + " !ATTRIBUTED -> no residual [! directive");
        }

        // --- objco.idl: inline triple, "[!if A || B]" ---
        string idl = ReadAnsi(Path.Combine(comDir, "objco.idl"));

        var idlDual = BaseSymbols();
        idlDual.Set("INTERFACE_DUAL", true); idlDual.Set("AUTOMATION", false);
        string i1 = TemplateRenderer.Render(idl, idlDual);
        Check(ref failures, Contains(i1, "dual,"), "objco.idl DUAL -> dual,");
        Check(ref failures, !Contains(i1, "oleautomation,"), "objco.idl DUAL -> no oleautomation,");
        Check(ref failures, Contains(i1, "nonextensible,"), "objco.idl DUAL -> nonextensible,");
        Check(ref failures, Contains(i1, "interface IFoo : IAcadObject"), "objco.idl DUAL -> IAcadObject base");
        Check(ref failures, !Contains(i1, "[!"), "objco.idl DUAL -> no residual [! directive");

        var idlNone = BaseSymbols();
        string i2 = TemplateRenderer.Render(idl, idlNone);
        Check(ref failures, !Contains(i2, "dual,"), "objco.idl none -> no dual,");
        Check(ref failures, !Contains(i2, "oleautomation,"), "objco.idl none -> no oleautomation,");
        Check(ref failures, !Contains(i2, "nonextensible,"), "objco.idl none -> no nonextensible,");
        Check(ref failures, Contains(i2, "interface IFoo : IUnknown"), "objco.idl none -> IUnknown base");
        Check(ref failures, !Contains(i2, "[!"), "objco.idl none -> no residual [! directive");

        var idlAuto = BaseSymbols();
        idlAuto.Set("AUTOMATION", true);
        string i3 = TemplateRenderer.Render(idl, idlAuto);
        Check(ref failures, !Contains(i3, "dual,"), "objco.idl AUTOMATION -> no dual,");
        Check(ref failures, Contains(i3, "oleautomation,"), "objco.idl AUTOMATION -> oleautomation,");
        Check(ref failures, Contains(i3, "nonextensible,"), "objco.idl AUTOMATION -> nonextensible,");

        // --- dynpropint.idl: "[!if X]IDispatch[!else]IUnknown[!endif]" on one line ---
        string dint = ReadAnsi(Path.Combine(dynDir, "dynpropint.idl"));

        var dintDual = BaseSymbols();
        dintDual.Set("INTERFACE_DUAL", true);
        string d1 = TemplateRenderer.Render(dint, dintDual);
        Check(ref failures, Contains(d1, "interface IFoo : IDispatch"), "dynpropint.idl DUAL -> IDispatch base");
        Check(ref failures, Contains(d1, "dual,"), "dynpropint.idl DUAL -> dual,");
        Check(ref failures, Contains(d1, "nonextensible,"), "dynpropint.idl DUAL -> nonextensible,");
        Check(ref failures, !Contains(d1, "[!"), "dynpropint.idl DUAL -> no residual [! directive");

        string d2 = TemplateRenderer.Render(dint, BaseSymbols());
        Check(ref failures, Contains(d2, "interface IFoo : IUnknown"), "dynpropint.idl none -> IUnknown base");
        Check(ref failures, !Contains(d2, "dual,"), "dynpropint.idl none -> no dual,");
        Check(ref failures, !Contains(d2, "nonextensible,"), "dynpropint.idl none -> no nonextensible,");
        Check(ref failures, !Contains(d2, "[!"), "dynpropint.idl none -> no residual [! directive");

        // --- C4: '$' escaping helper ---
        Check(ref failures, TemplateRenderer.EscapeDollars("a$b$") == "a$$b$$", "EscapeDollars doubles '$'");

        return failures;
    }

    static SymbolTable BaseSymbols()
    {
        var s = new SymbolTable();
        s.Set("PROJECT_NAME", "MyProj");
        s.Set("HEADER_FILE", "Foo.h");
        s.Set("CLASS_NAME", "CFoo");
        s.Set("COCLASS", "Foo");
        s.Set("SHORT_NAME", "Foo");
        s.Set("INTERFACE_NAME", "IFoo");
        s.Set("INTERFACE_IID", "{11111111-2222-3333-4444-555555555555}");
        s.Set("CONNECTION_POINT_IID", "{66666666-7777-8888-9999-AAAAAAAAAAAA}");
        s.Set("CLSID_REGISTRY_FORMAT", "{BBBBBBBB-CCCC-DDDD-EEEE-FFFFFFFF0000}");
        s.Set("TYPE_NAME", "Foo Class");
        s.Set("PROGID", "MyProj.Foo");
        s.Set("VERSION_INDEPENDENT_PROGID", "MyProj.Foo.1");
        s.Set("LIB_NAME", "MyProjLib");
        s.Set("TYPELIB_VERSION_MAJOR", "1");
        s.Set("TYPELIB_VERSION_MINOR", "0");
        s.Set("ARX_CLASS_NAME", "CFoo");
        return s;
    }

    static bool Contains(string text, string token) => text.IndexOf(token, StringComparison.Ordinal) >= 0;

    static void Check(ref int failures, bool ok, string what)
    {
        if (ok) { Console.WriteLine("OK    " + what); return; }
        failures++;
        Console.WriteLine("FAIL  " + what);
    }

    static string ReadAnsi(string path)
    {
        using (var sr = new StreamReader(path, Encoding.GetEncoding(1252)))
            return sr.ReadToEnd();
    }

    static void RenderTo(Assembly asm, string res, string target, SymbolTable syms, bool utf8 = false)
    {
        string src;
        using (var s = asm.GetManifestResourceStream(res))
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            src = Encoding.GetEncoding(1252).GetString(ms.ToArray());
        }
        string text = TemplateRenderer.Render(src, syms);
        File.WriteAllText(target, text, utf8 ? (Encoding)new UTF8Encoding(false) : Encoding.UTF8);
    }

    static List<(string Name, WizardOptions Opts)> GetSets()
    {
        return new List<(string, WizardOptions)>
        {
            ("ArxPlain", Plain(WizardKind.ArxApp, AppType.Arx)),
            ("ArxAtl", With(Plain(WizardKind.ArxApp, AppType.Arx), o => o.ComServer = ComServer.Atl)),
            ("ArxMfcExt", With(Plain(WizardKind.ArxApp, AppType.Arx), o => o.Mfc = MfcSupport.ExtensionShared)),
            ("DbxClr", With(Plain(WizardKind.ArxApp, AppType.Dbx), o => o.DotNetModule = true)),
            ("Crx", Plain(WizardKind.ArxApp, AppType.Crx)),
            ("OmfArx", With(Plain(WizardKind.OmfApp, AppType.Arx), o => o.OmfApp = true)),
            ("OmfDbx", Plain(WizardKind.OmfApp, AppType.Dbx)),
            ("OmfClr", With(Plain(WizardKind.OmfApp, AppType.Arx), o => { o.OmfApp = true; o.DotNetModule = true; })),
        };
    }

    static WizardOptions Plain(WizardKind k, AppType t)
    {
        var o = new WizardOptions { Kind = k, AppType = t };
        o.SelectedYears.Clear();
        o.SelectedYears.Add("2014");
        o.SelectedYears.Add("2024");
        o.SelectedYears.Add("2027");
        return o;
    }

    static WizardOptions With(WizardOptions o, Action<WizardOptions> a) { a(o); return o; }
}
