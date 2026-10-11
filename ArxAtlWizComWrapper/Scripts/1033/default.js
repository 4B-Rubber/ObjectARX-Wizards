//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauvel - Autodesk Developer Technical Services

//- ---------------------------------------------------------------------------------------------
//- Diagnostics (classic line). The engine's script scope is not the HTML pages' scope (it preloads
//- Visual Studio's own framework script but it does not see ArxWizCommon\arxCommon.js), and a
//- failure in here only ever surfaces as a message box with a bare description - "Object required"
//- is what a null handed back by the host looks like, and the old catch blocks turned every error
//- whose object carried no description into exactly that, with the real reason lost. The helpers
//- this file uses are therefore defined right here: every run appends a line to
//- %TEMP%\arxwiz-classic.log (the file the pages and the project wizards append to as well), and
//- every failure is reported as a sentence naming the step.
//- ---------------------------------------------------------------------------------------------
var ARX_WIZARD_NAME ="ArxAtlWizComWrapper" ;
var ARX_LOG_FILE ="arxwiz-classic.log" ;

function ArxItemFso () {
	return (new ActiveXObject ("Scripting.FileSystemObject")) ;
}

function ArxItemFsoOrNull () {
	try { return (ArxItemFso ()) ; } catch (e) { return (null) ; }
}

function ArxItemLog (strMsg) {
	try {
		var oFso =ArxItemFso () ;
		var oStream =oFso.OpenTextFile (oFso.GetSpecialFolder (2) + "\\" + ARX_LOG_FILE, 8, true) ;
		oStream.WriteLine (new Date ().toString () + " [" + ARX_WIZARD_NAME + "] " + strMsg) ;
		oStream.Close () ;
	} catch (e) { }
}

function ArxItemStr (v) {
	if ( v == null || typeof (v) == "undefined" )
		return ("") ;
	return ("" + v) ;
}

function ArxItemSymbol (strName) {
	try { return (ArxItemStr (wizard.FindSymbol (strName))) ; } catch (e) { return ("<error>") ; }
}

//- Everything the error object has, plus a hint for the one error that says nothing at all: a
//- property read from null or undefined (0x800A138F) means a symbol or project object was empty.
function ArxItemErrorText (e) {
	var strText ="" ;
	var nNumber =0 ;
	try { nNumber =e.number ; } catch (e0) { }
	try { strText =ArxItemStr (e.description) ; } catch (e1) { }
	if ( strText == "" ) { try { strText =ArxItemStr (e.message) ; } catch (e2) { } }
	if ( nNumber == -2146823281 )
		strText +=" (0x800A138F: a symbol or project object this wizard read back was null)" ;
	if ( strText == "" )
		strText ="script error 0x" + (nNumber < 0 ? (0x100000000 + nNumber) : nNumber).toString (16) ;
	return (strText) ;
}

//- The templates this wizard renders live in <wizard>\Templates\<locale>. TEMPLATES_PATH is the
//- symbol the framework's RenderAddTemplate() reads; it is not always set for an item wizard (or
//- not set to a folder this wizard has), and rendering from a path that does not exist produces an
//- empty file rather than an error - reported 2026-10-11: demo.h/demo.cpp, 0 bytes. Fall back to
//- the folder next to this script and hand that back to the framework.
function ArxItemTemplatesDir () {
	var strSymbol =ArxItemSymbol ("TEMPLATES_PATH") ;
	var oFso =ArxItemFsoOrNull () ;
	if ( oFso == null )
		return (strSymbol) ;
	if ( strSymbol != "" && oFso.FolderExists (strSymbol) )
		return (strSymbol) ;
	var strRoot =ArxItemSymbol ("ABSOLUTE_PATH") ;
	if ( strRoot != "" ) {
		if ( oFso.FolderExists (strRoot + "\\Templates\\1033") )
			return (strRoot + "\\Templates\\1033") ;
		try {
			var oEnum =new Enumerator (oFso.GetFolder (strRoot + "\\Templates").SubFolders) ;
			for ( ; !oEnum.atEnd () ; oEnum.moveNext () )
				return (String (oEnum.item ())) ;
		} catch (e) { }
	}
	return (strSymbol) ;
}

