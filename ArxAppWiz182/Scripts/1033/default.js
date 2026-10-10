//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauche - Autodesk Developer Technical Services

//wizard.OkCancelAlert(FindAutoCAD()); return;
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
var ARX_WIZARD_NAME = "ArxAppWiz182";
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
		+ " RenderAddTemplate=" + (typeof RenderAddTemplate)
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
		var strProjectPath = wizard.FindSymbol('PROJECT_PATH');
		var strProjectName = wizard.FindSymbol('PROJECT_NAME');
		wizard.AddSymbol("UPPER_CASE_PROJECT_NAME", strProjectName.toUpperCase());

		strStep = "safe names (CreateSafeName / CreateSafeRCFileName)";
		wizard.AddSymbol("SAFE_PROJECT_NAME", CreateSafeName(strProjectName));
		wizard.AddSymbol("UPPER_CASE_SAFE_PROJECT_NAME", CreateSafeName(strProjectName.toUpperCase()));

		strStep = "MFC / ATL flags";
		var bMfcExtDll = wizard.FindSymbol("MFC_EXT_SHARED");
		var bSharedMfc = wizard.FindSymbol("MFC_REG_SHARED") || bMfcExtDll;
		var bMfcSupport = wizard.FindSymbol("MFC_REG_STATIC") || bSharedMfc;
		if (bMfcSupport)
			wizard.AddSymbol("ARX_MFC_SUPPORT", "Dynamic");
		else
			wizard.AddSymbol("ARX_MFC_SUPPORT", "false");
		var bUseAtl = wizard.FindSymbol("ATL_COM_SERVER");
		if (bUseAtl)
			wizard.AddSymbol("ARX_ATL_SUPPORT", "Dynamic");
		else
			wizard.AddSymbol("ARX_ATL_SUPPORT", "false");

		strStep = "app type flags";
		var bArxAppType = wizard.FindSymbol('APP_ARX_TYPE');
		var bDotNetModule = wizard.FindSymbol('DOTNET_MODULE');
		if (bDotNetModule)
			wizard.AddSymbol("PRJ_TYPE_APP", bArxAppType ? "arxnet" : "dbxnet");
		else
			wizard.AddSymbol("PRJ_TYPE_APP", bArxAppType ? "arx" : "dbx");

		//- OMF specific preprocessor symbols and compiler options
		var bOmfAppType = wizard.FindSymbol('OMF_APP');
		if (bOmfAppType) {
			wizard.AddSymbol("ARX_OMF_DEFS", bArxAppType ? "_OMFAPP;USE_ACAD_MODELER;" : "_OMFAPP;USE_ACAD_MODELER;DBX;");
			wizard.AddSymbol("ARX_OMF_ZM", " -Zm1000");
		} else {
			wizard.AddSymbol("ARX_OMF_DEFS", "");
			wizard.AddSymbol("ARX_OMF_ZM", "");
		}

		//- Multi-year configurations: fills ARX_CONFIG_XML, ARX_YEAR_REGEX,
		//- ARX_TOOLSET_XML, ARX_CLR_XML (see ArxWizCommon/arxCommon.js).
		strStep = "multi-year symbols (ArxWizCommon\\arxCommon.js)";
		if (typeof ArxSetupVersionSymbols == "undefined")
			throw new Error("ArxWizCommon\\arxCommon.js did not reach the wizard script scope; see %TEMP%\\arxwiz-classic.log");
		ArxSetupVersionSymbols(wizard, !!bDotNetModule);

		//- Unique project GUIDs (the old templates hardcoded a single GUID)
		strStep = "project guids";
		wizard.AddSymbol("PROJECT_GUID", wizard.FormatGuid(wizard.CreateGuid(), 0));
		wizard.AddSymbol("PROJECT_RES_GUID", wizard.FormatGuid(wizard.CreateGuid(), 0));

		strStep = "render the skeleton to a temporary vcxproj";
		var strProjTemplate = RenderPrjToTemporaryFile();
		strStep = "AddFromTemplate";
		selProj = CreateArxProject(strProjectName, strProjectPath, strProjTemplate);
		strStep = "SetupFilters";
		SetupFilters(selProj);

		strStep = "CreateInfFile / AddFilesToCustomProj";
		var InfFile = CreateInfFile();
		AddFilesToCustomProj(selProj, strProjectName, strProjectPath, InfFile);
		InfFile.Delete();

		strStep = "SetCommonPchSettings / save";
		SetCommonPchSettings(selProj);
		selProj.Object.Save();

		if (bOmfAppType) {
			strStep = "MakeOmfResourceProject";
			MakeOmfResourceProject(selProj, strProjectPath, strProjectName);
		}

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

