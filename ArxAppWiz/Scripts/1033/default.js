//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauvel - Autodesk Developer Technical Services

//wizard.OkCancelAlert(""); return;
// config.InheritedPropertySheets = 'Fullpath\x.vsprops;';
// http://msdn.microsoft.com/en-us/library/aa291835(v=VS.71).aspx
// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.vswizard.vcwizctlclass_members.aspx

//- ---------------------------------------------------------------------------------------------
//- Diagnostics. This wizard runs inside Visual Studio's own script host, where a failure only
//- surfaces as a message box with the bare description ("缺少对象" / "Object required" and the
//- like) - no step, no line, no HRESULT. Every step of OnFinish is therefore appended to
//- %TEMP%\arxwiz-classic.log, together with what the script scope can actually see: that is what
//- tells a missing framework include (Common.js / Script.js from the Visual Studio installation)
//- apart from a missing multi-year helper (ArxWizCommon\arxCommon.js, which the HTML pages load).
//- ---------------------------------------------------------------------------------------------
var ARX_WIZARD_NAME = "ArxAppWiz";
var ARX_LOG_FILE = "arxwiz-classic.log";

function ArxWizLog(strMsg) {
  try {
    var oFso = new ActiveXObject("Scripting.FileSystemObject");
    var strFile = oFso.GetSpecialFolder(2) + "\\" + ARX_LOG_FILE;
    var oStream = oFso.OpenTextFile(strFile, 8, true);
    oStream.WriteLine(new Date().toString() + " [" + ARX_WIZARD_NAME + "] " + strMsg);
    oStream.Close();
  } catch (e) { }
}

//- typeof never throws for an absent identifier, so this reports the whole scope in one line.
function ArxWizScope() {
  return ("framework CreateSafeName=" + (typeof CreateSafeName)
    + " CreateSafeRCFileName=" + (typeof CreateSafeRCFileName)
    + " AddFilesToNewProjectWithInfFile=" + (typeof AddFilesToNewProjectWithInfFile)
    + " SetupFilters=" + (typeof SetupFilters)
    + " SetCommonPchSettings=" + (typeof SetCommonPchSettings)
    + " | multi-year ArxSetupVersionSymbols=" + (typeof ArxSetupVersionSymbols)
    + " | engine wizard=" + (typeof wizard)
    + " dte=" + (typeof dte));
}

function ArxWizErrorText(e) {
  var aParts = [];
  try { aParts.push("description=[" + e.description + "]"); } catch (e1) { aParts.push("description=<none>"); }
  try { aParts.push("message=[" + e.message + "]"); } catch (e2) { }
  try { aParts.push("name=[" + e.name + "]"); } catch (e3) { }
  try {
    var n = e.number;
    aParts.push("number=0x" + (n < 0 ? (0x100000000 + n) : n).toString(16));
  } catch (e4) { }
  return (aParts.join(" "));
}

//- SetErrorInfo only reads .description and .number; a local object keeps the report working even
//- when the engine's own error object has no usable description.
function ArxWizError(strDescription, nNumber) {
  this.description = strDescription;
  this.number = nNumber;
}

