//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauche - Autodesk Developer Technical Services

//wizard.OkCancelAlert(FindAutoCAD()); return;
// http://msdn.microsoft.com/en-us/library/aa291835(v=VS.71).aspx
// http://msdn.microsoft.com/en-us/library/microsoft.visualstudio.vswizard.vcwizctlclass_members.aspx

function OnFinish(selProj, selObj) {
	try {
		var strProjectPath = wizard.FindSymbol('PROJECT_PATH');
		var strProjectName = wizard.FindSymbol('PROJECT_NAME');
		wizard.AddSymbol("UPPER_CASE_PROJECT_NAME", strProjectName.toUpperCase());

		wizard.AddSymbol("SAFE_PROJECT_NAME", CreateSafeName(strProjectName));
		wizard.AddSymbol("UPPER_CASE_SAFE_PROJECT_NAME", CreateSafeName(strProjectName.toUpperCase()));

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
		ArxSetupVersionSymbols(wizard, !!bDotNetModule);

		//- Unique project GUIDs (the old templates hardcoded a single GUID)
		wizard.AddSymbol("PROJECT_GUID", wizard.FormatGuid(wizard.CreateGuid(), 0));
		wizard.AddSymbol("PROJECT_RES_GUID", wizard.FormatGuid(wizard.CreateGuid(), 0));

		var strProjTemplate = RenderPrjToTemporaryFile();
		selProj = CreateArxProject(strProjectName, strProjectPath, strProjTemplate);
		SetupFilters(selProj);

		var InfFile = CreateInfFile();
		AddFilesToCustomProj(selProj, strProjectName, strProjectPath, InfFile);
		InfFile.Delete();

		SetCommonPchSettings(selProj);
		selProj.Object.Save();

		if (bOmfAppType)
			MakeOmfResourceProject(selProj, strProjectPath, strProjectName);

	} catch (e) {
		if (e.description.length != 0)
			SetErrorInfo(e);
		return (e.number);
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
