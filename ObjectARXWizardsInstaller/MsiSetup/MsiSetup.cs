// Launches the MSI with its own UI. The bundle chains this launcher instead of chaining the MSI
// as an MsiPackage, because Burn skips a package whose ProductCode is already present: once the
// product was installed, re-running Setup.exe did nothing at all - no repair, and no way to pick
// a different set of target years. msiexec decides for itself what to show: the year wizard on a
// first install, the maintenance page (repair / change years / remove) afterwards.
//
// It also refuses to start while Visual Studio is running. The bundle installs the VSIX right
// after this step, and VSIXInstaller aborts when devenv/DevHub hold the extension files, which
// would leave the install half done. Stopping before msiexec touches anything keeps that atomic.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

static class MsiSetup
{
    // VSIXInstaller's own blocker list, minus the ones that never hold the extension.
    static readonly string[] BlockingProcesses = { "devenv", "DevHub" };

    const uint MB_OK = 0x0;
    const uint MB_ICONWARNING = 0x30;
    const uint MB_SETFOREGROUND = 0x10000;

    // Same codes msiexec itself uses, so the bundle reports something meaningful.
    const int ErrorInstallFailure = 1603;
    const int ErrorUserExit = 1602;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    static int Main(string[] args)
    {
        string msi = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrEmpty(msi) || !File.Exists(msi))
        {
            MessageBox(IntPtr.Zero, "The installer payload was not found:\r\n" + (msi ?? "(no argument)"),
                       "ObjectARX Multi-Year Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
            return ErrorInstallFailure;
        }

        // /uninstall <ProductCode> is what the bundle uses to take the MSI away again. No
        // Visual Studio check there - removing the product has nothing to do with the extension.
        if (string.Equals(msi, "/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            string productCode = args.Length > 1 ? args[1] : null;
            return RunMsiexec(string.IsNullOrEmpty(productCode) ? null : "/x " + productCode + " /qb");
        }

        string blocker = FindBlockingProcess();
        if (blocker != null)
        {
            MessageBox(IntPtr.Zero,
                       "Please close Visual Studio before installing.\r\n\r\n" +
                       "Running now: " + blocker + "\r\n\r\n" +
                       "The installer adds the ObjectARX wizards to Visual Studio, and that step " +
                       "cannot run while Visual Studio is open. Nothing has been installed yet - " +
                       "close Visual Studio and start the setup again.",
                       "ObjectARX Multi-Year Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
            return ErrorUserExit;
        }

        return RunMsiexec("/i \"" + msi + "\"");
    }

    static int RunMsiexec(string arguments)
    {
        if (arguments == null) return ErrorInstallFailure;

        string msiexec = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
        Console.WriteLine("[arx] msiexec " + arguments);
        var psi = new ProcessStartInfo(msiexec, arguments) { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            Console.WriteLine("[arx] msiexec exited with " + p.ExitCode);
            return p.ExitCode;
        }
    }

    /// <summary>Returns the display name of the first blocking process that is running, else null.</summary>
    static string FindBlockingProcess()
    {
        foreach (string name in BlockingProcesses)
        {
            Process[] found;
            try { found = Process.GetProcessesByName(name); }
            catch { continue; }
            foreach (var p in found)
            {
                try
                {
                    string display = p.MainWindowTitle;
                    return string.IsNullOrEmpty(display) ? p.ProcessName + ".exe" : p.ProcessName + ".exe (" + display + ")";
                }
                catch { return p.ProcessName + ".exe"; }
                finally { p.Dispose(); }
            }
        }
        return null;
    }
}