function OnFinish(selProj, selObj) {
  var strStep = "enter";
  try {
    ArxWizLog("OnFinish enter: " + ArxWizScope());
    strStep = "project symbols";
    var strProjectPath = wizard.FindSymbol("PROJECT_PATH");
    var strProjectName = wizard.FindSymbol("PROJECT_NAME");
    wizard.AddSymbol("UPPER_CASE_PROJECT_NAME", strProjectName.toUpperCase());
    strStep = "safe names (CreateSafeName / CreateSafeRCFileName)";
    wizard.AddSymbol("SAFE_PROJECT_NAME", CreateSafeName(strProjectName));
    wizard.AddSymbol(
      "UPPER_CASE_SAFE_PROJECT_NAME",
      CreateSafeName(strProjectName.toUpperCase())
    );
    wizard.AddSymbol(
      "RC_FILE_NAME",
      CreateSafeRCFileName(strProjectName) + ".rc"
    );

    strStep = "MFC / ATL flags";
    var bMfcExtDll = wizard.FindSymbol("MFC_EXT_SHARED");
    var bSharedMfc = wizard.FindSymbol("MFC_REG_SHARED") || bMfcExtDll;
    var bMfcSupport = wizard.FindSymbol("MFC_REG_STATIC") || bSharedMfc;
    if (bMfcSupport) wizard.AddSymbol("ARX_MFC_SUPPORT", "Dynamic");
    else wizard.AddSymbol("ARX_MFC_SUPPORT", "false");
    var bUseAtl = wizard.FindSymbol("ATL_COM_SERVER");
    if (bUseAtl) wizard.AddSymbol("ARX_ATL_SUPPORT", "Dynamic");
    else wizard.AddSymbol("ARX_ATL_SUPPORT", "false");

    strStep = "app type flags";
    var bArxAppType = wizard.FindSymbol("APP_ARX_TYPE");
    var bDbxAppType = wizard.FindSymbol("APP_DBX_TYPE");
    var bDotNetModule = wizard.FindSymbol("DOTNET_MODULE");
    if (bDotNetModule) {
      if (bArxAppType) wizard.AddSymbol("PRJ_TYPE_APP", "arxnet");
      else if (bDbxAppType) wizard.AddSymbol("PRJ_TYPE_APP", "dbxnet");
      else wizard.AddSymbol("PRJ_TYPE_APP", "crxnet");
    } else {
      if (bArxAppType) wizard.AddSymbol("PRJ_TYPE_APP", "arx");
      else if (bDbxAppType) wizard.AddSymbol("PRJ_TYPE_APP", "dbx");
      else wizard.AddSymbol("PRJ_TYPE_APP", "crx");
    }

    //- Multi-year configurations: fills ARX_CONFIG_XML, ARX_YEAR_REGEX,
    //- ARX_TOOLSET_XML, ARX_CLR_XML (see ArxWizCommon/arxCommon.js).
    //- The ObjectARX property sheets are consumed from the global folder
    //- C:\Program Files\Autodesk\ObjectARX Props, nothing is copied into
    //- the project directory anymore.
    strStep = "multi-year symbols (ArxWizCommon\\arxCommon.js)";
    if (typeof ArxSetupVersionSymbols == "undefined")
      throw new Error("ArxWizCommon\\arxCommon.js did not reach the wizard script scope; see %TEMP%\\arxwiz-classic.log");
    ArxSetupVersionSymbols(wizard, bDotNetModule ? true : false);

    //- Unique project GUID (the old template hardcoded a single GUID)
    strStep = "project guid";
    wizard.AddSymbol("PROJECT_GUID", wizard.FormatGuid(wizard.CreateGuid(), 0));

    //selProj =CreateProject (strProjectName, strProjectPath) ; //- Use the default Win32 only project template from Visual Studio
    strStep = "render the skeleton to a temporary vcxproj";
    var strProjTemplate = RenderPrjToTemporaryFile();
    strStep = "AddFromTemplate";
    selProj = CreateArxProject(strProjectName, strProjectPath, strProjTemplate);
    strStep = "SetupFilters";
    SetupFilters(selProj);
    strStep = "AddFilesToNewProjectWithInfFile";
    AddFilesToNewProjectWithInfFile(selProj, strProjectName);
    strStep = "save";
    selProj.Object.Save();
    ArxWizLog("OnFinish done: project=" + selProj.Name
      + " template=" + strProjTemplate
      + " years=" + wizard.FindSymbol("ARX_YEARS"));
  } catch (e) {
    var strText = "OnFinish failed at step [" + strStep + "]: " + ArxWizErrorText(e);
    ArxWizLog(strText);
    try {
      SetErrorInfo(new ArxWizError(strText, (e && e.number) ? e.number : -1));
    } catch (e1) {
      try { wizard.ReportError(strText); } catch (e2) { }
    }
    return ((e && e.number) ? e.number : -1);
  }
}

