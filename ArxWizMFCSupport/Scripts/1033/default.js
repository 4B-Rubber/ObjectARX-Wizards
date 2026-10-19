//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Fenton Webb / Cyrille Fauvel - Autodesk Developer Technical Services

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
var ARX_WIZARD_NAME ="ArxWizMFCSupport" ;
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
//- The host renderer (wizard.RenderTemplate) reports "rendered nothing" for these templates even
//- though the template file is on disk and the wizard symbols are set (Visual Studio 2022/2026),
//- so the templates are expanded here instead: the file is read with the FileSystemObject, the
//- four directives the templates use are evaluated against the wizard symbols, and the result is
//- written back. Nothing about the render then depends on the host.
//- The directives are [!if X] / [!else] / [!endif] / [!output X]; a condition is a bare symbol,
//- "!<symbol>", "<a> != <b>" or "<a> || <b>" (that is all the seven item template trees use).
function ArxItemReadAll (strPath) {
	var oFso =ArxItemFso () ;
	var oStream =oFso.OpenTextFile (strPath, 1, false, -2) ;
	var strText =oStream.ReadAll () ;
	oStream.Close () ;
	return (strText) ;
}

//- ANSI first, Unicode on failure: the classic item templates are ASCII today, but a localized
//- one would not fit ANSI and the framework's own writer retries in Unicode the same way.
function ArxItemWriteAll (strPath, strText) {
	var oFso =ArxItemFso () ;
	try {
		var oAnsi =oFso.CreateTextFile (strPath, true, false) ;
		oAnsi.Write (strText) ;
		oAnsi.Close () ;
		return ;
	} catch (e) {
		var oUni =oFso.CreateTextFile (strPath, true, -1) ;
		oUni.Write (strText) ;
		oUni.Close () ;
	}
}

//- A wizard symbol as a string; a missing one reads as "" (that is what [!output] wants).
function ArxItemSym (strName) {
	var v =null ;
	try { v =wizard.FindSymbol (strName) ; } catch (e) { v =null ; }
	if ( v == null || typeof (v) == "undefined" )
		return ("") ;
	return (ArxItemStr (v)) ;
}

//- The truth of a condition operand: a boolean symbol is used as it is, and a symbol that arrives
//- as a string counts as set while it is neither empty nor "false"/"0". An unknown symbol is false.
function ArxItemTruthy (strName) {
	var v =null ;
	try { v =wizard.FindSymbol (strName) ; } catch (e) { v =null ; }
	if ( v == null || typeof (v) == "undefined" )
		return (false) ;
	if ( typeof (v) == "boolean" )
		return (v) ;
	var strS =ArxItemStr (v) ;
	return (strS != "" && strS != "0" && strS.toLowerCase () != "false") ;
}

function ArxItemTrim (strS) {
	return (strS.replace (/^\s+|\s+$/g, "")) ;
}

//- One side of a "<a> != <b>" comparison. The templates write the bare class name on the right
//- (CLASS_NAME != CAdUiFileDialog), so a name that is not a wizard symbol is taken as that literal
//- text rather than read as an empty symbol.
function ArxItemOperand (strName) {
	var strTrimmed =ArxItemTrim (strName) ;
	var v =null ;
	try { v =wizard.FindSymbol (strTrimmed) ; } catch (e) { v =null ; }
	if ( v == null || typeof (v) == "undefined" )
		return (strTrimmed) ;
	return (ArxItemStr (v)) ;
}

function ArxItemEvalCond (strCond) {
	strCond =ArxItemTrim (strCond) ;
	if ( strCond.indexOf ("||") >= 0 ) {
		var rgOr =strCond.split ("||") ;
		for ( var nOr =0 ; nOr < rgOr.length ; nOr++ ) {
			if ( ArxItemEvalCond (rgOr[nOr]) )
				return (true) ;
		}
		return (false) ;
	}
	if ( strCond.indexOf ("!=") >= 0 ) {
		var rgNe =strCond.split ("!=") ;
		return (ArxItemOperand (rgNe[0]) != ArxItemOperand (rgNe[1])) ;
	}
	if ( strCond.charAt (0) == "!" )
		return (!ArxItemTruthy (ArxItemTrim (strCond.substring (1)))) ;
	return (ArxItemTruthy (strCond)) ;
}

//- Expands one template body. The directives are inline as well as line-based (object.h and the
//- .idl templates keep them inside a line), so the text is scanned token by token, not by line.
function ArxItemExpand (strText) {
	var rgFrames =[] ;
	var rgOut =[] ;
	var reTok =/\[!(if\s+[^\]]*|else|endif|output\s+[A-Za-z0-9_]+)\]/g ;
	var nLast =0 ;
	var m =null ;

	function ArxItemEmitting () {
		return (rgFrames.length == 0 ? true : rgFrames[rgFrames.length - 1].emitting) ;
	}

	while ( (m =reTok.exec (strText)) != null ) {
		if ( ArxItemEmitting () )
			rgOut.push (strText.substring (nLast, m.index)) ;
		var strBody =ArxItemTrim (m[1]) ;
		if ( strBody.indexOf ("if") == 0 && (strBody.length == 2 || /\s/.test (strBody.charAt (2))) ) {
			var bParent =ArxItemEmitting () ;
			var bCond =bParent && ArxItemEvalCond (strBody.substring (2)) ;
			rgFrames.push ({ parentEmitting: bParent, emitting: bCond, taken: bCond }) ;
		} else if ( strBody == "else" ) {
			if ( rgFrames.length > 0 ) {
				var oTop =rgFrames[rgFrames.length - 1] ;
				oTop.emitting =oTop.parentEmitting && !oTop.taken ;
				oTop.taken =true ;
			}
		} else if ( strBody == "endif" ) {
			if ( rgFrames.length > 0 )
				rgFrames.pop () ;
		} else if ( strBody.indexOf ("output") == 0 ) {
			if ( ArxItemEmitting () )
				rgOut.push (ArxItemSym (ArxItemTrim (strBody.substring (6)))) ;
		}
		nLast =reTok.lastIndex ;
	}
	if ( ArxItemEmitting () )
		rgOut.push (strText.substring (nLast)) ;
	return (rgOut.join ("")) ;
}

