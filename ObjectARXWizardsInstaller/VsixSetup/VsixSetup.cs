// Installs the ObjectARX wizards VSIX into the current user's Visual Studio.
// The bundle runs this after the per-machine MSI, so it must NOT require elevation and must not
// assume Visual Studio is in a specific edition folder.
using System;
using System.Diagnostics;
using System.IO;

static class VsixSetup
{
    const string DefaultVsixName = "ObjectARXMultiYearWizards.vsix";

    static int Main(string[] args)
    {
        string vsix = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultVsixName);
        if (!File.Exists(vsix))
        {
            Console.WriteLine("[arx] VSIX not found: " + vsix);
            return 0;
        }

        string ide = FindVsInstall();
        if (ide == null)
        {
            Console.WriteLine("[arx] Visual Studio 2022 not found - install the extension by hand:");
            Console.WriteLine("      " + vsix);
            return 0;
        }

        string installer = Path.Combine(ide, @"Common7\IDE\VSIXInstaller.exe");
        if (!File.Exists(installer))
        {
            Console.WriteLine("[arx] VSIXInstaller.exe not found under " + ide);
            return 0;
        }

        Console.WriteLine("[arx] Installing " + vsix);
        var psi = new ProcessStartInfo(installer, "/quiet \"" + vsix + "\"") { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            Console.WriteLine("[arx] VSIXInstaller exited with " + p.ExitCode);
            return p.ExitCode;
        }
    }

    /// <summary>
    /// vswhere.exe is the supported locator; the classic SxS\VS7 registry key has no 17.0 value on
    /// VS2022 machines, so the standard edition folders are only a fallback.
    /// </summary>
    static string FindVsInstall()
    {
        string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                      @"Microsoft Visual Studio\Installer\vswhere.exe");
        if (File.Exists(vswhere))
        {
            try
            {
                var psi = new ProcessStartInfo(vswhere, "-latest -products * -property installationPath -nologo")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (!string.IsNullOrEmpty(output) && Directory.Exists(output)) return output;
                }
            }
            catch { }
        }

        foreach (var edition in new[] { "Enterprise", "Professional", "Community", "BuildTools", "Preview" })
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                      @"Microsoft Visual Studio\2022\" + edition);
            if (Directory.Exists(dir)) return dir;
        }
        return null;
    }
}