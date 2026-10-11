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
var ARX_WIZARD_NAME ="ArxWizJig" ;
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

		var strClassName =wizard.FindSymbol ("CLASS_NAME") ;		
		var L_TransactionName_Text ="Add an ObjectARX Jig Class " ;
		oCM.StartTransaction (L_TransactionName_Text + strClassName) ;

		var strProjectName =wizard.FindSymbol ("PROJECT_NAME") ;
		wizard.AddSymbol ("SAFE_PROJECT_NAME", CreateSafeName (strProjectName)) ;
		var szUprProjectName =CreateSafeName (strProjectName.toUpperCase ()) ;
		wizard.AddSymbol ("UPPER_CASE_SAFE_PROJECT_NAME", szUprProjectName) ;

		var nbSt =wizard.FindSymbol ("NUMBER_OF_INPUTS") ;
		var nb =parseInt (nbSt) ;
		if ( nb < 1 ) nb =1 ; //- Make sure we have at least one entry
		if ( nb > 20 ) nb =20 ; //- Let's keep something reasonnable
		
		var inputPrompts ='"\\nPick point"' ;
		var kwords ='""' ;
		var userInputControls ='/*AcEdJig::UserInputControls::*/(AcEdJig::UserInputControls)0' ;
		var cursorTypes ='/*AcEdJig::CursorType::*/(AcEdJig::CursorType)0' ;
		var samplerSwitch ='case 1:\n\t\t\t// TODO : get an input here\n\t\t\t//status =GetStartPoint () ;\n\t\t\tbreak ;\n' ;
		var updateSwitch ='case 1:\n\t\t\t// TODO : update your entity for this input\n\t\t\t//mpEntity->setCenter (mInputPoints [mCurrentInputLevel]) ;\n\t\t\tbreak ;\n' ;

		for ( var j =1 ; j < nb ; j++ ) {
			inputPrompts +=',\n\t\t"\\nPick point"' ;
			kwords +=',\n\t\t""' ;
			userInputControls +=',\n\t\t/*AcEdJig::UserInputControls::*/(AcEdJig::UserInputControls)0' ;
			cursorTypes +=',\n\t\t/*AcEdJig::CursorType::*/(AcEdJig::CursorType)0' ;
			samplerSwitch +='\t\tcase ' + (j + 1) + ':\n\t\t\t// TODO : get an input here\n\t\t\t//status =GetStartPoint () ;\n\t\t\tbreak ;\n' ;
			updateSwitch +='\t\tcase ' + (j + 1) + ':\n\t\t\t// TODO : update your entity for this input\n\t\t\t//mpEntity->setCenter (mInputPoints [mCurrentInputLevel]) ;\n\t\t\tbreak ;\n' ;
		}

		wizard.AddSymbol ("INPUT_PROMPTS", inputPrompts) ;
		wizard.AddSymbol ("INPUT_KEYWORDS", kwords) ;
		wizard.AddSymbol ("INPUT_USERCTRLS", userInputControls) ;
		wizard.AddSymbol ("INPUT_CURSORTYPES", cursorTypes) ;
		wizard.AddSymbol ("SAMPLER_SWITCH", samplerSwitch) ;
		wizard.AddSymbol ("UPDATE_SWITCH", updateSwitch) ;

		//- Add the normal header and cpp files that are needed
		var strHeaderFile =wizard.FindSymbol ("HEADER_FILE") ;
		var strImplFile =wizard.FindSymbol ("IMPL_FILE") ;
		ArxItemRender ("Jig.h", strHeaderFile, selObj, true) ;
		ArxItemRender ("Jig.cpp", strImplFile, selObj, false) ;
		
		oCM.CommitTransaction () ;				
		//oCM.Classes.Find (strClassName).StartPoint.TryToShow (vsPaneShowTop) ;

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
