using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    // macOS only: auto-hide the Dock while Hearthstone is the frontmost app, and put it back
    // when the game loses focus or quits.
    //
    // It only ever flips the Dock from "always shown" to "auto-hide" and back. If the Dock
    // already auto-hides, it is left alone. While the Dock is hidden by us, a marker file
    // exists; if the game crashes, the next start (or launch_hsmod.sh, once the game process
    // is gone) sees the marker and restores the Dock.
    public static class DockAutoHide
    {
        private const string MarkerFileName = "HsMod.dock_hidden";
        private const int OsascriptTimeoutMs = 60000;   // the first run waits on the Automation permission prompt
        private const int QuitTimeoutMs = 3000;

        private static readonly object applyLock = new object();
        private static readonly AutoResetEvent wake = new AutoResetEvent(false);
        private static Thread worker;
        private static volatile bool hasFocus = true;
        private static volatile bool stopping;
        private static bool weHidDock;   // guarded by applyLock

        private static string MarkerPath => Path.Combine(BepInEx.Paths.ConfigPath, MarkerFileName);

        public static bool IsSupported => Application.platform == RuntimePlatform.OSXPlayer;

        public static void Init()
        {
            if (!IsSupported || worker != null) return;

            // A marker left behind means an earlier run hid the Dock and never restored it.
            weHidDock = File.Exists(MarkerPath);
            hasFocus = Application.isFocused;

            isDockAutoHideEnable.SettingChanged += delegate { wake.Set(); };
            worker = new Thread(WorkerLoop) { IsBackground = true, Name = "HsMod.DockAutoHide" };
            worker.Start();
            wake.Set();
        }

        public static void OnFocus(bool focus)
        {
            if (worker == null) return;
            hasFocus = focus;
            wake.Set();
        }

        public static void Shutdown()
        {
            if (worker == null || stopping) return;
            stopping = true;
            wake.Set();
            // Don't let a stuck worker block the game from quitting.
            if (!Monitor.TryEnter(applyLock, QuitTimeoutMs)) return;
            try
            {
                if (weHidDock) Apply(false, QuitTimeoutMs);
            }
            finally
            {
                Monitor.Exit(applyLock);
            }
        }

        private static void WorkerLoop()
        {
            while (!stopping)
            {
                wake.WaitOne();
                if (stopping) return;
                lock (applyLock)
                {
                    if (stopping) return;
                    try
                    {
                        bool wantHidden = isPluginEnable.Value && isDockAutoHideEnable.Value && hasFocus;
                        if (wantHidden && !weHidDock)
                        {
                            bool? alreadyHidden = GetAutoHide();
                            if (alreadyHidden == false) Apply(true, OsascriptTimeoutMs);
                        }
                        else if (!wantHidden && weHidDock)
                        {
                            Apply(false, OsascriptTimeoutMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"DockAutoHide: {ex.Message}");
                    }
                }
            }
        }

        // Caller holds applyLock.
        private static void Apply(bool hide, int timeoutMs)
        {
            if (hide)
            {
                // Write the marker first, so a crash right after hiding can still be recovered.
                File.WriteAllText(MarkerPath, DateTime.Now.ToString("o"));
                if (RunOsascript("tell application \"System Events\" to set autohide of dock preferences to true", timeoutMs) != null)
                    weHidDock = true;
                else
                    File.Delete(MarkerPath);
            }
            else
            {
                if (RunOsascript("tell application \"System Events\" to set autohide of dock preferences to false", timeoutMs) != null)
                {
                    weHidDock = false;
                    File.Delete(MarkerPath);
                }
            }
        }

        private static bool? GetAutoHide()
        {
            string output = RunOsascript("tell application \"System Events\" to get autohide of dock preferences", OsascriptTimeoutMs);
            if (output == null) return null;
            output = output.Trim();
            if (output == "true") return true;
            if (output == "false") return false;
            return null;
        }

        // Returns stdout, or null on failure. The script goes in over stdin to avoid argument quoting.
        private static string RunOsascript(string script, int timeoutMs)
        {
            var psi = new ProcessStartInfo("/usr/bin/osascript")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var process = Process.Start(psi))
            {
                process.StandardInput.Write(script);
                process.StandardInput.Close();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "DockAutoHide: osascript timed out");
                    return null;
                }
                if (process.ExitCode != 0)
                {
                    string error = stderrTask.Result.Trim();
                    if (error.Contains("-1743"))
                        error += " (allow Hearthstone to control System Events in System Settings > Privacy & Security > Automation)";
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"DockAutoHide: osascript failed: {error}");
                    return null;
                }
                return stdoutTask.Result;
            }
        }
    }
}
