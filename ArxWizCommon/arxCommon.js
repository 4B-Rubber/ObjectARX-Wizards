//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauvel - Autodesk Developer Technical Services
var szWizardsVersion ="20, 0, 1, 0 - December 5th, 2013" ;

var szArzWizApplicationTitle ="ObjectARX/DBX Application Wizard for AutoCAD (multi-year)" ;
var szArxWizCOMWrapperTitle ="AutoCAD COM Wrapper Object" ;
var szArxWizDynPropTitle ="AutoCAD Object Dynamic Property" ;
var szArxWizObjectTitle ="ObjectDBX Custom Object Class Wizard" ;
var szArxWizJIGTitle ="ObjectARX Jig Class Wizard" ;
var szArxWizMFCTitle ="ObjectARX MFC Class Wizard" ;
var szArxWizDotNETWrapperTitle ="ObjectARX .NET Custom Object Wrapper Class Wizard" ;
var szArxWizReactorsTitle ="ObjectARX/DBX Transient Reactors Class Wizard" ;
var szArxMemVariableWizTitle ="Add Member Variable Wizard" ;

//wizard.OkCancelAlert ("msg");

//- Get an AutoCAD release
//- The old FindAutoCADUsingWMI() helper no longer ships with Visual Studio,
//- so scan the Autodesk install folder and return the newest AutoCAD install.
function FindAutoCAD() {
	var szPath ="" ;
	var szBase ="C:\\Program Files\\Autodesk\\" ;
	try {
		var oFSO =new ActiveXObject ("Scripting.FileSystemObject") ;
		if ( oFSO.FolderExists (szBase) ) {
			var nBestYear =0 ;
			for ( var oEnum =new Enumerator (oFSO.GetFolder (szBase).SubFolders) ; !oEnum.atEnd () ; oEnum.moveNext () ) {
				var oFolder =oEnum.item () ;
				if ( oFolder.Name.indexOf ("AutoCAD ") == 0 && oFSO.FileExists (oFolder.Path + "\\acad.exe") ) {
					var nYear =parseInt (oFolder.Name.substr (8), 10) ;
					if ( !isNaN (nYear) && nYear >= nBestYear ) {
						nBestYear =nYear ;
						szPath =oFolder.Path + "\\" ;
					}
				}
			}
		}
	} catch (e) {
		szPath ="" ;
	}
	return (szPath) ;
}

//-----------------------------------------------------------------------------
//- Multi-year (multi-version) build support
//- One generated project contains one pair of configurations per selected
//- AutoCAD year: "YYYY" (release) and "YYYYd" (debug). The year is extracted
//- from $(Configuration) with a regular expression, the platform toolset and
//- the global ObjectARX property sheet are selected based on $(ArxYear).
//- Properties sheets are expected in: C:\Program Files\Autodesk\ObjectARX Props\

//- The global property sheet folder. The wizard bakes this path into the projects it generates, and
//- both installers rewrite the literal below to the folder the user picked (Inno: PatchArxCommonJs,
//- MSI: PatchArxCommonJsFiles). Generated projects also read
//- HKLM\SOFTWARE\Autodesk\ObjectARX Wizards\PropsDir at build time and prefer it over the baked
//- path, so this value only has to be right on a machine without that registry entry.
var ARX_PROPS_DIR ="C:\\Program Files\\Autodesk\\ObjectARX Props\\" ;

//- year            : AutoCAD / ObjectARX release year
//- toolset         : VC platform toolset (ArxSDKPlatform in the props)
//- sdk             : ObjectARX SDK release number (ArxSDKVersion)
//- x64Only         : no 32-bit AutoCAD starting with 2020
//- clrCore         : mixed mode uses .NET (Core) instead of .NET Framework
var g_ArxVersions =[
	{ year:"2010", toolset:"v90",  sdk:"18.2", x64Only:false, clrCore:false } ,
	{ year:"2012", toolset:"v90",  sdk:"18.2", x64Only:false, clrCore:false } ,
	{ year:"2014", toolset:"v100", sdk:"19.1", x64Only:false, clrCore:false } ,
	{ year:"2015", toolset:"v110", sdk:"21.0", x64Only:false, clrCore:false } ,
	{ year:"2016", toolset:"v110", sdk:"21.0", x64Only:false, clrCore:false } ,
	{ year:"2018", toolset:"v140", sdk:"22.0", x64Only:false, clrCore:false } ,
	{ year:"2019", toolset:"v141", sdk:"23.0", x64Only:false, clrCore:false } ,
	{ year:"2020", toolset:"v141", sdk:"23.1", x64Only:true , clrCore:false } ,
	{ year:"2021", toolset:"v142", sdk:"24"  , x64Only:true , clrCore:false } ,
	{ year:"2022", toolset:"v142", sdk:"24.1", x64Only:true , clrCore:false } ,
	{ year:"2023", toolset:"v142", sdk:"24.2", x64Only:true , clrCore:false } ,
	{ year:"2024", toolset:"v143", sdk:"24.3", x64Only:true , clrCore:false } ,
	{ year:"2025", toolset:"v143", sdk:"25"  , x64Only:true , clrCore:true  } ,
	{ year:"2026", toolset:"v143", sdk:"25.1", x64Only:true , clrCore:true  } ,
	{ year:"2027", toolset:"v145", sdk:"26.0", x64Only:true , clrCore:true  }
] ;

//- Years checked by default on the wizard version page
//- The four the year table marks "default" as well: the C++17 ones. The compatible set (2014 ...
//- 2027) stays one button press away, but it is no longer what a fresh page starts with.
var g_ArxDefaultYears ={ "2020":1, "2024":1, "2026":1, "2027":1 } ;

//- Years covered by every SDK in the range, i.e. what "Compatible" ticks on the version page.
//- Same set the installer's year table marks compatible.
var g_ArxCompatibleYears ={ "2014":1, "2016":1, "2018":1, "2020":1, "2024":1, "2026":1, "2027":1 } ;

function ArxIsDefaultYear (strYear) {
	return (g_ArxDefaultYears[strYear] == 1) ;
}

//- Returns the table rows selected on the version page (symbols TGT_YYYY),
//- in table (ascending) order.
function ArxGetSelectedVersions (oWiz) {
	var aSelected =[] ;
	for ( var i =0 ; i < g_ArxVersions.length ; i++ ) {
		try {
			if ( oWiz.FindSymbol ("TGT_" + g_ArxVersions[i].year) )
				aSelected.push (g_ArxVersions[i]) ;
		} catch (e) {}
	}
	return (aSelected) ;
}

function ArxBuildYearCondition (aYears) {
	var strCond ="" ;
	for ( var i =0 ; i < aYears.length ; i++ ) {
		if ( i > 0 )
			strCond +=" or " ;
		strCond +="'$(ArxYear)'==" + "'" + aYears[i] + "'" ;
	}
	return (strCond) ;
}

