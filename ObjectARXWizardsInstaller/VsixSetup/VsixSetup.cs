// Installs the ObjectARX wizards VSIX for the user who runs the setup - into every Visual Studio
// the VSIX says it supports, which is not the same as every Visual Studio on the machine. The bundle
// chains this after the per-machine MSI, so it must NOT require elevation: the extension is per-user,
// and an elevated VSIXInstaller would register it against the wrong profile.
//
// VSIXInstaller takes one to two minutes per instance, so this launcher does three things to keep the
// installer short (it used to spend seven minutes here, once per VS, and ended with a warning box):
//   * an instance whose version is outside the VSIX's InstallationTarget range is skipped - VS would
//     not accept the extension anyway,
//   * an instance that already has this extension (same id, same or newer version, in the user's
//     extension folder) is skipped without running VSIXInstaller at all, which is what a re-install
//     hits,
//   * exit codes that disagree with what actually landed are resolved by looking at the folder again,
//     so a "2004" that did install the extension is not reported as a failure.
// Every VSIXInstaller call gets its own log file next to this one, because its exit codes are not
// documented and the failure has to be readable.
//
// Burn hides the console, so everything is mirrored into %TEMP%\ObjectARXWizards-vsix.log, and a
// real failure raises a message box.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;

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

        string id, version;
        List<double> ranges = null;
        ReadManifest(vsix, out id, out version, ref ranges);
        if (id != null) Log("manifest: id=" + id + " version=" + version + " targets=" + RangeText(ranges));

        var instances = FindVsInstalls();
        Log("Visual Studio instances: " + (instances.Count == 0 ? "(none)" : string.Join("; ", instances.ToArray())));
        if (instances.Count == 0)
        {
            // The MSI does not insist on Visual Studio any more, so this is a supported situation:
            // the wizards are on disk and the extension can be added later by hand.
            Log("no Visual Studio found; install the extension by hand: " + vsix);
            return 0;
        }

        var failures = new List<string>();
        int installed = 0;
        int skipped = 0;
        foreach (string ide in instances)
        {
            double vsVersion = IdeVersion(ide);
            if (ranges != null && ranges.Count > 0 && !InRange(ranges, vsVersion))
            {
                Log("  " + ide + " -> skipped: not a target of this VSIX (" + RangeText(ranges) + ")");
                skipped++;
                continue;
            }
            if (id != null && IsInstalledFor(vsVersion, id, version))
            {
                Log("  " + ide + " -> skipped: extension " + version + " is already installed for this user");
                skipped++;
                continue;
            }

            int code = InstallInto(ide, vsix, vsVersion);
            Log("  " + ide + " -> VSIXInstaller exit " + code);
            bool ok = code == 0 || code == AlreadyInstalled;
            if (!ok && id != null && IsInstalledFor(vsVersion, id, version))
            {
                Log("  " + ide + " -> exit " + code + ", but the extension is registered now; taking that as installed");
                ok = true;
            }
            if (ok) installed++;
            else failures.Add(ide + " (VSIXInstaller exit " + code + ")");
        }

        if (failures.Count > 0)
        {
            Log("failed for " + failures.Count + " instance(s): " + string.Join("; ", failures.ToArray()));
            MessageBox(IntPtr.Zero,
                       "The ObjectARX extension was not installed for:\r\n" + string.Join("\r\n", failures.ToArray()) +
                       "\r\n\r\nThe usual cause is Visual Studio (or the Visual Studio Installer) running, or a profile the " +
                       "setup cannot write to. The wizards themselves are installed; install the extension again " +
                       "by running this file:\r\n" + vsix +
                       "\r\n\r\nDetails: " + LogPath,
                       "ObjectARX Multi-Version Wizards", MB_OK | MB_ICONWARNING | MB_SETFOREGROUND);
            return 1;
        }

        Log("done (" + installed + " installed, " + skipped + " skipped)");
        return 0;
    }

    /// <summary>Runs VSIXInstaller for one instance, with its own log file.</summary>
    static int InstallInto(string ide, string vsix, double vsVersion)
    {
        string installer = Path.Combine(ide, @"Common7\IDE\VSIXInstaller.exe");
        if (!File.Exists(installer))
        {
            Log("VSIXInstaller.exe not found under " + ide);
            return -1;
        }
        string logFile = Path.Combine(Path.GetTempPath(),
            string.Format(CultureInfo.InvariantCulture, "ObjectARXWizards-vsix-{0:0.0}.log", vsVersion));
        string arguments = "/quiet";
        if (SupportsLogFile(installer)) arguments += " /logFile \"" + logFile + "\"";
        arguments += " \"" + vsix + "\"";

        var psi = new ProcessStartInfo(installer, arguments) { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    /// <summary>
    /// VS2017's VSIXInstaller has no /logFile switch and answers with a usage error, so ask the
    /// executable itself once per version.
    /// </summary>
    static readonly Dictionary<string, bool> LogFileSupport = new Dictionary<string, bool>();
    static bool SupportsLogFile(string installer)
    {
        double version = IdeVersion(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(installer))));
        lock (LogFileSupport)
        {
            bool known;
            if (LogFileSupport.TryGetValue(installer, out known)) return known;
            bool supports = version >= 17.0;
            LogFileSupport[installer] = supports;
            return supports;
        }
    }

    /// <summary>
    /// Extension identity, version and supported Visual Studio versions, straight out of the .vsix
    /// (a zip: extension.vsixmanifest). Left null when the file cannot be read - the caller then
    /// falls back to trying every instance.
    /// </summary>
    static void ReadManifest(string vsix, out string id, out string version, ref List<double> ranges)
    {
        id = null;
        version = null;
        try
        {
            using (var zip = ZipFile.OpenRead(vsix))
            {
                var entry = zip.GetEntry("extension.vsixmanifest");
                if (entry == null)
                {
                    Log("extension.vsixmanifest is not in the VSIX; every instance will be tried");
                    return;
                }
                string text;
                using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();

                var idMatch = Regex.Match(text, "<Identity[^>]*\\bId=\"([^\"]+)\"");
                var versionMatch = Regex.Match(text, "<Identity[^>]*\\bVersion=\"([^\"]+)\"");
                if (idMatch.Success) id = idMatch.Groups[1].Value;
                if (versionMatch.Success) version = versionMatch.Groups[1].Value;

                ranges = new List<double>();
                foreach (Match m in Regex.Matches(text, "<InstallationTarget[^>]*Version=\"\\[([0-9.]+),([0-9.]+)\\)\""))
                {
                    double lo = VsNumber(m.Groups[1].Value);
                    double hi = VsNumber(m.Groups[2].Value);
                    if (!ranges.Contains(lo)) ranges.Add(lo);
                    if (!ranges.Contains(hi)) ranges.Add(hi);
                }
            }
        }
        catch (Exception ex)
        {
            Log("VSIX manifest could not be read (" + ex.Message + "); every instance will be tried");
            id = null;
            version = null;
            ranges = null;
        }
    }

    /// <summary>"17.0" as a double that orders correctly (17.9 -&gt; 17.009, so it stays below 18.0).</summary>
    static double VsNumber(string version)
    {
        string[] parts = version.Split('.');
        double major = 0, minor = 0;
        if (parts.Length > 0) double.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
        if (parts.Length > 1) double.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
        return major + minor / 1000.0;
    }

    static bool InRange(List<double> ranges, double version)
    {
        if (version <= 0) return true;                       // unknown: try rather than skip
        for (int i = 0; i + 1 < ranges.Count; i += 2)
            if (version >= ranges[i] && version < ranges[i + 1]) return true;
        return false;
    }

    static string RangeText(List<double> ranges)
    {
        if (ranges == null || ranges.Count == 0) return "(any)";
        var parts = new List<string>();
        for (int i = 0; i + 1 < ranges.Count; i += 2)
            parts.Add(string.Format(CultureInfo.InvariantCulture, "[{0:0.0}, {1:0.0})", ranges[i], ranges[i + 1]));
        return string.Join(" ", parts.ToArray());
    }

    /// <summary>The Visual Studio version of an installation folder, from devenv.exe. 0 when unknown.</summary>
    static double IdeVersion(string ide)
    {
        if (string.IsNullOrEmpty(ide)) return 0;
        foreach (string exe in new[] { @"Common7\IDE\devenv.exe", @"Common7\IDE\VSIXInstaller.exe" })
        {
            string path = Path.Combine(ide, exe);
            if (!File.Exists(path)) continue;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                return info.FileMajorPart + info.FileMinorPart / 1000.0;
            }
            catch { }
        }
        return 0;
    }

    /// <summary>
    /// True when this user already has that extension - same id, same or newer version - in the
    /// extension folder of a hive for that Visual Studio version. VS anonymises the folder names, so
    /// the manifest inside each one is what gets read.
    /// </summary>
    static bool IsInstalledFor(double vsVersion, string id, string version)
    {
        if (vsVersion <= 0) return false;
        try
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       @"Microsoft\VisualStudio");
            if (!Directory.Exists(root)) return false;
            string hivePattern = string.Format(CultureInfo.InvariantCulture, "{0:0.0}*", vsVersion);
            foreach (string hive in Directory.GetDirectories(root, hivePattern))
            {
                string extensions = Path.Combine(hive, "Extensions");
                if (!Directory.Exists(extensions)) continue;
                foreach (string extension in Directory.GetDirectories(extensions))
                {
                    string manifest = Path.Combine(extension, "extension.vsixmanifest");
                    if (!File.Exists(manifest)) continue;
                    string text = File.ReadAllText(manifest);
                    if (text.IndexOf("Id=\"" + id + "\"", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var versionMatch = Regex.Match(text, "<Identity[^>]*\\bVersion=\"([^\"]+)\"");
                    if (!versionMatch.Success) return true;
                    return CompareVersions(versionMatch.Groups[1].Value, version) >= 0;
                }
            }
        }
        catch (Exception ex) { Log("extension folder could not be checked: " + ex.Message); }
        return false;
    }

    static int CompareVersions(string a, string b)
    {
        try { return Version.Parse(a).CompareTo(Version.Parse(b)); }
        catch { return 0; }
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
                // No WaitForExit here, unlike the install: VSIXInstaller launches a chain of copies of
                // itself and can stay alive in its own UI thread long after the extension is gone. The
                // Inno uninstaller froze on exactly that; the bundle must not wait for it either.
                Log("  " + ide + " -> VSIXInstaller uninstall started (not waited for)");
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
