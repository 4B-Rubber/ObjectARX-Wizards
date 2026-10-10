//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauvel - Autodesk Developer Technical Services

//- ---------------------------------------------------------------------------------------------
//- Diagnostics (classic line). The engine's script scope is not the HTML pages' scope: it preloads
//- Visual Studio's own framework script but it does not see ArxWizCommon\arxCommon.js, and a failure
//- in here only ever surfaces as a message box with a bare description ("Object required" and the
//- like). arxCommon.js is evaluated into this scope at file level (bottom of this file) and every
//- run appends to %TEMP%\arxwiz-classic.log - the same file the pages and the project wizards write.
//- ---------------------------------------------------------------------------------------------
var ARX_WIZARD_NAME ="ArxAtlWizDynProp" ;
function ArxItemOnFinishImpl (selProj, selObj) {
	var oCM ;
	try {
		oCM	=selProj.CodeModel ;

		var strShortName =wizard.FindSymbol ("SHORT_NAME") ;
		var L_TRANSACTION_Text ="Add an AutoCAD object dynamic property object. " ;
		oCM.StartTransaction (L_TRANSACTION_Text + strShortName) ;

		var bDLL ;
		if ( typeDynamicLibrary == selProj.Object.Configurations (1).ConfigurationType )
			bDLL =true ;
		else
			bDLL =false ;
		wizard.AddSymbol ("DLL_APP", bDLL) ;

		var strArxClassName =wizard.FindSymbol ("ARX_CLASS_NAME") ;
		
		var strProjectName =wizard.FindSymbol ("PROJECT_NAME") ;
		var strProjectPath =wizard.FindSymbol ("PROJECT_PATH") ;
		var strTemplatePath =wizard.FindSymbol ("TEMPLATES_PATH") ;
		var strUpperShortName =strShortName.toUpperCase() ;
		var strInterfaceName =wizard.FindSymbol ("INTERFACE_NAME") ;
		wizard.AddSymbol ("UPPER_SHORT_NAME", strUpperShortName) ;
		var strVIProgID =wizard.FindSymbol ("VERSION_INDEPENDENT_PROGID") ;
		wizard.AddSymbol ("PROGID", strVIProgID.substr (0, 37) + ".1") ;
		var bConnectionPoint =wizard.FindSymbol ("CONNECTION_POINTS") ;
 		var strClassName =wizard.FindSymbol ("CLASS_NAME") ;
		var strHeaderFile =wizard.FindSymbol ("HEADER_FILE") ;
		var strImplFile =wizard.FindSymbol ("IMPL_FILE") ;
		var strCoClass =wizard.FindSymbol ("COCLASS") ;
		var bAttributed =wizard.FindSymbol ("ATTRIBUTED") ;

		var strProjectRC =GetProjectFile (selProj, "RC", true, false) ;

		//----- Create necessary GUIDS
		CreateGUIDs () ;
		if ( !bAttributed ) {
			//----- Get LibName
			wizard.AddSymbol ("LIB_NAME", oCM.IDLLibraries (1).Name) ;
			//----- Get LibID
			var oUuid =oCM.IDLLibraries (1).Attributes.Find ("uuid") ;
			if ( oUuid )
				wizard.AddSymbol ("LIBID_REGISTRY_FORMAT", oUuid.Value) ;
			//----- Get typelib version
			var oVersion =oCM.IDLLibraries (1).Attributes.Find ("version") ;
			if ( oVersion ) {
				var aryMajorMinor =oVersion.Value.split ('.') ;
				for ( var nCntr =0 ; nCntr < aryMajorMinor.length ; nCntr++ ) {
					if ( nCntr == 0 )
						wizard.AddSymbol ("TYPELIB_VERSION_MAJOR", aryMajorMinor [nCntr]) ;
					else
						wizard.AddSymbol ("TYPELIB_VERSION_MINOR", aryMajorMinor [nCntr]) ;
				}
			}
			//----- Get AppID
			var strAppID =wizard.GetAppID () ;
			if ( strAppID.length > 0 ) {
				wizard.AddSymbol ("APPID_EXIST", true) ;
				wizard.AddSymbol ("APPID_REGISTRY_FORMAT", strAppID) ;
			}
			//----- Add RGS file resource
			var strRGSFile =GetUniqueFileName (strProjectPath, strShortName + ".rgs") ;
			var strRGSID ="IDR_" + strUpperShortName ;

			RenderAddTemplate (wizard, "dynprop.rgs", strRGSFile, false, false) ;

			var oResHelper =wizard.ResourceHelper ;
			oResHelper.OpenResourceFile (strProjectRC) ;

			var strSymbolValue =oResHelper.AddResource (strRGSID, strProjectPath + strRGSFile, "REGISTRY") ;
			wizard.AddSymbol ("RGS_ID", strSymbolValue.split ("=").shift ()) ;

			oResHelper.CloseResourceFile () ;
			//----- Add connection point support
			if ( bConnectionPoint )
				RenderAddTemplate (wizard, "connpt.h", "_" + strInterfaceName + "Events_CP.h", selObj, false) ;
			//----- Render objco.idl and insert into strProject.idl
			AddCoclassFromFile (oCM, "dynpropco.idl") ;
			//----- Render objint.idl and insert into strProject.idl
			AddInterfaceFromFile (oCM, "dynpropint.idl") ;
			selProj.Object.Save () ;
			SetMergeProxySymbol (selProj) ;
		}
		//----- Add header
		RenderAddTemplate (wizard, "dynprop.h", strHeaderFile, selObj, true) ;
		//----- Add CPP
		RenderAddTemplate (wizard, "dynprop.cpp", strImplFile, selObj, false) ;
		oCM.CommitTransaction () ;
		oCM.Classes.Find (strClassName).StartPoint.TryToShow (vsPaneShowTop) ;
	} catch ( e ) {
		if ( oCM )
			oCM.AbortTransaction () ;

		if ( e.description.length != 0 )
			SetErrorInfo (e) ;
		return (e.number) ;
	}
}

