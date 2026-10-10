// Launches the MSI with its own UI. The bundle chains this launcher instead of chaining the MSI
// as an MsiPackage, because Burn skips a package whose ProductCode is already present: once the
// product was installed, re-running Setup.exe did nothing at all - no repair, and no way to pick
// a different set of target years. msiexec decides for itself what to show: the year wizard on a
// first install, the maintenance page (repair / change years / remove) afterwards.
//
// It also refuses to start while Visual Studio is running. The bundle installs the VSIX right
// after this step, and VSIXInstaller aborts when devenv/DevHub hold the extension files, which
// would leave the install half done. Stopping before msiexec touches anything keeps that atomic;
// ArxSkipVsCheck=1 (the bundle forwards it as --skipvscheck=1) lifts the check for unattended runs.
//
// Optional switches, all forwarded by the bundle (see Bundle.wxs). Empty values are ignored, so
// the bundle can pass them unconditionally:
//   --quiet=1        hand msiexec /qn - no MSI UI, the built-in default years are used
//   --skipvscheck=1  do not look for a running Visual Studio
//   --rds=SYM        registered developer symbol    -> MSI property RDS
//   --arxroot=DIR    Autodesk root folder           -> MSI property ARXROOT
//   --arxsdk=DIR     ObjectARX SDK folder           -> MSI property ARXPATH
//   --propsdir=DIR   property sheet folder          -> MSI property ARXPROPSDIR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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
        if (string.IsNullOrEmpty(msi) || (msi[0] != '/' && !File.Exists(msi)))
        {
            MessageBox(IntPtr.Zero, "The installer payload was not found:\r\n" + (msi ?? "(no argument)"),
                       "ObjectARX Multi-Version Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
            return ErrorInstallFailure;
        }

        bool quiet = IsSet(args, "--quiet");
        string properties = BuildPropertyArguments(args);

        // /uninstall <ProductCode> is what the bundle uses to take the MSI away again. No
        // Visual Studio check there - removing the product has nothing to do with the extension.
        if (string.Equals(msi, "/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            string productCode = args.Length > 1 ? args[1] : null;
            string ui = quiet ? " /qn" : " /qb";
            return RunMsiexec(string.IsNullOrEmpty(productCode) ? null : "/x " + productCode + ui + LogArguments());
        }

        if (!IsSet(args, "--skipvscheck"))
        {
            string blocker = FindBlockingProcess();
            if (blocker != null)
            {
                MessageBox(IntPtr.Zero,
                           "Please close Visual Studio before installing.\r\n\r\n" +
                           "Running now: " + blocker + "\r\n\r\n" +
                           "The installer adds the ObjectARX wizards to Visual Studio, and that step " +
                           "cannot run while Visual Studio is open. Nothing has been installed yet - " +
                           "close Visual Studio and start the setup again.\r\n\r\n" +
                           "Unattended installs can pass ArxSkipVsCheck=1 to the setup.",
                           "ObjectARX Multi-Version Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
                return ErrorUserExit;
            }
        }

        Console.WriteLine("[arx] quiet=" + quiet + " properties=" + (properties.Length == 0 ? "(none)" : properties));
        return RunMsiexec("/i \"" + msi + "\"" + (quiet ? " /qn" : "") + properties + LogArguments());
    }

    /// <summary>True when the switch is present with 1/true (empty counts as not set).</summary>
    static bool IsSet(string[] args, string name)
    {
        string value = Option(args, name);
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The value of --name=value, or null. Surrounding quotes and spaces are trimmed.</summary>
    static string Option(string[] args, string name)
    {
        string prefix = name + "=";
        foreach (string arg in args)
        {
            if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return arg.Substring(prefix.Length).Trim().Trim('"');
        }
        return null;
    }

    /// <summary>The msiexec property list for the values the bundle passed through.</summary>
    static string BuildPropertyArguments(string[] args)
    {
        var sb = new StringBuilder();
        AppendProperty(sb, "RDS", Option(args, "--rds"));
        AppendProperty(sb, "ARXROOT", Option(args, "--arxroot"));
        AppendProperty(sb, "ARXPATH", Option(args, "--arxsdk"));
        AppendProperty(sb, "ARXPROPSDIR", Option(args, "--propsdir"));
        return sb.ToString();
    }

    static void AppendProperty(StringBuilder sb, string name, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append(' ').Append(name).Append("=\"").Append(value.TrimEnd('\\', '"')).Append('"');
    }

    /// <summary>
    /// msiexec writes a verbose log next to the bundle's own log. Without it a failed install is
    /// just an exit code - why msiexec refused (version conflict, policy block, a failed condition)
    /// only ever shows up here.
    /// </summary>
    static string LogArguments()
        => " /l*v \"" + Path.Combine(Path.GetTempPath(), "ObjectARXWizards-msi.log") + "\"";

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