//- Computes every template symbol used by the multi-year vcxproj templates:
//-   ARX_CONFIG_XML    : ProjectConfiguration entries (newest first, debug first)
//-   ARX_YEAR_REGEX    : regular expression extracting the year
//-   ARX_TOOLSET_XML   : year conditioned PlatformToolset entries
//-   ARX_CLR_XML       : CLRSupport entry (false / per-year true|NetCore)
//-   ARX_YEARS         : comma separated selected years
//- bClr must be true for a mixed .NET module project.
function ArxSetupVersionSymbols (oWiz, bClr) {
	var aSelected =ArxGetSelectedVersions (oWiz) ;
	if ( aSelected.length == 0 )
		throw new Error ("Select at least one target AutoCAD version on the Target Versions page.") ;

	//- Newest year first so that the newest debug configuration is the
	//- default active configuration, matching the modern project templates.
	var aOrdered =[] ;
	for ( var i =aSelected.length - 1 ; i >= 0 ; i-- )
		aOrdered.push (aSelected[i]) ;

	var strConfigXml ="" ;
	for ( var i =0 ; i < aOrdered.length ; i++ ) {
		var strYear =aOrdered[i].year ;
		strConfigXml +="    <ProjectConfiguration Include=\"" + strYear + "d|x64\"><Configuration>" + strYear + "d</Configuration><Platform>x64</Platform></ProjectConfiguration>\r\n" ;
		strConfigXml +="    <ProjectConfiguration Include=\"" + strYear + "|x64\"><Configuration>" + strYear + "</Configuration><Platform>x64</Platform></ProjectConfiguration>\r\n" ;
	}

	var strRegex ="" ;
	for ( var i =0 ; i < aSelected.length ; i++ ) {
		if ( i > 0 )
			strRegex +="|" ;
		strRegex +=aSelected[i].year ;
	}

	var strToolsetXml ="" ;
	for ( var i =0 ; i < aSelected.length ; i++ ) {
		strToolsetXml +="    <PlatformToolset Condition=\"'$(ArxYear)'=='" + aSelected[i].year + "'\">" + aSelected[i].toolset + "</PlatformToolset>\r\n" ;
	}

	var strClrXml ;
	var strNetFwCond ="" ;
	if ( !bClr ) {
		strClrXml ="<CLRSupport>false</CLRSupport>" ;
	} else {
		//- 2025+ mixed modules use .NET (Core), older ones use .NET Framework
		var aCore =[] ;
		var aFramework =[] ;
		for ( var i =0 ; i < aSelected.length ; i++ ) {
			if ( aSelected[i].clrCore )
				aCore.push (aSelected[i].year) ;
			else
				aFramework.push (aSelected[i].year) ;
		}
		strClrXml ="" ;
		if ( aFramework.length > 0 ) {
			strClrXml +="<CLRSupport Condition=\"" + ArxBuildYearCondition (aFramework) + "\">true</CLRSupport>" ;
			//- The framework ObjectARX net property sheets do not reference
			//- System.Core, but mgdinterop.h uses System.Dynamic (IDynamicMetaObjectProvider).
			strNetFwCond =ArxBuildYearCondition (aFramework) ;
		}
		if ( aCore.length > 0 )
			strClrXml +="<CLRSupport Condition=\"" + ArxBuildYearCondition (aCore) + "\">NetCore</CLRSupport>" ;
	}

	var strYears ="" ;
	for ( var i =0 ; i < aSelected.length ; i++ ) {
		if ( i > 0 )
			strYears +="," ;
		strYears +=aSelected[i].year ;
	}

	oWiz.AddSymbol ("ARX_CONFIG_XML", strConfigXml) ;
	oWiz.AddSymbol ("ARX_YEAR_REGEX", strRegex) ;
	oWiz.AddSymbol ("ARX_TOOLSET_XML", strToolsetXml) ;
	oWiz.AddSymbol ("ARX_CLR_XML", strClrXml) ;
	oWiz.AddSymbol ("ARX_NETFW_CONDITION", strNetFwCond) ;
	oWiz.AddSymbol ("ARX_YEARS", strYears) ;
	oWiz.AddSymbol ("ARX_PROPS_DIR", ARX_PROPS_DIR) ;
	return (aSelected) ;
}

//- Get the RDS value
function GetRDSValue () {		
	var macros =window.external.ProjectObject.CodeModel.Macros ;
	//- It is defined as szRDS
	for ( var i =1 ; i <= macros.Count ; i++ ) {
		if ( macros.Item (i).name == "szRDS" ) {
			var szVal =macros.Item (i).value ;
			return (szVal.slice (szVal.indexOf ("\"") + 1, szVal.lastIndexOf ("\""))) ;
		}
	}
	return ("") ;
}

//- Check whether it is ARX / DBX applications
//- * a DBX project has either an acrxEntryPoint function or an AcRxDbxApp class
//-   it does not link to any AutoCAD libs
//-   it has the .dbx extension
//- * an ARX project is a DBX project, plus it links to AutoCAD libs
//-   it has the .arx extension
//- * a ObjectDBX host application can either be a .dll/.exe
//-   it links to rcexeobj.obj
//- CodeClass.IsDerivedFrom raises "access denied" on the C++ code model of VS2022/VS2026. The
//- wizard page's InitDocument then aborted before it set the field defaults (reported 2026-10-20:
//- the Jig / Reactors / .NET Wrapper pages came up empty). Probing defensively keeps the page
//- alive: true / false when the code model answered, and null when it could not - the class's
//- direct Bases are tried second to recover the answer, but a base that is not listed there does
//- not mean "not derived" (the relation is transitive), so that case stays null.
function ArxIsDerivedFrom (cls, strBase) {
	try {
		return (cls.IsDerivedFrom (strBase) ? true : false) ;
	} catch (e) { }
	try {
		for ( var i =1 ; i <= cls.Bases.Count ; i++ ) {
			if ( "" + cls.Bases.Item (i).Name == strBase )
				return (true) ;
		}
	} catch (e1) { }
	return (null) ;
}