function CreateGUIDs () {
	try {
		//----- create CLSID
		var strRawGUID =wizard.CreateGuid () ;
		var strFormattedGUID =wizard.FormatGuid (strRawGUID, 0) ;
		wizard.AddSymbol ("CLSID_REGISTRY_FORMAT", strFormattedGUID) ;
		//----- Create interface GUID
		strRawGUID =wizard.CreateGuid () ;
		strFormattedGUID =wizard.FormatGuid (strRawGUID, 0) ;
		wizard.AddSymbol ("INTERFACE_IID", strFormattedGUID) ;
		//----- Create connection point GUID
		var bConnectionPoint =wizard.FindSymbol ("CONNECTION_POINTS") ;
		if ( bConnectionPoint ) {
			strRawGUID =wizard.CreateGuid () ;
			strFormattedGUID =wizard.FormatGuid (strRawGUID, 0) ;
			wizard.AddSymbol ("CONNECTION_POINT_IID", strFormattedGUID) ;
		}
	} catch ( e ) {
		throw e ;
	}
}

//- ---------------------------------------------------------------------------------------------
//- The engine's script scope is not the HTML pages' scope: it preloads Visual Studio's own
//- Common.js / Script.js but it does NOT see ArxWizCommon\arxCommon.js, which the pages include.
//- Evaluate that file into this scope - for the diagnostics helpers above all. The eval call has to
//- sit at file scope like this: inside a function its definitions would die with the function.
//- ---------------------------------------------------------------------------------------------
try {
	var oArxSharedFso =new ActiveXObject ("Scripting.FileSystemObject") ;
	var strArxSharedPath =wizard.FindSymbol ("ABSOLUTE_PATH") ;
	strArxSharedPath =strArxSharedPath.substring (0, strArxSharedPath.lastIndexOf ("\\") + 1)
		+ "ArxWizCommon\\arxCommon.js" ;
	var oArxSharedStream =oArxSharedFso.OpenTextFile (strArxSharedPath, 1) ;
	var strArxShared =oArxSharedStream.ReadAll () ;
	oArxSharedStream.Close () ;
	eval (strArxShared) ;
	ArxWizLog ("ArxWizCommon\\arxCommon.js evaluated into the item wizard script scope: "
		+ ArxWizScope ()) ;
} catch (e) { }

//- ---------------------------------------------------------------------------------------------
//- OnFinish runs the original body wrapped, so a failure names itself in the log along with the
//- symbols the body reads back - an empty one is the usual reason for the bare "Object required".
//- ---------------------------------------------------------------------------------------------
var gArxItemOnFinishImpl =ArxItemOnFinishImpl ;
function OnFinish (selProj, selObj) {
	try {
		ArxWizLog ("OnFinish enter") ;
		var nArxItemResult =ArxItemOnFinishImpl (selProj, selObj) ;
		ArxWizLog ("OnFinish done, result=" + nArxItemResult) ;
		return (nArxItemResult) ;
	} catch (e) {
		ArxWizLog ("OnFinish FAILED: " + ArxWizErrorText (e)
			+ " | symbols CLASS_NAME_ROOT=[" + ArxWizSymbol ("CLASS_NAME_ROOT")
			+ "] CLASS_NAME=[" + ArxWizSymbol ("CLASS_NAME")
			+ "] HEADER_FILE=[" + ArxWizSymbol ("HEADER_FILE")
			+ "] IMPL_FILE=[" + ArxWizSymbol ("IMPL_FILE")
			+ "] TEMPLATE_HEADER=[" + ArxWizSymbol ("TEMPLATE_HEADER")
			+ "] TEMPLATE_IMPL=[" + ArxWizSymbol ("TEMPLATE_IMPL")
			+ "] INCLUDE_HEADER=[" + ArxWizSymbol ("INCLUDE_HEADER") + "]") ;
		throw e ;
	}
}