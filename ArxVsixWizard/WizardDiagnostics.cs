// Diagnostics for the wizard extension. Two facts made this worth keeping:
//
//   * a creation timeline - how long each phase of "New Project" takes, written as one
//     CreationSummary line plus a per-file WriteFile line;
//   * when an item is added relative to the last creation - every window line and every sampler
//     tick carries sinceCreation=..., which is what the "Add New Item" crash correlates with
//     (details and measurements: docs\VS-AddNewItem-Crash.md).
//
// %TEMP%\ArxVsixWizard\no-ui.flag keeps the option windows out of the way (defaults are used); the
// offline smoke test drives the wizards that way. Everything here is best effort: a diagnostics
// failure must never break a wizard run.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ArxVsixWizard
{
    internal static class WizardDiagnostics
    {
        static readonly string Dir = Path.Combine(Path.GetTempPath(), "ArxVsixWizard");
        static readonly string LogPath = Path.Combine(Dir, "wizard.log");
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static readonly object Gate = new object();

        static DateTime _watchUntil = DateTime.MinValue;
        static int _watchStarted;
        static string _vsVersion;
        static DateTime _lastCreationUtc = DateTime.MinValue;

        public static readonly int Pid = SafePid();

        /// <summary>Skip the wizard windows entirely (defaults are used); the offline smoke test uses this.</summary>
        public static bool NoUi => Flag("no-ui.flag") || Env("ARXWIZ_NOUI");

        public static string LogPathForUser => LogPath;

        // ---- logging ----

        /// <summary>One line per event: time, seconds since this assembly loaded, pid, thread, stage.</summary>
        public static void Log(string stage, string message)
        {
            try
            {
                string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} +{1,8:0.000}s pid={2} tid={3} [{4}] {5}\r\n",
                    DateTime.Now, Clock.Elapsed.TotalSeconds, Pid, GetCurrentThreadId(), stage, message);
                lock (Gate)
                {
                    Directory.CreateDirectory(Dir);
                    File.AppendAllText(LogPath, line);
                }
            }
            catch { /* diagnostics must never break a wizard run */ }
        }

        /// <summary>One line per run: thread and apartment, the switch in effect, the devenv build, the foreground window.</summary>
        public static void LogRunContext(string stage)
        {
            Log(stage, "thread=" + GetCurrentThreadId() + " apt=" + Apartment()
                + " noUi=" + (NoUi ? 1 : 0)
                + " devenv=" + VsVersion()
                + " foreground=" + DescribeForeground());
        }

        /// <summary>Foreground window plus the seconds since the last creation - the two facts every other line wants to carry.</summary>
        public static string WindowState()
            => "foreground=" + DescribeForeground() + " sinceCreation=" + SinceCreation();

        /// <summary>Called by the project wizard once the files are on disk and Save() is done.</summary>
        public static void NoteCreationDone()
        {
            try { _lastCreationUtc = DateTime.UtcNow; } catch { }
        }

        /// <summary>Seconds since the last project creation; "none" before the first one.</summary>
        public static string SinceCreation()
        {
            try
            {
                if (_lastCreationUtc == DateTime.MinValue) return "none";
                return (DateTime.UtcNow - _lastCreationUtc).TotalSeconds.ToString("0.0") + "s";
            }
            catch { return "?"; }
        }

        /// <summary>
        /// One line per project creation, with every phase the log records separately - so "how long
        /// does creating a project take" is answered by a single line instead of a scan. dialogMs is
        /// the time spent in the option page, engineMs the template engine's own work up to
        /// ProjectFinishedGenerating, writeMs the file writes, saveMs the project.Save() call.
        /// </summary>
        public static void LogCreationSummary(string project, int files, long dialogMs, long engineMs,
            long writeMs, long saveMs, long totalMs)
        {
            Log("CreationSummary", "project=" + project + " files=" + files
                + " dialogMs=" + dialogMs + " engineMs=" + engineMs + " writeMs=" + writeMs
                + " saveMs=" + saveMs + " totalMs=" + totalMs
                + " doneAt=" + DateTime.Now.ToString("HH:mm:ss.fff"));
        }

        public static string DescribeForeground() => DescribeWindow(GetForegroundWindow());

        /// <summary>pid / class / title / enabled of a window, so a foreign owner is obvious.</summary>
        public static string DescribeWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "none";
            try
            {
                uint pid;
                uint thread = GetWindowThreadProcessId(hwnd, out pid);
                var cls = new StringBuilder(96);
                GetClassName(hwnd, cls, cls.Capacity);
                return string.Format("0x{0:X}[pid={1} thread={2} class={3} enabled={4} title=\"{5}\"]",
                    hwnd.ToInt64(), pid, thread, cls,
                    IsWindowEnabled(hwnd) ? 1 : 0, WindowTitle(hwnd));
            }
            catch { return "0x" + hwnd.ToInt64().ToString("X"); }
        }

        /// <summary>
        /// The window our wizard dialogs are owned by: Visual Studio's own main window, with the
        /// foreground window only as a last resort - GetForegroundWindow() alone returns another
        /// application's window whenever Visual Studio is not in the foreground.
        /// </summary>
        public static IntPtr PreferredOwner()
        {
            IntPtr hwnd = MainWindow();
            if ((hwnd == IntPtr.Zero) || !IsWindow(hwnd)) hwnd = GetForegroundWindow();
            return hwnd;
        }

        // ---- background sampler ----

        /// <summary>
        /// Samples the process state every two seconds until <paramref name="seconds"/> after the
        /// last call; later calls just extend the window, so one sampler covers the whole session
        /// part that matters and then stops by itself. Each tick carries the seconds since the last
        /// creation (that is what the Add New Item crash correlates with), the process CPU share,
        /// the thread count and whether the main window is still enabled (a modal dialog disables
        /// it). Between the wizards no code of ours runs, so these lines are the only in-process
        /// evidence of what the state was.
        /// </summary>
        public static void StartWatch(string tag, int seconds = 300)
        {
            try
            {
                _watchUntil = DateTime.UtcNow.AddSeconds(seconds);
                if (Interlocked.Exchange(ref _watchStarted, 1) != 0) return;

                var thread = new Thread(() =>
                {
                    IntPtr main = MainWindow();
                    TimeSpan lastCpu = TimeSpan.Zero;
                    DateTime lastCpuAt = DateTime.UtcNow;
                    Log("watch/start", "tag=" + tag + " " + WindowState());
                    while (DateTime.UtcNow < _watchUntil)
                    {
                        try { Thread.Sleep(2000); } catch { }
                        if (DateTime.UtcNow >= _watchUntil) break;
                        try
                        {
                            if (main == IntPtr.Zero || !IsWindow(main)) main = MainWindow();
                            double cpuPercent = -1;
                            try
                            {
                                TimeSpan cpu = Process.GetCurrentProcess().TotalProcessorTime;
                                DateTime now = DateTime.UtcNow;
                                double ms = (now - lastCpuAt).TotalMilliseconds;
                                if (ms > 1) cpuPercent = (cpu - lastCpu).TotalMilliseconds / ms * 100.0;
                                lastCpu = cpu;
                                lastCpuAt = now;
                            }
                            catch { }

                            Log("watch/tick", "sinceCreation=" + SinceCreation()
                                + " cpu=" + (cpuPercent < 0 ? "?" : cpuPercent.ToString("0")) + "%"
                                + " threads=" + ThreadCount()
                                + " mainEnabled=" + (main != IntPtr.Zero && IsWindowEnabled(main) ? 1 : 0));
                        }
                        catch { }
                    }
                })
                { IsBackground = true, Name = "ArxVsixWizard.Watch" };
                thread.Start();
            }
            catch { }
        }

        // ---- helpers ----

        static IntPtr MainWindow()
        {
            try { return Process.GetCurrentProcess().MainWindowHandle; }
            catch { return IntPtr.Zero; }
        }

        static int ThreadCount()
        {
            try { return Process.GetCurrentProcess().Threads.Count; }
            catch { return -1; }
        }

        static string Apartment()
        {
            try { return Thread.CurrentThread.GetApartmentState().ToString(); }
            catch { return "?"; }
        }

        static string VsVersion()
        {
            if (_vsVersion != null) return _vsVersion;
            try
            {
                _vsVersion = Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion;
            }
            catch { _vsVersion = "?"; }
            return _vsVersion;
        }

        /// <summary>
        /// A window title that cannot block the caller: WM_GETTEXT is sent with a short timeout, so a
        /// busy UI thread yields "?" instead of freezing whoever is writing the log.
        /// </summary>
        static string WindowTitle(IntPtr hwnd)
        {
            try
            {
                var text = new StringBuilder(180);
                IntPtr result;
                SendMessageTimeout(hwnd, 0x000D /* WM_GETTEXT */, (IntPtr)text.Capacity, text,
                    SmtoAbortIfHung | SmtoBlock, 60, out result);
                return text.ToString();
            }
            catch { return "?"; }
        }

        static bool Flag(string fileName)
        {
            try { return File.Exists(Path.Combine(Dir, fileName)); }
            catch { return false; }
        }

        static bool Env(string name)
        {
            try { return Environment.GetEnvironmentVariable(name) == "1"; }
            catch { return false; }
        }

        static int SafePid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return -1; }
        }

        const uint SmtoBlock = 0x0001;
        const uint SmtoAbortIfHung = 0x0002;

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam,
            uint flags, uint timeout, out IntPtr result);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
    }
}