//-----------------------------------------------------------------------------
function AddFilesToCustomProj(proj, strProjectName, strProjectPath, InfFile) {
	try {
		var projItems = proj.ProjectItems;

		var strTemplatePath = wizard.FindSymbol('TEMPLATES_PATH');

		var strTpl = '';
		var strName = '';

		var strTextStream = InfFile.OpenAsTextStream(1, -2);
		while (!strTextStream.AtEndOfStream) {
			strTpl = strTextStream.ReadLine();
			if (strTpl != '') {
				strName = strTpl;
				var strTarget = GetTargetName(strName, strProjectName);
				var strTemplate = strTemplatePath + '\\' + strTpl;
				var strFile = strProjectPath + '\\' + strTarget;

				var bCopyOnly = false; //----- "true" will only copy the file from strTemplate to strTarget without rendering/adding to the project
				var strExt = strName.substr(strName.lastIndexOf('.'));
				if (strExt == '.bmp' || strExt == ".ico" || strExt == '.gif' || strExt == '.rtf' || strExt == '.css')
					bCopyOnly = true;
				wizard.RenderTemplate(strTemplate, strFile, bCopyOnly);
				proj.Object.AddFile(strFile);
			}
		}
		strTextStream.Close();
	} catch (e) {
		throw e;
	}
}

//-----------------------------------------------------------------------------
function GetTargetName(strName, strProjectName) {
	try {
		//----- Set the name of the rendered file based on the template filename
		var strTarget = strName;

		if (strName.substr(0, 4) == 'Root')
			strTarget = strProjectName + strName.substr(4, strName.length - 4);

		if (strName.substr(0, 3) == 'Omf')
			strTarget = strProjectName + strName.substr(3, strName.length - 3);

		return (strTarget);
	} catch (e) {
		throw e;
	}
}

//-----------------------------------------------------------------------------
function MakeOmfResourceProject(selProj, strProjectPath, strProjectName) {
	//- So we need a resource only DLL
	RenderAddTemplate(wizard, "OmfEnuRes.h", strProjectPath + "\\Enu\\Resource.h", false, false);
	RenderAddTemplate(wizard, "OmfEnuRes.rc", strProjectPath + "\\Enu\\" + strProjectName + "Enu.rc", false, false);
	RenderAddTemplate(wizard, "OmfEnuRes.vcxproj", strProjectPath + "\\Enu\\" + strProjectName + "Enu.vcxproj", false, false);
	RenderAddTemplate(wizard, "OmfEnuRes.vcxproj.filters", strProjectPath + "\\Enu\\" + strProjectName + "Enu.vcxproj.filters", false, false);
	var resDllProj = selProj.DTE.Solution.AddFromFile(strProjectPath + "\\Enu\\" + strProjectName + "Enu.vcxproj", false);

	var deps = selProj.DTE.Solution.SolutionBuild.BuildDependencies;
	for (var i = 1; i <= deps.Count; i++) {
		var dep = deps.Item(i);
		if (dep.Project.UniqueName == selProj.UniqueName) {
			dep.AddProject(resDllProj.UniqueName);
			break;
		}
	}
}

//- ---------------------------------------------------------------------------------------------
//- The engine's script scope is not the HTML pages' scope: it preloads Visual Studio's own
//- Common.js / Script.js (SetupFilters, CreateInfFile, RenderAddTemplate, ...) but it does NOT see
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
