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
var ARX_WIZARD_NAME ="ArxWizCustomObject" ;
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
//- Where an item's files go. ITEM_PATH is what the item wizards that ship with Visual Studio build
//- their target file name from; PROJECT_PATH is the project folder, which is what the classic ATL
//- wizards used and what the reported failures showed a bare name being resolved against. Both may
//- come with or without a trailing separator.
function ArxItemProjectDir (oParent) {
	var strPath =ArxItemSymbol ("ITEM_PATH") ;
	if ( strPath == "" )
		strPath =ArxItemSymbol ("PROJECT_PATH") ;
	while ( strPath.length > 0 && (strPath.charAt (strPath.length - 1) == "\\" || strPath.charAt (strPath.length - 1) == "/") )
		strPath =strPath.substring (0, strPath.length - 1) ;
	if ( strPath == "" && oParent ) {
		try {
			var strProject =ArxItemStr (oParent.ContainingProject.FullName) ;
			var nSlash =strProject.lastIndexOf ("\\") ;
			if ( nSlash > 0 )
				strPath =strProject.substring (0, nSlash) ;
		} catch (e) { }
	}
	return (strPath) ;
}

//- Size of the rendered file, or -1 when it is not on disk / not readable.
function ArxItemFileSize (strPath) {
	var oFso =ArxItemFsoOrNull () ;
	if ( oFso == null )
		return (-1) ;
	try {
		if ( !oFso.FileExists (strPath) )
			return (-1) ;
		return (oFso.GetFile (strPath).Size) ;
	} catch (e) {
		return (-1) ;
	}
}

//- The host's write can land a moment after RenderTemplate returns; wait briefly before calling an
//- attempt a failure. Reading 0 too early is what made the retry append a second copy of the
//- template on 2026-10-11 (dbdemo.h came out with two copyright headers).
function ArxItemSizeSettled (strPath) {
	var nSize =ArxItemFileSize (strPath) ;
	if ( nSize > 0 )
		return (nSize) ;
	var oStart =(new Date ()).getTime () ;
	while ( ((new Date ()).getTime () - oStart) < 1000 ) {
		nSize =ArxItemFileSize (strPath) ;
		if ( nSize > 0 )
			return (nSize) ;
	}
	return (nSize) ;
}

//- The one render call, in the two argument spelling the DEFFile, PropertySheet and RCFile item
//- wizards shipping with Visual Studio use. There is no retry spelling, and that is the fix for
//- the files that came out with the template twice: the (false, true) spelling of ResourceScript
//- and UnitTest does not write at once - it landed 18 seconds after the wizard returned on
//- 2026-10-12, when the project was saved - and that late write appends, so the retry that had
//- already written the file produced a second copy. The target is removed first so the call
//- always starts from nothing.
function ArxItemRenderWith (strTemplateFull, strFull) {
	ArxItemLog ("wizard.RenderTemplate (\"" + strTemplateFull + "\", \"" + strFull + "\")") ;
	var oFso =ArxItemFsoOrNull () ;
	if ( oFso != null ) {
		try { oFso.DeleteFile (strFull) ; } catch (e) { }
	}
	wizard.RenderTemplate (strTemplateFull, strFull) ;
	return (ArxItemSizeSettled (strFull) > 0) ;
}

//- Renders one template into the project folder and adds the file to the project.
//-
//- Two things are deliberate here, both from the failures reported 2026-10-11 (template on disk and
//- readable, target file created in the right folder, generated class still 0 bytes):
//-  * the host call is the bare two argument form, the one the DEFFile, PropertySheet and RCFile
//-    item wizards shipping with Visual Studio use; see ArxItemRenderWith for why there is no
//-    retry spelling behind it.
//-  * the target is an absolute path (ITEM_PATH / PROJECT_PATH + name), the spelling those item
//-    wizards use as well.
//- A target that stays empty is deleted and reported, never left in the project as an empty class.
function ArxItemRender (strTemplate, strTarget, oParent, bOpen) {
	var oFso =ArxItemFsoOrNull () ;
	var strWas =ArxItemSymbol ("TEMPLATES_PATH") ;
	var strDir =ArxItemTemplatesDir () ;
	if ( strDir != "" && strDir != strWas ) {
		wizard.AddSymbol ("TEMPLATES_PATH", strDir) ;
		ArxItemLog ("TEMPLATES_PATH was '" + strWas + "', using '" + strDir + "'") ;
	}
	var strTemplateFull =strDir + "\\" + strTemplate ;
	if ( oFso != null && strDir != "" && !oFso.FileExists (strTemplateFull) )
		throw new Error ("template not found: " + strTemplateFull) ;
	var strFull =strTarget ;
	if ( strFull.charAt (1) != ":" ) {
		var strTargetDir =ArxItemProjectDir (oParent) ;
		if ( strTargetDir != "" )
			strFull =strTargetDir + "\\" + strFull ;
	}
	ArxItemRenderWith (strTemplateFull, strFull) ;
	var nSize =ArxItemSizeSettled (strFull) ;
	if ( oFso == null )
		nSize =1 ;
	if ( nSize == 0 || nSize == -1 ) {
		try { oFso.DeleteFile (strFull) ; } catch (e) { }
		throw new Error ("'" + strTemplateFull + "' rendered nothing into " + strFull) ;
	}
	ArxItemLog ("rendered '" + strTemplateFull + "' -> '" + strFull + "' (" + nSize + " bytes)") ;
	if ( oParent )
		AddFileToProject (strFull, oParent, bOpen) ;
}

