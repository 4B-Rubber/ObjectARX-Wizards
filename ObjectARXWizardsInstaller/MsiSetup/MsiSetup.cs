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
//   --years=CSV      target years, e.g. 2020,2024   -> MSI properties YEAR_<year>
// Installing through this launcher also hides the MSI from Settings &gt; Apps (ARPSYSTEMCOMPONENT=1):
// the bundle is the entry the user should see, and a second identical one next to it is just noise.
// The switches mirror the Inno line's /RDS=, /ARXROOT=, /ARXSDKPATH=, /PROPSDIR=, /YEARS=,
// /SKIPVSIX=1 (bundle level) and /SKIPVSCHECK=1 one for one.
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
    const int ErrorUnknownProduct = 1605;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    static extern uint MsiOpenPackage(string packagePath, out IntPtr product);

    [DllImport("msi.dll", CharSet = CharSet.Unicode)]
    static extern uint MsiGetProductProperty(IntPtr product, string property, StringBuilder value, ref int length);

    [DllImport("msi.dll")]
    static extern uint MsiCloseHandle(IntPtr handle);

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

        // /uninstall [ProductCode] is what the bundle uses to take the MSI away again. No Visual
        // Studio check there - removing the product has nothing to do with the extension.
        //
        // The code is optional on purpose. The bundle used to repeat ObjectARXWizards.wxs's define,
        // and it went stale the moment the version moved: 0.2.5's bundle still asked msiexec to
        // remove 0.2.4's ProductCode. Without it the code is read from the MSI that sits next to
        // this launcher, which is the very build the bundle installed. 1605 (no such product) is
        // success: that is what an uninstall looks like once a newer bundle took the product away.
        if (string.Equals(msi, "/uninstall", StringComparison.OrdinalIgnoreCase))
        {
            string productCode = args.Length > 1 && args[1].StartsWith("{", StringComparison.Ordinal)
                ? args[1]
                : ProductCodeOf(FindBundledMsi());
            if (string.IsNullOrEmpty(productCode))
            {
                Console.WriteLine("[arx] /uninstall: no product code and no MSI next to the launcher");
                return ErrorInstallFailure;
            }
            string ui = quiet ? " /qn" : " /qb";
            Console.WriteLine("[arx] /uninstall " + productCode);
            int code = RunMsiexec("/x " + productCode + ui + LogArguments());
            return code == ErrorUnknownProduct ? 0 : code;
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

    /// <summary>The MSI that ships next to this launcher, or null when it is not there.</summary>
    static string FindBundledMsi()
    {
        try
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(folder)) return null;
            string candidate = Path.Combine(folder, "ObjectARXWizard.msi");
            return File.Exists(candidate) ? candidate : null;
        }
        catch { return null; }
    }

    /// <summary>ProductCode of an MSI, read through the Windows Installer API.</summary>
    static string ProductCodeOf(string packagePath)
    {
        if (string.IsNullOrEmpty(packagePath)) return null;
        IntPtr product;
        if (MsiOpenPackage(packagePath, out product) != 0) return null;
        try
        {
            int length = 64;
            var value = new StringBuilder(length);
            if (MsiGetProductProperty(product, "ProductCode", value, ref length) != 0) return null;
            return value.ToString();
        }
        catch { return null; }
        finally { MsiCloseHandle(product); }
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
        // The bundle is the entry the user sees in Settings > Apps: this launcher is only ever run by
        // it, and installing the MSI through it without this would leave a second, identically named
        // entry next to the bundle's own. ARPSYSTEMCOMPONENT hides the product from that list (and
        // from Programs and Features) while leaving everything else - detection, repair, /x by code -
        // alone, and the bundle's own uninstall still takes the product away. A standalone run of
        // ObjectARXWizard.msi does not go through this launcher, so that route keeps its entry.
        sb.Append(" ARPSYSTEMCOMPONENT=1");
        AppendProperty(sb, "RDS", Option(args, "--rds"));
        AppendProperty(sb, "ARXROOT", Option(args, "--arxroot"));
        AppendProperty(sb, "ARXPATH", Option(args, "--arxsdk"));
        AppendProperty(sb, "ARXPROPSDIR", Option(args, "--propsdir"));
        AppendYears(sb, Option(args, "--years"));
        return sb.ToString();
    }

    /// <summary>
    /// --years=2020,2024,2026,2027 starts the wizard with exactly those years ticked, the way the Inno
    /// line's /YEARS= does: DefaultArxYears leaves the selection alone as soon as any YEAR_* is set, so
    /// the default set is never unioned in. Only four-digit entries are forwarded - anything else would
    /// set a property nobody reads.
    /// </summary>
    static void AppendYears(StringBuilder sb, string years)
    {
        if (string.IsNullOrEmpty(years)) return;
        foreach (string part in years.Split(','))
        {
            string year = part.Trim();
            if (year.Length != 4) continue;
            int value;
            if (!int.TryParse(year, out value)) continue;
            sb.Append(" YEAR_").Append(year).Append("=1");
        }
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

        // The UAC prompt has to belong to this process, not to the MSI. Letting Windows Installer
        // elevate itself (what happens when a per-machine MSI is started from an unelevated process)
        // elevates the server only: an immediate custom action scheduled in the execute sequence still
        // runs in an impersonated server, i.e. with the client's *unelevated* token. The WiX DTF stub
        // then cannot create the temporary directory it extracts itself into, beside the CA DLL in
        // %WINDIR%\Installer, and every managed action dies with "SFXCA: Failed to create temp
        // directory. Error code 5" - the install rolls back with 1603. Starting msiexec elevated makes
        // those actions run as the elevated user, exactly as they do when the MSI is started from an
        // elevated shell. Measured 2026-10-11; the MSI log that shows it is %TEMP%\ObjectARXWizards-msi.log.
        bool elevated = IsElevated();
        var psi = new ProcessStartInfo(msiexec, arguments) { UseShellExecute = !elevated };
        if (!elevated) psi.Verb = "runas";

        try
        {
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                Console.WriteLine("[arx] msiexec exited with " + p.ExitCode);
                return p.ExitCode;
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // 1223: the elevation prompt was declined. Report that the way msiexec reports a cancelled
            // install, so the bundle says "cancelled" instead of "failed".
            int code = ex.NativeErrorCode == 1223 ? ErrorUserExit : ErrorInstallFailure;
            Console.WriteLine("[arx] msiexec could not be started: " + ex.Message + " -> " + code);
            return code;
        }
    }

    /// <summary>True when this process already has the administrator token, so msiexec needs no prompt
    /// and its output can be captured the old way.</summary>
    static bool IsElevated()
    {
        try
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
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