//- RenderAddTemplate() with the template folder checked first and the result measured afterwards:
//- a template that is not where the host looks and a template that renders nothing are both silent
//- otherwise, and both end up as an empty class in the project.
function ArxItemRender (strTemplate, strTarget, oParent, bOpen) {
	var strWas =ArxItemSymbol ("TEMPLATES_PATH") ;
	var strDir =ArxItemTemplatesDir () ;
	if ( strDir != "" && strDir != strWas ) {
		wizard.AddSymbol ("TEMPLATES_PATH", strDir) ;
		ArxItemLog ("TEMPLATES_PATH was '" + strWas + "', using '" + strDir + "'") ;
	}
	var oFso =ArxItemFsoOrNull () ;
	if ( oFso != null && strDir != "" && !oFso.FileExists (strDir + "\\" + strTemplate) )
		throw new Error ("template not found: " + strDir + "\\" + strTemplate) ;
	RenderAddTemplate (wizard, strTemplate, strTarget, oParent, bOpen) ;
	if ( oFso == null )
		return ;
	var strFull =strTarget ;
	if ( strFull.charAt (1) != ":" )
		strFull =ArxItemSymbol ("PROJECT_PATH") + strFull ;
	if ( !oFso.FileExists (strFull) ) {
		ArxItemLog ("rendered '" + strTemplate + "', but " + strFull + " is not on disk") ;
		return ;
	}
	var nSize =oFso.GetFile (strFull).Size ;
	ArxItemLog ("rendered '" + strTemplate + "' -> '" + strFull + "' (" + nSize + " bytes)") ;
	if ( nSize == 0 ) {
		try { oFso.DeleteFile (strFull) ; } catch (e) { }
		throw new Error ("template '" + strTemplate + "' in '" + strDir + "' rendered an empty file") ;
	}
}
function ArxItemOnFinishImpl (selProj, selObj) {
	var oCM ;
	try {
		oCM	=selProj.CodeModel ;

		var strShortName =wizard.FindSymbol ("SHORT_NAME") ;
		var L_TRANSACTION_Text ="Add an AutoCAD COM wrapper object. " ;
		oCM.StartTransaction (L_TRANSACTION_Text + strShortName) ;

		var bDLL ;
		if ( typeDynamicLibrary == selProj.Object.Configurations (1).ConfigurationType )
			bDLL =true ;
		else
			bDLL =false ;
		wizard.AddSymbol ("DLL_APP", bDLL) ;

		var strProjectName =wizard.FindSymbol ("PROJECT_NAME") ;
		var strProjectPath =wizard.FindSymbol ("PROJECT_PATH") ;
		var strTemplatePath =wizard.FindSymbol ("TEMPLATES_PATH") ;
		var strUpperShortName =strShortName.toUpperCase () ;
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
			//- The .rc line and the #define for this .rgs are written by the wizard page while it
			//- is still up (OnFinish in HTML\1033\default.htm -> ArxResAddRegistry in
			//- ArxWizCommon\arxCommon.js): wizard.ResourceHelper.AddResource() went through the
			//- host's resource service, which hands back nothing when the project's .rc cannot be
			//- built with the project's active configuration, and this script then dereferenced
			//- that empty result - the bare "Object required" this wizard used to end on. The file
			//- name comes from the page (ARX_RGS_FILE) so both sides write the same one.
			var strRGSFile =ArxItemSymbol ("ARX_RGS_FILE") ;
			if ( strRGSFile == "" )
				strRGSFile =GetUniqueFileName (strProjectPath, strShortName + ".rgs") ;
			var strRGSID ="IDR_" + strUpperShortName ;
			ArxItemRender ("object.rgs", strRGSFile, false, false) ;
			
			wizard.AddSymbol ("RGS_ID", strRGSID) ;
			//----- Add connection point support
			if ( bConnectionPoint )
				ArxItemRender ("connpt.h", "_" + strInterfaceName + "Events_CP.h", selObj, false) ;
			//------ Render objco.idl and insert into strProject.idl
			AddCoclassFromFile (oCM, "objco.idl") ;
			//----- Render objint.idl and insert into strProject.idl
			//AddInterfaceFromFile (oCM, "objint.idl") ;
			selProj.Object.Save () ;
			SetMergeProxySymbol (selProj) ;
		}
		//------ Add header
		ArxItemRender ("object.h", strHeaderFile, selObj, true) ;
		//------ Add CPP
		ArxItemRender ("object.cpp", strImplFile, selObj, false) ;
		oCM.CommitTransaction () ;
		var oArxItemClass =oCM.Classes.Find (strClassName) ;
		if ( oArxItemClass != null )
			oArxItemClass.StartPoint.TryToShow (vsPaneShowTop) ;
		else
			ArxItemLog ("class '" + strClassName + "' is not in the code model; nothing to select") ;
	} catch ( e ) {
		if ( oCM )
			oCM.AbortTransaction () ;
		ArxItemLog ("step failed: " + ArxItemErrorText (e)) ;
		throw e ;
	}
}

function CreateGUIDs () {
	try {
		//----- Create CLSID
		var strRawGUID =wizard.CreateGuid () ;
		var strFormattedGUID =wizard.FormatGuid (strRawGUID, 0) ;
		wizard.AddSymbol ("CLSID_REGISTRY_FORMAT", strFormattedGUID) ;
		//----- Create interface GUID
		strRawGUID =wizard.CreateGuid () ;
		strFormattedGUID =wizard.FormatGuid (strRawGUID, 0) ;
		wizard.AddSymbol ("INTERFACE_IID", strFormattedGUID) ;
		//------ Create connection point GUID
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
//- OnFinish runs the original body wrapped, so a failure names itself in the log and in the error
//- box the host shows (the message is set here, not left to the raw error object, whose missing
//- description is what used to end up on screen as a bare "Object required").
//- ---------------------------------------------------------------------------------------------
var gArxItemOnFinishImpl =ArxItemOnFinishImpl ;
function OnFinish (selProj, selObj) {
	try {
		ArxItemLog ("OnFinish enter") ;
		var nArxItemResult =gArxItemOnFinishImpl (selProj, selObj) ;
		ArxItemLog ("OnFinish done, result=" + nArxItemResult) ;
		return (nArxItemResult) ;
	} catch (e) {
		var strArxItemText =ARX_WIZARD_NAME + " could not add the item: " + ArxItemErrorText (e) ;
		ArxItemLog ("OnFinish FAILED: " + strArxItemText) ;
		try { SetErrorInfo (new Error (strArxItemText)) ; } catch (e1) { }
		var nArxItemNumber =0 ;
		try { nArxItemNumber =parseInt (e.number, 10) ; } catch (e2) { }
		if ( nArxItemNumber == 0 || isNaN (nArxItemNumber) )
			nArxItemNumber =-1 ;
		return (nArxItemNumber) ;
	}
}
