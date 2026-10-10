// Installs the ObjectARX wizards VSIX for the user who runs the setup - into every Visual Studio
// on the machine, not just the newest one. The bundle chains this after the per-machine MSI, so it
// must NOT require elevation: the extension is per-user, and an elevated VSIXInstaller would
// register it against the wrong profile.
//
// Burn hides the console, so everything is mirrored into %TEMP%\ObjectARXWizards-vsix.log, and a
// real failure now raises a message box instead of the silent Vital="no" exit the bundle had before.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

static class VsixSetup
{
    const string DefaultVsixName = "ObjectARXMultiVersionWizards.vsix";
    // VSIXInstaller's AlreadyInstalledException: this version is already registered for the user.
    const int AlreadyInstalled = 1001;
    const uint MB_OK = 0x0;
    const uint MB_ICONWARNING = 0x30;
    const uint MB_SETFOREGROUND = 0x10000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ObjectARXWizards-vsix.log");

    static int Main(string[] args)
    {
        // The bundle's uninstall runs this launcher again, with --uninstall: the extension is per user
        // and only this process knows how it was put in, so it takes it out the same way.
        foreach (string arg in args)
            if (string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)) return UninstallAll();

        string vsix = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultVsixName);
        Log("VSIX setup start; vsix=" + vsix);
        Log("elevated=" + IsElevated() + " (the extension has to land in the invoking user's profile)");

        if (!File.Exists(vsix))
        {
            Log("VSIX not found - nothing to do");
            return 0;
        }

        var instances = FindVsInstalls();
        Log("Visual Studio instances: " + (instances.Count == 0 ? "(none)" : string.Join("; ", instances.ToArray())));
        if (instances.Count == 0)
        {
            // The MSI does not insist on Visual Studio any more, so this is a supported situation:
            // the wizards are on disk and the extension can be added later by hand.
            Log("no Visual Studio found; install the extension by hand: " + vsix);
            return 0;
        }

        int failed = 0;
        foreach (string ide in instances)
        {
            int code = InstallInto(ide, vsix);
            Log("  " + ide + " -> VSIXInstaller exit " + code);
            if (code != 0 && code != AlreadyInstalled) failed++;
        }

        if (failed > 0)
        {
            Log("failed for " + failed + " instance(s)");
            MessageBox(IntPtr.Zero,
                       "The ObjectARX extension was not installed for " + failed + " Visual Studio instance(s).\r\n\r\n" +
                       "The usual cause is Visual Studio (or the Visual Studio Installer) running, or a profile the " +
                       "setup cannot write to. The wizards themselves are installed; install the extension again " +
                       "by running this file:\r\n" + vsix + "\r\n\r\nDetails: " + LogPath,
                       "ObjectARX Multi-Version Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
            return 1;
        }

        Log("done");
        return 0;
    }

    static int InstallInto(string ide, string vsix)
    {
        string installer = Path.Combine(ide, @"Common7\IDE\VSIXInstaller.exe");
        if (!File.Exists(installer))
        {
            Log("VSIXInstaller.exe not found under " + ide);
            return -1;
        }
        var psi = new ProcessStartInfo(installer, "/quiet \"" + vsix + "\"") { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    /// <summary>True when the current process runs with an administrator token.</summary>
    static bool IsElevated()
    {
        try
        {
            using (var identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>
    /// Every Visual Studio with a VSIXInstaller.exe. vswhere is the supported locator, asked for
    /// all products and prereleases so a 2026 or a preview instance is included; the Program Files
    /// scan is the fallback when vswhere is missing, and it also catches installs vswhere does not
    /// list (BuildTools and other editions).
    /// </summary>
    static List<string> FindVsInstalls()
    {
        var result = new List<string>();

        string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                      @"Microsoft Visual Studio\Installer\vswhere.exe");
        if (File.Exists(vswhere))
        {
            try
            {
                var psi = new ProcessStartInfo(vswhere, "-all -prerelease -products * -property installationPath -nologo")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string dir = line.Trim();
                        if (Directory.Exists(dir) && !result.Contains(dir)) result.Add(dir);
                    }
                }
            }
            catch (Exception ex) { Log("vswhere failed: " + ex.Message); }
        }

        foreach (string root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            string vsRoot = Path.Combine(root, @"Microsoft Visual Studio");
            if (!Directory.Exists(vsRoot)) continue;
            foreach (string versionDir in Directory.GetDirectories(vsRoot))
            {
                foreach (string editionDir in Directory.GetDirectories(versionDir))
                {
                    if (File.Exists(Path.Combine(editionDir, @"Common7\IDE\VSIXInstaller.exe")) &&
                        !result.Contains(editionDir))
                        result.Add(editionDir);
                }
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Removes the extension from every Visual Studio on the machine. Called by the bundle's
    /// uninstall: the MSI takes the machine payload away and the extension - installed per user by
    /// this launcher - has to follow, or the IDE keeps offering the ObjectARX project wizards.
    /// </summary>
    static int UninstallAll()
    {
        Log("VSIX uninstall start");
        Log("elevated=" + IsElevated() + " (the extension lives in the invoking user's profile)");

        var instances = FindVsInstalls();
        if (instances.Count == 0)
        {
            Log("no Visual Studio found; nothing to remove");
            return 0;
        }

        foreach (string ide in instances)
        {
            string installer = Path.Combine(ide, @"Common7\IDE\VSIXInstaller.exe");
            if (!File.Exists(installer))
            {
                Log("VSIXInstaller.exe not found under " + ide);
                continue;
            }
            var psi = new ProcessStartInfo(installer, "/quiet /uninstall:ObjectARX.MultiYear.Wizard") { UseShellExecute = false };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
                Log("  " + ide + " -> VSIXInstaller uninstall exit " + p.ExitCode);
            }
        }

        // A leftover extension must never stop the bundle from uninstalling; the log says what happened.
        Log("done");
        return 0;
    }

    static void Log(string message)
    {
        try { File.AppendAllText(LogPath, DateTime.Now.ToString("s") + " " + message + "\r\n"); }
        catch { }
        Console.WriteLine("[arx] " + message);
    }
}