function ArxItemOnFinishImpl (selProj, selObj) {
	var oCM ;
	try {
		oCM	=selProj.CodeModel ;

		var strClassName =wizard.FindSymbol ("CLASS_NAME") ;

		var L_TransactionName_Text ="Adds a class that inherits from ObjectDBX to your project " ;
		oCM.StartTransaction (L_TransactionName_Text + strClassName) ;

		var strProjectPath =wizard.FindSymbol ("PROJECT_PATH") ;
		var strTemplatePath =wizard.FindSymbol ("TEMPLATES_PATH") ;
		var strIncludeHeader =wizard.FindSymbol ("INCLUDE_HEADER") ;
		var strHeaderFile =wizard.FindSymbol ("HEADER_FILE") ;
		var strImplFile =wizard.FindSymbol ("IMPL_FILE") ;
		var strTemplateHeader =wizard.FindSymbol ("TEMPLATE_HEADER") ;
		var strTemplateImpl =wizard.FindSymbol ("TEMPLATE_IMPL") ;
		var strBaseClass =wizard.FindSymbol ("BASE_CLASS") ;
		var strProjectCPP =GetProjectFile (selProj, "CPP", false, true) ;
		
		var strProjectName =wizard.FindSymbol ("PROJECT_NAME") ;
		wizard.AddSymbol ("SAFE_PROJECT_NAME", CreateSafeName (strProjectName)) ;
		var szUprProjectName =CreateSafeName (strProjectName.toUpperCase ()) ;
		wizard.AddSymbol ("UPPER_CASE_SAFE_PROJECT_NAME", szUprProjectName) ;

		var protocols =wizard.FindSymbol ("PROTOCOLS") ;
		switch ( parseInt (protocols) ) {
			case 1:
				wizard.AddSymbol ("ACDBOBJECT_PROTOCOLS", true) ;
				wizard.AddSymbol ("ACDBENTITY_PROTOCOLS", false) ;
				wizard.AddSymbol ("ACDBCURVE_PROTOCOLS", false) ;
				break ;
			case 2:
				wizard.AddSymbol ("ACDBOBJECT_PROTOCOLS", true) ;
				wizard.AddSymbol ("ACDBENTITY_PROTOCOLS", true) ;
				wizard.AddSymbol ("ACDBCURVE_PROTOCOLS", false) ;
				break ;
			case 3:
				wizard.AddSymbol ("ACDBOBJECT_PROTOCOLS", true) ;
				wizard.AddSymbol ("ACDBENTITY_PROTOCOLS", true) ;
				wizard.AddSymbol ("ACDBCURVE_PROTOCOLS", true) ;
				break ;
			default:
				wizard.AddSymbol ("ACDBOBJECT_PROTOCOLS", false) ;
				wizard.AddSymbol ("ACDBENTITY_PROTOCOLS", false) ;
				wizard.AddSymbol ("ACDBCURVE_PROTOCOLS", false) ;
				break ;
		}
		
		ArxItemRender (strTemplateHeader, strHeaderFile, selObj, true) ;
		ArxItemRender (strTemplateImpl, strImplFile, selObj, false) ;

		oCM.CommitTransaction () ;
				
		var oArxItemClass =oCM.Classes.Find (strClassName) ;
		if ( oArxItemClass != null )
			ShowTextPoint (oArxItemClass.StartPoint) ;
		else
			ArxItemLog ("class '" + strClassName + "' is not in the code model; nothing to select") ;
	} catch ( e ) {
		if ( oCM )
			oCM.AbortTransaction () ;

		ArxItemLog ("step failed: " + ArxItemErrorText (e)) ;
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
