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
		if ( cls.IsDerivedFrom ('AcRxArxApp') )
			return (false) ;
		if ( cls.IsDerivedFrom ('AcRxDbxApp') )
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
		if ( cls.IsDerivedFrom ('AcRxArxApp') )
			return (false) ;
		if ( cls.IsDerivedFrom ('AcRxDbxApp') )
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
		if ( cls.IsDerivedFrom ('AcRxArxApp') )
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
	//- Verify it has an acrxEntryPoint function or an AcRxDbxApp class
	var codeModel =prj.CodeModel ;
	for ( var i =1 ; i <= codeModel.Functions.Count ; i++ ) {
		var fct =codeModel.Functions.Item (i) ;
		if ( fct.Name == 'acrxEntryPoint' )
			break ;
	}
	for ( var j =1 ; j <= codeModel.Classes.Count ; j++ ) {
		var cls =codeModel.Classes.Item (j) ;
		if ( cls.IsDerivedFrom ('AcRxArxApp') )
			break ;
	}
	if ( i > codeModel.Functions.Count && j > codeModel.Classes.Count )
		return (false) ;

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
    return (cls.IsDerivedFrom('AcDbObject'));
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
