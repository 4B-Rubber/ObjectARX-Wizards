// The command is declared DefaultInvisible with DynamicVisibility so it can be limited to
// ObjectARX projects by BeforeQueryStatus. That needs the package to be *loaded*: an invisible
// command cannot be clicked, and a package is only loaded when one of its commands runs, so
// without an auto-load context the package would never load and the command would never appear
// (measured on VS 18.10: zero "PackageInit" lines in %TEMP%\ArxVsixWizard\wizard.log while the
// extension was installed and the IDE had restarted). Hence the auto-load below.
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
            // The previous attempt at this command failed silently because the package never
            // loaded; this line is the first proof of life in %TEMP%\ArxVsixWizard\wizard.log.
            WizardDiagnostics.Log("PackageInit", "enter");
            try
            {
                await base.InitializeAsync(cancellationToken, progress);
                await AddArxItemCommand.InitializeAsync(this);
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