//-----------------------------------------------------------------------------
function CreateArxProject(strProjectName, strProjectPath, strProjTemplate) {
  try {
    /* var strProjTemplatePath =wizard.FindSymbol ("ABSOLUTE_PATH") ;
    var strProjTemplate =strProjTemplatePath + "\\Templates\\1033\\x64win32.vcxproj" ;
 */
    var Solution = dte.Solution;
    var strSolutionName = "";
    if (wizard.FindSymbol("CLOSE_SOLUTION")) {
      Solution.Close();
      strSolutionName = wizard.FindSymbol("VS_SOLUTION_NAME");
      if (strSolutionName.length) {
        var strSolutionPath = strProjectPath.substr(
          0,
          strProjectPath.length - strProjectName.length
        );
        Solution.Create(strSolutionPath, strSolutionName);
      }
    }

    var strProjectNameWithExt = strProjectName + ".vcxproj";
    var oTarget = wizard.FindSymbol("TARGET");
    var oProj;
    if (wizard.FindSymbol("WIZARD_TYPE") == vsWizardAddSubProject) {
      // vsWizardAddSubProject
      var prjItem = oTarget.AddFromTemplate(
        strProjTemplate,
        strProjectPath + "\\" + strProjectNameWithExt
      );
      oProj = prjItem.SubProject;
    } else {
      oProj = oTarget.AddFromTemplate(
        strProjTemplate,
        strProjectPath,
        strProjectNameWithExt
      );
    }
    return oProj;
  } catch (e) {
    throw e;
  }
}

//-----------------------------------------------------------------------------
function GetTargetName(strName, strProjectName) {
  try {
    //----- Set the name of the rendered file based on the template filename
    var strTarget = strName;
    if (strName.substr(0, 4) == "root")
      strTarget = strProjectName + strName.substr(4, strName.length - 4);
    return strTarget;
  } catch (e) {
    throw e;
  }
}

function SetFileProperties(projfile, strName) { }

//-----------------------------------------------------------------------------
function RenderPrjToTemporaryFile() {
  var strProjTemplatePath = wizard.FindSymbol("ABSOLUTE_PATH");
  var strProjTemplate =
    strProjTemplatePath + "\\Templates\\1033\\x64win32.vcxproj";

  var TemporaryFolder = 2;
  var oFSO = new ActiveXObject("Scripting.FileSystemObject");
  var oFolder = oFSO.GetSpecialFolder(TemporaryFolder);
  var strTempFile =
    oFSO.GetAbsolutePathName(oFolder.Path) +
    "\\" +
    oFSO.GetTempName() +
    ".vcxproj";
  var strTempFileContents = wizard.RenderTemplateToString(strProjTemplate);
  var oStream;
  oStream = oFSO.CreateTextFile(strTempFile, true, false /*ANSI*/);
  oStream.Write(strTempFileContents);
  oStream.Close();
  return strTempFile;
}

//- ---------------------------------------------------------------------------------------------
//- The engine's script scope is not the HTML pages' scope: it preloads Visual Studio's own
//- Common.js / Script.js (SetupFilters, CreateSafeName, RenderAddTemplate, ...) but it does NOT see
//- ArxWizCommon\arxCommon.js, which the pages include - calling into it from OnFinish fails with a
//- bare "Object required". Evaluate that file into this scope. The eval call has to sit at file
//- scope like this: inside a function its definitions would die with the function.
//- ---------------------------------------------------------------------------------------------
try {
  var oArxSharedFso = new ActiveXObject("Scripting.FileSystemObject");
  var strArxSharedPath = wizard.FindSymbol("ABSOLUTE_PATH");
  strArxSharedPath = strArxSharedPath.substring(0, strArxSharedPath.lastIndexOf("\\") + 1)
    + "ArxWizCommon\\arxCommon.js";
  var oArxSharedStream = oArxSharedFso.OpenTextFile(strArxSharedPath, 1);
  var strArxShared = oArxSharedStream.ReadAll();
  oArxSharedStream.Close();
  eval(strArxShared);
  ArxWizLog("ArxWizCommon\\arxCommon.js evaluated into the wizard script scope"
    + " (ArxSetupVersionSymbols=" + (typeof ArxSetupVersionSymbols) + ")");
} catch (e) {
  ArxWizLog("ArxWizCommon\\arxCommon.js could NOT be evaluated: " + ArxWizErrorText(e));
}
