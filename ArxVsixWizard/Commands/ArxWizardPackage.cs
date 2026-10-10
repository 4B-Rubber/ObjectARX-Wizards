// The package. This line ships the classic wizards: the project entries come from the installer's
// vcprojects\Autodesk (.vsz/.vsdir) and the item entries from vcprojectitems\ObjectARX, so the
// package carries no command of its own. It stays registered because the extension is what makes
// Visual Studio rebuild the wizard list those entries live in, and the log line below is the proof
// of life for that install (%TEMP%\ArxVsixWizard\wizard.log -> "PackageInit enter/done"). The
// auto-load context is kept for the same reason: with no command to click, the package would
// otherwise never load and the log would stay empty, which is exactly how the 0.2.0 command bug
// went unnoticed (measured on VS 18.10: zero "PackageInit" lines while the extension was installed
// and the IDE had restarted).
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ArxVsixWizard.Commands
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    // SolutionExists = F1536EF8-92EC-443C-9ED7-FDADF150DA82; background load keeps the IDE
    // startup cost off the UI thread. The package is trivial, so loading it per solution is fine.
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82", PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.PackageGuidString)]
    public sealed class ArxWizardPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // First proof of life in %TEMP%\ArxVsixWizard\wizard.log: an extension that never logs
            // this line is not being loaded at all.
            WizardDiagnostics.Log("PackageInit", "enter");
            try
            {
                await base.InitializeAsync(cancellationToken, progress);
                WizardDiagnostics.Log("PackageInit", "done");
            }
            catch (Exception ex)
            {
                WizardDiagnostics.Log("PackageInit", "failed: " + ex);
                throw;
            }
        }
    }
}