function IsDbxProject (prj) {
	//- Verify it is a C++ project
	if ( prj.Kind != '{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}' )
		return (false) ;
	//- Verify project type (Static lib)
	var configType =prj.Object.Configurations (1).ConfigurationType
	if ( configType == 4 ) //- ConfigurationTypes.typeStaticLibrary
		return (true) ;
	//- Verify extension
	var outputName =prj.Object.Configurations (1).PrimaryOutput ;
	if (   outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.dbx'
		&& outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.dll'
	)
		return (false) ;
	//- Verify it has an acrxEntryPoint function or an AcRxDbxApp class
	var codeModel =prj.CodeModel ;
	for ( var i =1 ; i <= codeModel.Functions.Count ; i++ ) {
		var fct =codeModel.Functions.Item (i) ;
		if ( fct.Name == 'acrxEntryPoint' )
			break ;
	}
	for ( var j =1 ; j <= codeModel.Classes.Count ; j++ ) {
		var cls =codeModel.Classes.Item (j) ;
		if ( ArxIsDerivedFrom (cls, 'AcRxArxApp') )
			return (false) ;
		if ( ArxIsDerivedFrom (cls, 'AcRxDbxApp') )
			break ;
	}
	if ( i > codeModel.Functions.Count && j > codeModel.Classes.Count )
		return (false) ;
	//- Verify libs
	var linkTool =prj.Object.Configurations (1).Tools ('VCLinkerTool') ;
	var libs =linkTool.AdditionalDependancies ;
	if ( libs != null ) {
		libs =libs.toLowerCase () ;
		if ( libs.indexOf ('acad.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('acbblclkeditpe.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('acdbmpolygon') != -1 ) return (false) ;
		if ( libs.indexOf ('acedapi.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('acui') != -1 ) return (false) ;
		if ( libs.indexOf ('adui') != -1 ) return (false) ;
		if ( libs.indexOf ('anav.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('oleaprot.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('actc.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('actcui.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('aseapi') != -1 ) return (false) ;
		if ( libs.indexOf ('asiapi') != -1 ) return (false) ;
	}
	//- Verify libs in #pragmas
	// .NET does not provide a way to do this, so we ignore this for now!
	
	return (true) ;
}

function IsDbxHostproject (prj) {
	//- Verify it is a C++ project
	if ( prj.Kind != '{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}' )
		return (false) ;
	//- Verify project type (static lib)
	var configType =prj.Object.Configurations (1).ConfigurationType 
	if ( configType == 4 ) //- ConfigurationTypes.typeStaticLibrary
		return (true) ;
	//- Verify extension
	var outputName =prj.Object.Configurations (1).PrimaryOutput ;
	if (   outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.exe'
		&& outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.dll'
	)
		return (false) ;
	//- Verify it has an acrxEntryPoint function or an AcRxDbxApp class
	var codeModel =prj.CodeModel ;
	for ( var i =1 ; i <= codeModel.Functions.Count ; i++ ) {
		var fct =codeModel.Functions.Item (i) ;
		if ( fct.Name == 'acrxEntryPoint' )
			break ;
	}
	for ( var j =1 ; j <= codeModel.Classes.Count ; j++ ) {
		var cls =codeModel.Classes.Item (j) ;
		if ( ArxIsDerivedFrom (cls, 'AcRxArxApp') )
			return (false) ;
		if ( ArxIsDerivedFrom (cls, 'AcRxDbxApp') )
			break ;
	}
	if ( i > codeModel.Functions.Count && j > codeModel.Classes.Count )
		return (false) ;
	//- Verify libs
	var linkTool =prj.Object.Configurations (1).Tools ('VCLinkerTool') ;
	var libs =linkTool.AdditionalDependancies ;
	if ( libs == null )
		return (false) ;
	libs =libs.toLowerCase () ;
	if ( libs.indexOf ('acad.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('acbblclkeditpe.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('acdbmpolygon') != -1 ) return (false) ;
	if ( libs.indexOf ('acedapi.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('acui') != -1 ) return (false) ;
	if ( libs.indexOf ('adui') != -1 ) return (false) ;
	if ( libs.indexOf ('anav.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('oleaprot.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('actc.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('actcui.lib') != -1 ) return (false) ;
	if ( libs.indexOf ('aseapi') != -1 ) return (false) ;
	if ( libs.indexOf ('asiapi') != -1 ) return (false) ;
	//- For this one, it must be there
	if ( libs.indexOf ('rcexelib.obj') == -1 ) return (false) ;
	//- Verify libs in #pragmas
	// .NET does not provide a way to do this, so we ignore this for now!
	
	return (true) ;
}

function IsCrxProject (prj) {
	//- Verify it is a C++ project
	if ( prj.Kind != '{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}' )
		return (false) ;
	//- Verify project type (static lib)
	var configType =prj.Object.Configurations (1).ConfigurationType 
	if ( configType == 4 ) //- ConfigurationTypes.typeStaticLibrary
		return (true) ;
	//- Verify extension
	var outputName =prj.Object.Configurations (1).PrimaryOutput ;
	if (   outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.crx'
		&& outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.dll'
	)
		return (false) ;
	//- Verify it has an acrxEntryPoint function or an AcRxDbxApp class
	var codeModel =prj.CodeModel ;
	for ( var i =1 ; i <= codeModel.Functions.Count ; i++ ) {
		var fct =codeModel.Functions.Item (i) ;
		if ( fct.Name == 'acrxEntryPoint' )
			break ;
	}
	for ( var j =1 ; j <= codeModel.Classes.Count ; j++ ) {
		var cls =codeModel.Classes.Item (j) ;
		if ( ArxIsDerivedFrom (cls, 'AcRxArxApp') )
			break ;
	}
	if ( i > codeModel.Functions.Count && j > codeModel.Classes.Count )
		return (false) ;
	//- Verify libs
	var linkTool =prj.Object.Configurations (1).Tools ('VCLinkerTool') ;
	var libs =linkTool.AdditionalDependancies ;
	if ( libs != null ) {
		libs =libs.toLowerCase () ;
		if ( libs.indexOf ('acad.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('acbblclkeditpe.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('acedapi.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('adui') != -1 ) return (false) ;
		if ( libs.indexOf ('anav.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('oleaprot.lib') != -1 ) return (false) ;
		if ( libs.indexOf ('aseapi') != -1 ) return (false) ;
		if ( libs.indexOf ('asiapi') != -1 ) return (false) ;
		//- Verify libs in #pragmas
		// .NET does not provide a way to do this, so we ignore this for now!
	}
	return (true) ;
}

function IsArxProject (prj) {
	//- Verify it is a C++ project
	if ( prj.Kind != '{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}' )
		return (false) ;
	//- Verify project type (static lib)
	var configType =prj.Object.Configurations (1).ConfigurationType 
	if ( configType == 4 ) //- ConfigurationTypes.typeStaticLibrary
		return (true) ;
	//- Verify extension
	var outputName =prj.Object.Configurations (1).PrimaryOutput ;
	if (   outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.arx'
		&& outputName.substr (outputName.lastIndexOf ('.')).toLowerCase () != '.dll'
	)
		return (false) ;
	//- Verify it has an acrxEntryPoint function or a class derived from AcRxArxApp. The code model
	//- probe is wrapped: CodeClass.IsDerivedFrom raises "access denied" on VS2022/VS2026 and that
	//- throw propagated out of the wizard page's InitDocument, so the fields came up with no values
	//- (reported 2026-10-20: the Jig / Reactors / .NET Wrapper pages). When the code model cannot
	//- answer, the project is accepted on the checks above rather than failing the wizard.
	try {
		var codeModel =prj.CodeModel ;
		var bEntry =false ;
		for ( var i =1 ; i <= codeModel.Functions.Count ; i++ ) {
			if ( codeModel.Functions.Item (i).Name == 'acrxEntryPoint' ) { bEntry =true ; break ; }
		}
		var bApp =false ;
		for ( var j =1 ; j <= codeModel.Classes.Count ; j++ ) {
			var oDerived =ArxIsDerivedFrom (codeModel.Classes.Item (j), 'AcRxArxApp') ;
			if ( oDerived == true ) { bApp =true ; break ; }
			if ( oDerived == null ) { bApp =true ; break ; }	//- the code model cannot answer
		}
		if ( !bEntry && !bApp )
			return (false) ;
	} catch (e) {
		//- code model unavailable: keep the verdict from the checks above
	}

	return (true) ;
}

function IsValidAdeskProject (prj, level) {
	//- Levels: (bitwise)
	//-   1- .dbx Object Enabler
	//-   2- .arx AutoCAD Application
	//-   4- .exe/.dll ObjectDBX Host Application
	//-   8- .crx AutoCAD Console Application
	var dbx =IsDbxProject (prj) ;
	var crx =IsCrxProject (prj) ;
	var arx =IsArxProject (prj) ;
	var host =IsDbxHostproject (prj) ;

	switch ( level ) {
		case 1: return (dbx) ;
		case 2: return (arx) ;
		case 3: return (dbx | arx) ;
		case 4: return (host) ;
		case 5: return (dbx | host) ;
		case 6: return (arx | host) ;
		case 7: return (dbx | arx | host) ;
		case 8: return (crx) ;
		case 9: return (dbx | crx) ;
		case 10: return (arx | crx) ;
		case 11: return (dbx | arx | crx) ;
		case 12: return (host | crx) ;
		case 13: return (dbx | host | crx) ;
		case 14: return (arx | host | crx) ;
		case 15: return (dbx | arx | host | crx) ;
		default: break ;
	}
	return (false) ;
}

//- Is that class derived at least from AcDbObject
function IsDbxObject(cls) {
    return (ArxIsDerivedFrom (cls, 'AcDbObject'));
}

//- Calling ARX Wizard's help
function InvokeArxWizardHelp () {
	var myUrl =window.external.FindSymbol("ABSOLUTE_PATH") ;
	var myName =window.external.FindSymbol("WIZARD_NAME") ;
	myUrl =myUrl.substring (0, myUrl.lastIndexOf ("\\") + 1) ;
	open ("ms-its:" + myUrl + "ArxWizardHelp.chm::HTML/" + myName + ".htm") ;
}

//- ---------------------------------------------------------------------------------------------
//- Diagnostics (classic line). The pages run in their own script scope and so does the engine's
//- Scripts\<lcid>\default.js; both append to %TEMP%\arxwiz-classic.log, so a scope missing Visual
//- Studio's framework script (Common.js / Script.js) can be told apart from one missing this file
//- or a multi-year helper. The log file is capped below - this runs on every page of every wizard.
//- ---------------------------------------------------------------------------------------------
try {
	var oArxLogFso =new ActiveXObject ("Scripting.FileSystemObject") ;
	var strArxLogFile =oArxLogFso.GetSpecialFolder (2) + "\\arxwiz-classic.log" ;
	if ( oArxLogFso.FileExists (strArxLogFile) == false
		|| oArxLogFso.GetFile (strArxLogFile).Size < 262144 ) {
		var oArxLog =oArxLogFso.OpenTextFile (strArxLogFile, 8, true) ;
		oArxLog.WriteLine (new Date ().toString () + " [page " + document.location.pathname + "] "
			+ "framework OnWizFinish=" + (typeof OnWizFinish)
			+ " SetErrorInfo=" + (typeof SetErrorInfo)
			+ " CreateSafeName=" + (typeof CreateSafeName)
			+ " | this file ArxSetupVersionSymbols=" + (typeof ArxSetupVersionSymbols)
			+ " | SCRIPT_COMMON_PATH=" + window.external.FindSymbol ("SCRIPT_COMMON_PATH")) ;
		oArxLog.Close () ;
	}
} catch (e) { }

//- ---------------------------------------------------------------------------------------------
//- Engine-scope logging helpers (classic line). The engine's script scope (Scripts\<lcid>\default.js)
//- does not see the pages' variables; the wizard scripts evaluate this file into their scope (see
//- the project and item wizard scripts), which is where these come from. They are only defined when
//- the file doing the evaluating does not bring its own, so the project wizard keeps its longer
//- scope dump. The format matches the page-side line above, one file for the whole line.
//- ---------------------------------------------------------------------------------------------
var ARX_LOG_FILE ="arxwiz-classic.log" ;

if ( typeof ArxWizLog == "undefined" ) {
	function ArxWizLog (strMsg) {
		try {
			var oArxWizLogFso =new ActiveXObject ("Scripting.FileSystemObject") ;
			var strArxWizLogFile =oArxWizLogFso.GetSpecialFolder (2) + "\\" + ARX_LOG_FILE ;
			var oArxWizLog =oArxWizLogFso.OpenTextFile (strArxWizLogFile, 8, true) ;
			oArxWizLog.WriteLine (new Date ().toString () + " ["
				+ (typeof ARX_WIZARD_NAME == "undefined" ? "arxCommon" : ARX_WIZARD_NAME) + "] " + strMsg) ;
			oArxWizLog.Close () ;
		} catch (e) { }
	}
}

if ( typeof ArxWizErrorText == "undefined" ) {
	//- The engine's error object often has no description at all ("缺少对象" is all the wizard host
	//- ever shows); this reports every field it does have.
	function ArxWizErrorText (e) {
		var aArxWizParts =[] ;
		try { aArxWizParts.push ("description=[" + e.description + "]") ; } catch (e1) { aArxWizParts.push ("description=<none>") ; }
		try { aArxWizParts.push ("message=[" + e.message + "]") ; } catch (e2) { }
		try { aArxWizParts.push ("name=[" + e.name + "]") ; } catch (e3) { }
		try {
			var nArxWizNumber =e.number ;
			aArxWizParts.push ("number=0x" + (nArxWizNumber < 0 ? (0x100000000 + nArxWizNumber) : nArxWizNumber).toString (16)) ;
		} catch (e4) { }
		return (aArxWizParts.join (" ")) ;
	}
}

if ( typeof ArxWizScope == "undefined" ) {
	//- typeof never throws for an absent identifier, so this reports the scope in one line.
	function ArxWizScope () {
		return ("framework CreateSafeName=" + (typeof CreateSafeName)
			+ " SetupFilters=" + (typeof SetupFilters)
			+ " RenderAddTemplate=" + (typeof RenderAddTemplate)
			+ " SetErrorInfo=" + (typeof SetErrorInfo)
			+ " | multi-year ArxSetupVersionSymbols=" + (typeof ArxSetupVersionSymbols)
			+ " | engine wizard=" + (typeof wizard)) ;
	}
}

if ( typeof ArxWizSymbol == "undefined" ) {
	//- A symbol that was never set is the usual reason an item wizard dies on "缺少对象": the engine
	//- reads it back and passes undefined to RenderAddTemplate.
	function ArxWizSymbol (strName) {
		try {
			var vSymbol =wizard.FindSymbol (strName) ;
			return (vSymbol == null ? "<null>" : String (vSymbol)) ;
		} catch (e) {
			return ("<error>") ;
		}
	}
}

//- ---------------------------------------------------------------------------------------------
//- Tooltips (classic line). The pages carry their help text in TITLE="..." attributes, and the host
//- that draws the native tooltip renders an empty black box for them on this machine - every page,
//- every control (reported 2026-10-11). Move that text out of the host's hands and show it in a
//- layer of our own: light background, dark text, next to the cursor. Every page gets this for free
//- because they all include this file. The engine scope has no document, hence the guard, and the
//- handlers are attached with attachEvent so a page's own document.onmouseover is left alone.
//- ---------------------------------------------------------------------------------------------
if ( typeof document != "undefined" && document != null ) {
	var gArxTipNode =null ;
	var gArxTipOrigin =null ;

	function ArxTipCreate () {
		if ( gArxTipNode )
			return (gArxTipNode) ;
		var oArxTip =document.createElement ("DIV") ;
		oArxTip.style.position ="absolute" ;
		oArxTip.style.display ="none" ;
		oArxTip.style.zIndex ="32767" ;
		oArxTip.style.maxWidth ="420px" ;
		oArxTip.style.padding ="4px 7px" ;
		oArxTip.style.backgroundColor ="#FFFFE1" ;
		oArxTip.style.color ="#000000" ;
		oArxTip.style.border ="1px solid #808080" ;
		oArxTip.style.fontFamily ="Tahoma, MS Sans Serif, sans-serif" ;
		oArxTip.style.fontSize ="11px" ;
		oArxTip.style.lineHeight ="14px" ;
		document.body.appendChild (oArxTip) ;
		gArxTipNode =oArxTip ;
		return (oArxTip) ;
	}

	//- The TITLE sits on the SPAN that wraps a control as often as on the control itself. An *empty*
	//- TITLE counts too: the host draws its black box for those as well, so it has to be taken away
	//- (the pages set title="" on the year checkboxes), only the text is skipped when showing.
	function ArxTipElement (oFrom) {
		for ( var oNode =oFrom ; oNode != null && oNode != document.body ; oNode =oNode.parentElement ) {
			if ( oNode.__arxTip || (oNode.getAttribute && oNode.getAttribute ("title") != null) )
				return (oNode) ;
		}
		return (null) ;
	}

	function ArxTipHide () {
		gArxTipOrigin =null ;
		if ( gArxTipNode )
			gArxTipNode.style.display ="none" ;
	}

	function ArxTipPlace (oEvent) {
		if ( gArxTipNode == null || gArxTipNode.style.display == "none" )
			return ;
		var nLeft =oEvent.clientX + 16 ;
		var nTop =oEvent.clientY + 22 ;
		var nMaxLeft =document.body.clientWidth + document.body.scrollLeft - gArxTipNode.offsetWidth - 8 ;
		if ( nLeft > nMaxLeft )
			nLeft =nMaxLeft ;
		gArxTipNode.style.left =(nLeft + document.body.scrollLeft) + "px" ;
		gArxTipNode.style.top =(nTop + document.body.scrollTop) + "px" ;
	}

	function ArxTipOnMouseOver (oEvent) {
		var oNode =ArxTipElement (oEvent.srcElement ? oEvent.srcElement : oEvent.target) ;
		if ( oNode == null ) {
			ArxTipHide () ;
			return ;
		}
		if ( oNode == gArxTipOrigin ) {
			ArxTipPlace (oEvent) ;
			return ;
		}
		var strText =oNode.__arxTip ? oNode.__arxTip : oNode.getAttribute ("title") ;
		//- Take the attribute away - an empty one too, the host draws its black box for those as
		//- well - and only then decide whether there is anything to show.
		if ( oNode.getAttribute && oNode.getAttribute ("title") != null ) {
			oNode.__arxTip =strText == null ? "" : strText ;
			oNode.removeAttribute ("title") ;
		}
		if ( !strText ) {
			ArxTipHide () ;
			return ;
		}
		var oTip =ArxTipCreate () ;
		oTip.innerHTML =strText ;
		oTip.style.display ="block" ;
		gArxTipOrigin =oNode ;
		ArxTipPlace (oEvent) ;
	}

	function ArxTipInstall () {
		if ( document.attachEvent ) {
			document.attachEvent ("onmouseover", ArxTipOnMouseOver) ;
			document.attachEvent ("onmousemove", ArxTipPlace) ;
			document.attachEvent ("onmouseout", ArxTipHide) ;
			document.attachEvent ("onclick", ArxTipHide) ;
		} else if ( document.addEventListener ) {
			document.addEventListener ("mouseover", ArxTipOnMouseOver, false) ;
			document.addEventListener ("mousemove", ArxTipPlace, false) ;
			document.addEventListener ("mouseout", ArxTipHide, false) ;
			document.addEventListener ("click", ArxTipHide, false) ;
		}
	}

	ArxTipInstall () ;
}

//- ---------------------------------------------------------------------------------------------
//- Step navigation (classic line, page scope).
//-
//- The Previous/Next buttons are built by the framework (initItems() in Visual Studio's common.js)
//- and call getPrevPage()/getNextPage(), which derive the target page from the left pane markup:
//- they walk the DIVs the TABLE class="THREE" hands out and use their *indices*. On these pages
//- that collection comes back with repeated entries - dumped on MFC Support it is 0,1,2,2,2,2,3,4,5
//- instead of 0..5 - so "Previous" from MFC Support asked for .NET Options and the wizard's step
//- tracking was off (reported 2026-10-11). The two functions are replaced here by a lookup over the
//- same tabs in document order; the buttons resolve them by name when they are clicked, so no DOM
//- surgery and no timing is involved. A tab the page marked disabled is stepped over, which is how
//- the framework's own version skips steps.
//- ---------------------------------------------------------------------------------------------
//- The wizard's steps, taken from the page's own tab_array (tab_array [0] =Default ; tab_array [1]
//- =Protocols ; ...), which every page of this line declares in the order the left pane shows them.
//- The DOM that draws those tabs is deliberately not used: MSHTML hands the nav DIVs out *twice* on
//- pages whose markup nests a table inside a span (MFC Support comes back as Default AppType Version
//- MFC COM dotNET MFC COM dotNET), which is what made the framework's own getNextPage()/
//- getPrevPage() - and the first version of this lookup - point at the wrong step.
function ArxNavTabs () {
	var aTabs =[] ;
	try {
		if ( typeof tab_array != "undefined" && tab_array != null && tab_array.length > 1 ) {
			for ( var i =0 ; i < tab_array.length ; i++ ) {
				if ( tab_array[i] == null )
					continue ;
				var strId =ArxResStr (tab_array[i].id) ;
				if ( strId != "" )
					aTabs[aTabs.length] = { id:strId, span:tab_array[i] } ;
			}
		}
	} catch (e) { }
	return (aTabs) ;
}

//- The page this is running in, without folder and extension ("Version" for .../Version.htm).
function ArxNavHere () {
	var strPath =ArxResStr (typeof window != "undefined" && window.location ? window.location.pathname : "") ;
	var nSlash =strPath.lastIndexOf ("/") ;
	var nBack =strPath.lastIndexOf ("\\") ;
	if ( nBack > nSlash )
		nSlash =nBack ;
	var strName =(nSlash < 0 ? strPath : strPath.substring (nSlash + 1)) ;
	var nDot =strName.lastIndexOf (".") ;
	if ( nDot > 0 )
		strName =strName.substring (0, nDot) ;
	return (strName) ;
}

//- The page the next (nDirection > 0) or previous step shows, as "<name>.htm"; the current page's
//- own name when there is no step in that direction.
function ArxNavPage (nDirection) {
	var aTabs =ArxNavTabs () ;
	var strHere =ArxNavHere ().toLowerCase () ;
	var nCurrent =-1 ;
	for ( var i =0 ; i < aTabs.length ; i++ ) {
		if ( aTabs[i].id.toLowerCase () == strHere )
			nCurrent =i ;
	}
	if ( nCurrent < 0 )
		return ("") ;
	for ( var i =nCurrent + nDirection ; i >= 0 && i < aTabs.length ; i += nDirection ) {
		if ( !aTabs[i].span.disabled )	//- a step the page marked skipped
			return (aTabs[i].id + ".htm") ;
	}
	return (aTabs[nCurrent].id + ".htm") ;
}

//- Wires one of the two framework-built buttons to the step looked up above, and makes it usable
//- again when the framework disabled it because it could not work out a step in that direction.
function ArxNavWire (strButton, nDirection) {
	try {
		var oButton =document.getElementById (strButton) ;
		if ( oButton == null )
			return ;
		var strTarget =ArxNavPage (nDirection) ;
		if ( strTarget == "" || strTarget.toLowerCase () == (ArxNavHere ().toLowerCase () + ".htm") ) {
			oButton.disabled =true ;	//- first step / last step: nowhere to go
			return ;
		}
		oButton.disabled =false ;
		oButton.onclick =function () {
			try {
				bValidating =true ;
				if ( typeof ValidateInput == "function" && !ValidateInput () ) {
					bValidating =false ;
					return ;
				}
			} catch (e) { }
			Next (document, ArxNavPage (nDirection)) ;
		} ;
	} catch (e) { }
}

function ArxNavInstall () {
	//- The framework's own two functions are replaced as well: the buttons resolve them by name, so
	//- whatever path calls them gets the right step.
	try { window.getNextPage =function () { return (ArxNavPage (1)) ; } ; } catch (e) { }
	try { window.getPrevPage =function () { return (ArxNavPage (-1)) ; } ; } catch (e1) { }
	//- Build the buttons if the host has not done it yet (initItems returns early when they are
	//- there) and re-point them.
	try { if ( typeof initItems == "function" ) initItems () ; } catch (e2) { }
	ArxNavWire ("PreviousBtn", -1) ;
	ArxNavWire ("NextBtn", 1) ;
	//- The background the framework paints on the current tab, done here as well: initItems() returns
	//- before that part when the buttons are already there, whoever put them there.
	try {
		var aTables =document.getElementsByTagName ("TABLE") ;
		for ( var i =0 ; i < aTables.length ; i++ ) {
			var strClass =ArxResStr (aTables[i].className).toLowerCase () ;
			if ( strClass != "linktextselected" && strClass != "linktextselectedindent" )
				continue ;
			var aSpans =aTables[i].getElementsByTagName ("SPAN") ;
			for ( var j =0 ; j < aSpans.length ; j++ ) {
				aSpans[j].style.padding ="4px" ;
				aSpans[j].style.left ="-2px" ;
				aSpans[j].style.backgroundColor ="InactiveBorder" ;
				aSpans[j].style.lineHeight ="1.7em" ;
			}
		}
	} catch (e3) { }
}

if ( typeof document != "undefined" && document != null ) {
	//- Now, and then on a timer: the framework's own copy is pulled in through a script element of
	//- its own and lands when it lands (it can arrive after this file and put its own two functions
	//- back). Re-asserting beats guessing a delay; it stops by itself after ten seconds.
	var g_ArxNavTicks =0 ;
	try {
		ArxNavInstall () ;
		var g_ArxNavTimer =setInterval (function () {
			g_ArxNavTicks++ ;
			if ( g_ArxNavTicks > 40 ) {
				clearInterval (g_ArxNavTimer) ;
				return ;
			}
			ArxNavInstall () ;
		}, 250) ;
	} catch (e) { }
}

//- ---------------------------------------------------------------------------------------------
//- Which target years are actually installed (classic line, page scope).
//-
//- The setup generates one property sheet per ticked year (Autodesk.arx-<year>.props, written by
//- arx-genprops), so the presence of that file is what "this year is installed" means - the wizard
//- version page disables the years without one instead of letting the page tick a year whose build
//- configuration can never build (asked for 2026-10-11).
//-
//- The folder is the install folder by default and the setup records its choice in
//- HKLM\SOFTWARE\Autodesk\ObjectARX Wizards\PropsDir; both are tried, and *when neither holds any
//- props file at all the answer is "unknown"* - a wizard that cannot see the props must not disable
//- years, that would only take the feature away.
//- ---------------------------------------------------------------------------------------------
function ArxPropsDirs () {
	var aDirs =[] ;
	try {
		var oShell =new ActiveXObject ("WScript.Shell") ;
		var aKeys =[ "HKLM\\SOFTWARE\\Autodesk\\ObjectARX Wizards\\PropsDir",
			"HKLM\\SOFTWARE\\Wow6432Node\\Autodesk\\ObjectARX Wizards\\PropsDir" ] ;
		for ( var i =0 ; i < aKeys.length ; i++ ) {
			try {
				var strValue =ArxResStr (oShell.RegRead (aKeys[i])) ;
				if ( strValue != "" )
					aDirs[aDirs.length] =strValue ;
			} catch (e) { }
		}
	} catch (e) { }
	try {
		var strWizardDir =ArxResStr (window.external.FindSymbol ("ABSOLUTE_PATH")) ;
		var nSlash =strWizardDir.lastIndexOf ("\\") ;
		if ( nSlash > 0 )
			aDirs[aDirs.length] =strWizardDir.substring (0, nSlash) ;
	} catch (e1) { }
	return (aDirs) ;
}

//- { "<year>":1, ... } for the years whose props file is there, or null when no props file was found
//- in any of the folders - i.e. when the installed set cannot be told.
var g_ArxInstalledYears =undefined ;

function ArxInstalledYears () {
	if ( typeof g_ArxInstalledYears != "undefined" )
		return (g_ArxInstalledYears) ;
	g_ArxInstalledYears =null ;
	try {
		var oFso =ArxResFso () ;
		var aDirs =ArxPropsDirs () ;
		for ( var nDir =0 ; nDir < aDirs.length ; nDir++ ) {
			var oYears ={} ;
			var nFound =0 ;
			for ( var i =0 ; i < g_ArxVersions.length ; i++ ) {
				try {
					if ( oFso.FileExists (aDirs[nDir] + "\\Autodesk.arx-" + g_ArxVersions[i].year + ".props") ) {
						oYears[g_ArxVersions[i].year] =1 ;
						nFound++ ;
					}
				} catch (e) { }
			}
			ArxResLog ("props in " + aDirs[nDir] + ": " + nFound + " year(s)") ;
			if ( nFound > 0 ) {
				g_ArxInstalledYears =oYears ;
				break ;
			}
		}
	} catch (e1) { }
	return (g_ArxInstalledYears) ;
}

//- Is that year installed? True when the installed set cannot be told (see above).
function ArxYearInstalled (strYear) {
	var oYears =ArxInstalledYears () ;
	if ( oYears == null )
		return (true) ;
	return (oYears[strYear] == 1) ;
}

//- ---------------------------------------------------------------------------------------------
//- Project resource files, edited as text (classic line, page scope).
//-
//- The classic item wizards used to let the wizard host do this: window.external.GetDialogIds()
//- for the Names page, wizard.ResourceHelper.AddResource() to add a DIALOG or a REGISTRY
//- resource. The host routes both through its resource service, which compiles the project's .rc
//- with the project's *active configuration*; when that configuration's toolset is incomplete -
//- reported 2026-10-11: "fatal error RC1015: cannot open include file 'winres.h'" out of
//- VS2017's atlmfc include, the active configuration being v141 - the resource service yields
//- nothing at all, and the wizard then dies on a bare "Object required" (a null handed back by
//- AddResource and dereferenced by the calling script).
//-
//- These helpers do the same edits as plain text, in the page's own script scope, where writing
//- files demonstrably works (every page writes this line's log). No resource compiler and no
//- project configuration is involved: the new id is allocated above the highest one the project
//- knows, exactly like the resource editor would do it.
//-
//- The generated code depends on the id being defined, so the callers report a failed edit to the
//- user instead of swallowing it; see ArxResErrText().
//- ---------------------------------------------------------------------------------------------
function ArxResFso () {
	return (new ActiveXObject ("Scripting.FileSystemObject")) ;
}

function ArxResLog (strMsg) {
	try {
		var strFile =ArxResFso ().GetSpecialFolder (2) + "\\arxwiz-classic.log" ;
		var oStream =ArxResFso ().OpenTextFile (strFile, 8, true) ;
		oStream.WriteLine (new Date ().toString () + " [res] " + strMsg) ;
		oStream.Close () ;
	} catch (e) { }
}

//- Text of any error, whatever shape it arrives in. A missing description is what turns a real
//- failure into the bare "Object required" / "Object expected" the host shows.
function ArxResErrText (e) {
	var strText ="" ;
	var nNumber =0 ;
	try { nNumber =e.number ; } catch (e0) { }
	try { strText =e.description == null ? "" : String (e.description) ; } catch (e1) { }
	if ( strText == "" ) { try { strText =e.message == null ? "" : String (e.message) ; } catch (e2) { } }
	if ( strText == "" )
		strText ="no description" ;
	if ( nNumber == -2146823281 )	//- 0x800A138F: a property was read from null/undefined
		strText +=" (0x800A138F - a symbol, project object or resource the wizard read back was null)" ;
	return (strText) ;
}

function ArxResStr (v) {
	if ( v == null || typeof (v) == "undefined" )
		return ("") ;
	var strValue ="" + v ;
	if ( strValue == "undefined" || strValue == "null" )
		return ("") ;
	return (strValue) ;
}

function ArxResMatchName (strName, strPattern) {
	var strRegEx ="^" + strPattern.replace (/\./g, "\\.").replace (/\*/g, ".*") + "$" ;
	return ((new RegExp (strRegEx, "i")).test (strName)) ;
}

//- First file in strDir whose name matches strPattern ("*.rc", "resource.h"); "" when there is none.
function ArxResFindFile (strDir, strPattern) {
	try {
		var oFso =ArxResFso () ;
		if ( strDir == "" || !oFso.FolderExists (strDir) )
			return ("") ;
		var oEnum =new Enumerator (oFso.GetFolder (strDir).Files) ;
		for ( ; !oEnum.atEnd () ; oEnum.moveNext () ) {
			var oFile =oEnum.item () ;
			if ( ArxResMatchName (String (oFile.Name), strPattern) )
				return (String (oFile.Path)) ;
		}
	} catch (e) { }
	return ("") ;
}

//- The project folder: PROJECT_PATH (the symbol the pages already browse with), else the folder of
//- the project file. No trailing separator.
function ArxResProjectDir () {
	var strPath ="" ;
	try { strPath =ArxResStr (window.external.FindSymbol ("PROJECT_PATH")) ; } catch (e) { }
	if ( strPath == "" ) {
		try { strPath =ArxResStr (window.external.ProjectObject.FullName) ; } catch (e1) { }
		if ( strPath != "" ) {
			var nSlash =strPath.lastIndexOf ("\\") ;
			if ( nSlash > 0 )
				strPath =strPath.substring (0, nSlash) ;
		}
	}
	while ( strPath.length > 0 && (strPath.charAt (strPath.length - 1) == "\\" || strPath.charAt (strPath.length - 1) == "/") )
		strPath =strPath.substring (0, strPath.length - 1) ;
	return (strPath) ;
}

function ArxResReadText (strPath) {
	var oStream =ArxResFso ().OpenTextFile (strPath, 1, false, -2) ;
	var strText =oStream.ReadAll () ;
	oStream.Close () ;
	return (strText) ;
}

function ArxResWriteText (strPath, strText) {
	var oStream =ArxResFso ().OpenTextFile (strPath, 2, true, -2) ;
	oStream.Write (strText) ;
	oStream.Close () ;
}

//- <dir>\<name>, uniquified the way the wizard used to do it: Name.rgs, Name1.rgs, Name2.rgs ...
function ArxResUniqueFileName (strDir, strName) {
	try {
		var oFso =ArxResFso () ;
		if ( !oFso.FileExists (strDir + "\\" + strName) )
			return (strName) ;
		var nDot =strName.lastIndexOf (".") ;
		var strBase =(nDot < 0 ? strName : strName.substring (0, nDot)) ;
		var strExt =(nDot < 0 ? "" : strName.substring (nDot)) ;
		for ( var nTry =1 ; nTry < 1000 ; nTry++ ) {
			if ( !oFso.FileExists (strDir + "\\" + strBase + nTry + strExt) )
				return (strBase + nTry + strExt) ;
		}
	} catch (e) { }
	return (strName) ;
}

//- The project's resource script and its resource header, or "" for the one that is missing.
function ArxResFiles () {
	var oFiles ={ dir:"", rc:"", header:"" } ;
	oFiles.dir =ArxResProjectDir () ;
	if ( oFiles.dir == "" )
		return (oFiles) ;
	oFiles.rc =ArxResFindFile (oFiles.dir, "*.rc") ;
	oFiles.header =ArxResFindFile (oFiles.dir, "resource.h") ;
	return (oFiles) ;
}

//- Names of the DIALOG resources defined in the text of a .rc (what GetDialogIds() used to list).
function ArxResDialogIds (strRcText) {
	var aIds =[] ;
	var reDialog =/^[ \t]*([A-Za-z_][A-Za-z0-9_]*)[ \t]+DIALOG/i ;
	var aLines =strRcText.replace (/\r\n/g, "\n").split ("\n") ;
	for ( var nLine =0 ; nLine < aLines.length ; nLine++ ) {
		var aMatch =reDialog.exec (aLines [nLine]) ;
		if ( aMatch == null )
			continue ;
		var bKnown =false ;
		for ( var nId =0 ; nId < aIds.length ; nId++ ) {
			if ( aIds [nId] == aMatch [1] ) {
				bKnown =true ;
				break ;
			}
		}
		if ( !bKnown )
			aIds [aIds.length] =aMatch [1] ;
	}
	return (aIds) ;
}

//- A dialog template (DIALOG 0, 0, 186, 95 / DIALOG DISCARDABLE 0, 0, ...) with the id in front.
function ArxResNameDialog (strTemplateText, strId) {
	var strText =strTemplateText.replace (/\r\n/g, "\n") ;
	var nEnd =strText.indexOf ("\n") ;
	var strFirst =(nEnd < 0 ? strText : strText.substring (0, nEnd)) ;
	var strRest =(nEnd < 0 ? null : strText.substring (nEnd + 1)) ;
	var strTrimmed =strFirst.replace (/^[ \t]+/, "") ;
	var strRenamed ;
	if ( /^DIALOG/i.test (strTrimmed) )
		strRenamed =strId + " " + strTrimmed ;
	else {
		var nSpace =strTrimmed.search (/[ \t]/) ;
		strRenamed =(nSpace < 0 ? strId + " " + strTrimmed : strId + strTrimmed.substring (nSpace)) ;
	}
	var strResult =(strRest == null ? strRenamed : strRenamed + "\n" + strRest) ;
	return (strResult.replace (/\n/g, "\r\n")) ;
}

//- Free resource value: _APS_NEXT_RESOURCE_VALUE when the header keeps it, else above every value
//- the header already defines.
function ArxResNextValue (strHeaderText) {
	var aNext =/#define[ \t]+_APS_NEXT_RESOURCE_VALUE[ \t]+([0-9]+)/i.exec (strHeaderText) ;
	if ( aNext != null )
		return (parseInt (aNext [1], 10)) ;
	var nMax =99 ;
	var reDefine =/^[ \t]*#define[ \t]+[A-Za-z_][A-Za-z0-9_]*[ \t]+([0-9]+)[ \t]*$/gmi ;
	var aDefine ;
	while ( (aDefine =reDefine.exec (strHeaderText)) != null ) {
		var nValue =parseInt (aDefine [1], 10) ;
		if ( nValue > nMax )
			nMax =nValue ;
	}
	return (nMax + 1) ;
}

//- Header text with "#define <id> <value>" in it and _APS_NEXT_RESOURCE_VALUE moved past it.
//- The new id goes above the resource editor's bookkeeping block, anchored on the "// Next default
//- values for new objects" comment (else on "#ifdef APSTUDIO_INVOKED"). Everything from that block
//- down is the editor's own _APS_NEXT_* bookkeeping, and a normal build skips the APSTUDIO_INVOKED
//- region - so a define placed inside that region leaves the id undefined for the generated code
//- (reported 2026-10-19: "IDD_<class>" landed inside the block and the dialog class would not
//- compile - the id was "gone" again; the VSIX line was fixed for exactly this on 2026-10-08).
function ArxResDefineId (strHeaderText, strId, nValue) {
	var strText =strHeaderText ;
	var strDefine ="#define " + strId + " " + nValue ;
	if ( !(new RegExp ("^[ \\t]*#define[ \\t]+" + strId + "\\b", "mi")).test (strText) ) {
		var aAnchor =/^[ \t]*\/\/ Next default values for new objects/mi.exec (strText) ;
		if ( aAnchor == null )
			aAnchor =/^[ \t]*#ifdef APSTUDIO_INVOKED/mi.exec (strText) ;
		if ( aAnchor != null )
			strText =strText.substring (0, aAnchor.index).replace (/[ \t\r\n]+$/, "") + "\r\n" + strDefine + "\r\n\r\n" + strText.substring (aAnchor.index) ;
		else
			strText =strText.replace (/[ \t\r\n]+$/, "") + "\r\n" + strDefine + "\r\n" ;
	}
	//- aValue.length is the *array* length (full match + groups), aValue [0].length the match text.
	var aValue =/(#define[ \t]+_APS_NEXT_RESOURCE_VALUE[ \t]+)([0-9]+)/i.exec (strText) ;
	if ( aValue != null )
		strText =strText.substring (0, aValue.index + aValue [1].length) + (nValue + 1) + strText.substring (aValue.index + aValue [0].length) ;
	else
		strText =strText.replace (/[ \t\r\n]+$/, "") + "\r\n#define _APS_NEXT_RESOURCE_VALUE " + (nValue + 1) + "\r\n" ;
	return (strText) ;
}

//- A template from this wizard's Templates folder (dialog.rc / childDialog.rc), located next to the
//- page rather than through a host symbol, with the host's locale tried before the 1033 copy.
function ArxResTemplatePath (strFile) {
	var strRoot ="" ;
	try { strRoot =ArxResStr (window.external.FindSymbol ("ABSOLUTE_PATH")) ; } catch (e) { }
	if ( strRoot == "" )
		return ("") ;
	var aTries =[ strRoot + "\\Templates\\1033\\" + strFile, strRoot + "\\Templates\\" + ArxResStr (window.external.GetHostLocale ()) + "\\" + strFile ] ;
	try {
		var oFso =ArxResFso () ;
		for ( var nTry =0 ; nTry < aTries.length ; nTry++ ) {
			if ( oFso.FileExists (aTries [nTry]) )
				return (aTries [nTry]) ;
		}
	} catch (e1) { }
	return (aTries [0]) ;
}

//- The dialog ids of the project's .rc ([] when there is no resource script to read).
function ArxResDialogList () {
	try {
		var oFiles =ArxResFiles () ;
		if ( oFiles.rc == "" ) {
			ArxResLog ("no .rc next to the project file (" + oFiles.dir + ")") ;
			return ([]) ;
		}
		var aIds =ArxResDialogIds (ArxResReadText (oFiles.rc)) ;
		ArxResLog ("dialog ids in " + oFiles.rc + ": " + aIds.join (", ")) ;
		return (aIds) ;
	} catch (e) {
		ArxResLog ("reading the project's .rc failed: " + ArxResErrText (e)) ;
		return ([]) ;
	}
}

//- Adds a dialog resource to the project's .rc / resource.h. Returns { ok, value, message }: the
//- caller decides whether a failure is worth a message box.
function ArxResAddDialog (strId, strTemplatePath) {
	try {
		if ( strId == null || strId == "" )
			return ({ ok:false, value:0, message:"the dialog id is empty" }) ;
		var oFiles =ArxResFiles () ;
		if ( oFiles.dir == "" )
			return ({ ok:false, value:0, message:"the project folder is unknown" }) ;
		if ( oFiles.rc == "" )
			return ({ ok:false, value:0, message:"no .rc file next to the project file (" + oFiles.dir + ")" }) ;
		if ( oFiles.header == "" )
			return ({ ok:false, value:0, message:"no resource.h next to the project file (" + oFiles.dir + ")" }) ;
		var strRcText =ArxResReadText (oFiles.rc) ;
		var strHeaderText =ArxResReadText (oFiles.header) ;
		if ( (new RegExp ("^[ \\t]*#define[ \\t]+" + strId + "\\b", "mi")).test (strHeaderText ) )
			return ({ ok:true, value:0, message:"'" + strId + "' is already defined in " + oFiles.header + ", nothing to add" }) ;
		if ( (new RegExp ("^[ \\t]*" + strId + "[ \\t]+DIALOG", "mi")).test (strRcText) )
			return ({ ok:true, value:0, message:"'" + strId + "' is already a DIALOG in " + oFiles.rc + ", nothing to add" }) ;
		if ( strTemplatePath == "" || !ArxResFso ().FileExists (strTemplatePath) )
			return ({ ok:false, value:0, message:"the dialog template was not found (" + strTemplatePath + ")" }) ;
		var nValue =ArxResNextValue (strHeaderText) ;
		var strBlock =ArxResNameDialog (ArxResReadText (strTemplatePath), strId) ;
		ArxResWriteText (oFiles.rc, strRcText.replace (/[ \t\r\n]+$/, "") + "\r\n\r\n" + strBlock + "\r\n") ;
		ArxResWriteText (oFiles.header, ArxResDefineId (strHeaderText, strId, nValue)) ;
		return ({ ok:true, value:nValue, message:"'" + strId + "' = " + nValue + " appended to " + oFiles.rc }) ;
	} catch (e) {
		return ({ ok:false, value:0, message:ArxResErrText (e) }) ;
	}
}

//- Adds "<id> REGISTRY DISCARDABLE "<file>"" for a .rgs file to the project's .rc / resource.h.
//- Same contract as ArxResAddDialog(); the caller has already claimed the .rgs file name.
function ArxResAddRegistry (strId, strRgsFile) {
	try {
		if ( strId == null || strId == "" || strRgsFile == null || strRgsFile == "" )
			return ({ ok:false, value:0, message:"the registry resource id / file name is empty" }) ;
		var oFiles =ArxResFiles () ;
		if ( oFiles.dir == "" )
			return ({ ok:false, value:0, message:"the project folder is unknown" }) ;
		if ( oFiles.rc == "" )
			return ({ ok:false, value:0, message:"no .rc file next to the project file (" + oFiles.dir + ")" }) ;
		if ( oFiles.header == "" )
			return ({ ok:false, value:0, message:"no resource.h next to the project file (" + oFiles.dir + ")" }) ;
		var strRcText =ArxResReadText (oFiles.rc) ;
		var strHeaderText =ArxResReadText (oFiles.header) ;
		if ( (new RegExp ("\\b" + strId + "[ \\t]+REGISTRY\\b", "i")).test (strRcText) )
			return ({ ok:true, value:0, message:"'" + strId + "' is already a REGISTRY entry in " + oFiles.rc + ", nothing to add" }) ;
		if ( (new RegExp ("^[ \\t]*#define[ \\t]+" + strId + "\\b", "mi")).test (strHeaderText) )
			return ({ ok:true, value:0, message:"'" + strId + "' is already defined in " + oFiles.header + ", nothing to add" }) ;
		var nValue =ArxResNextValue (strHeaderText) ;
		ArxResWriteText (oFiles.rc, strRcText.replace (/[ \t\r\n]+$/, "") + "\r\n" + strId + " REGISTRY DISCARDABLE \"" + strRgsFile + "\"\r\n") ;
		ArxResWriteText (oFiles.header, ArxResDefineId (strHeaderText, strId, nValue)) ;
		return ({ ok:true, value:nValue, message:"'" + strId + "' = " + nValue + " and " + strRgsFile + " appended to " + oFiles.rc }) ;
	} catch (e) {
		return ({ ok:false, value:0, message:ArxResErrText (e) }) ;
	}
}
