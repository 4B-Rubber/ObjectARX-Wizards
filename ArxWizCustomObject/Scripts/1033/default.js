//- Copyright (c) Autodesk, Inc. All rights reserved.
//- by Cyrille Fauvel - Autodesk Developer Technical Services

//- ---------------------------------------------------------------------------------------------
//- Diagnostics (classic line). The engine's script scope is not the HTML pages' scope: it preloads
//- Visual Studio's own framework script but it does not see ArxWizCommon\arxCommon.js, and a failure
//- in here only ever surfaces as a message box with a bare description ("Object required" and the
//- like). arxCommon.js is evaluated into this scope at file level (bottom of this file) and every
//- run appends to %TEMP%\arxwiz-classic.log - the same file the pages and the project wizards write.
//- ---------------------------------------------------------------------------------------------
var ARX_WIZARD_NAME ="ArxWizCustomObject" ;
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
		
		RenderAddTemplate (wizard, strTemplateHeader, strHeaderFile, selObj, true) ;
		RenderAddTemplate (wizard, strTemplateImpl, strImplFile, selObj, false) ;

		oCM.CommitTransaction () ;
				
		ShowTextPoint (oCM.Classes.Find (strClassName).StartPoint) ;
	} catch ( e ) {
		if ( oCM )
			oCM.AbortTransaction () ;

		if ( e.description.length != 0 )
			SetErrorInfo (e) ;
		return (e.number) ;
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