//- Renders one template into the target path (in script) and adds the file to the project.
//-
//- Two things are deliberate here, both from the failures reported 2026-10-11 (template on disk
//- and readable, target file created in the right folder, generated class still 0 bytes):
//-  * the target is an absolute path (ITEM_PATH / PROJECT_PATH + name), the spelling the item
//-    wizards shipping with Visual Studio use;
//-  * the rendering itself is done in script, so the host call that wrote nothing is out of the
//-    picture entirely.
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
	if ( oFso != null ) {
		try { oFso.DeleteFile (strFull) ; } catch (eDel) { }
	}
	ArxItemLog ("render (in script) \"" + strTemplateFull + "\" -> \"" + strFull + "\"") ;
	ArxItemWriteAll (strFull, ArxItemExpand (ArxItemReadAll (strTemplateFull))) ;
	var nSize =ArxItemSizeSettled (strFull) ;
	if ( oFso == null )
		nSize =1 ;
	if ( nSize <= 0 ) {
		try { oFso.DeleteFile (strFull) ; } catch (eDel2) { }
		throw new Error ("'" + strTemplateFull + "' rendered nothing into " + strFull) ;
	}
	ArxItemLog ("rendered '" + strTemplateFull + "' -> '" + strFull + "' (" + nSize + " bytes)") ;
	if ( oParent )
		AddFileToProject (strFull, oParent, bOpen) ;
}


function ArxItemOnFinishImpl (selProj, selObj) {
	var oCM ;
	try {
		oCM =selProj.CodeModel ;

		var strClassName =wizard.FindSymbol ("CLASS_NAME") ;

		var L_TransactionName_Text ="Add an ObjectARX MFC class " ;
		oCM.StartTransaction (L_TransactionName_Text + strClassName) ;

		var strHeaderFile		=wizard.FindSymbol ("HEADER_FILE") ;
		var strImplFile			=wizard.FindSymbol ("IMPL_FILE") ;
		var strDialogID			=wizard.FindSymbol ("IDD_DIALOG") ;
		var strTemplateHeader	=wizard.FindSymbol ("TEMPLATE_HEADER") ;
		var strTemplateImpl		=wizard.FindSymbol ("TEMPLATE_IMPL") ;
		var strBaseClass		=wizard.FindSymbol ("BASE_CLASS") ;

		// child dialog stuff
		var strChildDlgNeeded =wizard.FindSymbol ("CHILD_DIALOG_NEEDED") ;
		var strChildHeaderFile =wizard.FindSymbol ("CHILDHEADER_FILE") ;
		var strChildImplFile  =wizard.FindSymbol ("CHILDIMPL_FILE") ;
		// child resource
		var strChildResourceNeeded =wizard.FindSymbol ("CHILD_RESOURCE_NEEDED") ;

		//- The dialog resource itself was added by the wizard page while it was still up (see
		//- CreateDialogResource() in HTML\1033\default.htm): the host's resource service - which
		//- is what wizard.ResourceHelper.OpenResourceFile()/AddResource() reached into - compiles
		//- the project's .rc with the project's *active configuration*, and with an incomplete
		//- toolset that fails ("fatal error RC1015: cannot open include file 'winres.h'") and hands
		//- back nothing, which is where the bare "Object required" of this wizard came from.
		//- The child dialog files belong to the class, not to the resource, so they are rendered
		//- whenever the base class asked for a child dialog - the old code only did it when the
		//- dialog resource was created in the same run.
		ArxItemLog ("symbols: class=" + ArxItemStr (strClassName) + " base=" + ArxItemStr (strBaseClass)
			+ " idd=" + ArxItemStr (strDialogID) + " child_dialog=" + ArxItemStr (strChildDlgNeeded)
			+ " child_resource=" + ArxItemStr (strChildResourceNeeded)) ;
		if ( strChildDlgNeeded == true ) {
			ArxItemRender ("ChildDialog.h", strChildHeaderFile, selObj, true) ;
			ArxItemRender ("ChildDialog.cpp", strChildImplFile, selObj, false) ;
		}

		// add the normal header and cpp files that are needed
		ArxItemRender (strTemplateHeader, strHeaderFile, selObj, true) ;
		ArxItemRender (strTemplateImpl, strImplFile, selObj, false) ;

		oCM.CommitTransaction () ;
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
		var strArxItemText =ARX_WIZARD_NAME + " could not add the class: " + ArxItemErrorText (e) ;
		ArxItemLog ("OnFinish FAILED: " + strArxItemText) ;
		try { SetErrorInfo (new Error (strArxItemText)) ; } catch (e1) { }
		var nArxItemNumber =0 ;
		try { nArxItemNumber =parseInt (e.number, 10) ; } catch (e2) { }
		if ( nArxItemNumber == 0 || isNaN (nArxItemNumber) )
			nArxItemNumber =-1 ;
		return (nArxItemNumber) ;
	}
}
