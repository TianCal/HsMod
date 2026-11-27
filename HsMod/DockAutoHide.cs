using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using static HsMod.PluginConfig;

namespace HsMod
{
    // macOS only: auto-hide the Dock and/or the menu bar while Hearthstone is the frontmost
    // app, and put them back when the game loses focus or quits.
    //
    // Each one is only ever flipped from "shown" to "auto-hide" and back. If it already
    // auto-hides, it is left alone. While we have one hidden, its marker file exists; if the
    // game crashes, the next start (or launch_hsmod.sh, once the game process is gone) sees
    // the marker and restores it.
    public static class DockAutoHide
    {
        private sealed class Bar
        {
            public string Name;
            public string Property;      // System Events "dock preferences" property
            public string MarkerFileName;
            public Func<bool> Enabled;
            public bool WeHid;           // guarded by applyLock
            public string MarkerPath => Path.Combine(BepInEx.Paths.ConfigPath, MarkerFileName);
        }

        private static readonly Bar[] bars =
        {
            new Bar { Name = "Dock", Property = "autohide", MarkerFileName = "HsMod.dock_hidden", Enabled = () => isDockAutoHideEnable.Value },
            new Bar { Name = "menu bar", Property = "autohide menu bar", MarkerFileName = "HsMod.menubar_hidden", Enabled = () => isMenuBarAutoHideEnable.Value },
        };

        private const int OsascriptTimeoutMs = 60000;   // the first run waits on the Automation permission prompt
        private const int QuitTimeoutMs = 3000;

        private static readonly object applyLock = new object();
        private static readonly AutoResetEvent wake = new AutoResetEvent(false);
        private static Thread worker;
        private static volatile bool hasFocus = true;
        private static volatile bool stopping;

        public static bool IsSupported => Application.platform == RuntimePlatform.OSXPlayer;

        public static void Init()
        {
            if (!IsSupported || worker != null) return;

            // A marker left behind means an earlier run hid that bar and never restored it.
            foreach (var bar in bars) bar.WeHid = File.Exists(bar.MarkerPath);
            hasFocus = Application.isFocused;

            isDockAutoHideEnable.SettingChanged += delegate { wake.Set(); };
            isMenuBarAutoHideEnable.SettingChanged += delegate { wake.Set(); };
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
                foreach (var bar in bars)
                    if (bar.WeHid) Apply(bar, false, QuitTimeoutMs);
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
                    foreach (var bar in bars)
                    {
                        if (stopping) return;
                        try
                        {
                            bool wantHidden = isPluginEnable.Value && bar.Enabled() && hasFocus;
                            if (wantHidden && !bar.WeHid)
                            {
                                if (GetAutoHide(bar) == false) Apply(bar, true, OsascriptTimeoutMs);
                            }
                            else if (!wantHidden && bar.WeHid)
                            {
                                Apply(bar, false, OsascriptTimeoutMs);
                            }
                        }
                        catch (Exception ex)
                        {
                            Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"DockAutoHide ({bar.Name}): {ex.Message}");
                        }
                    }
                }
            }
        }

        // Caller holds applyLock.
        private static void Apply(Bar bar, bool hide, int timeoutMs)
        {
            string script = $"tell application \"System Events\" to set {bar.Property} of dock preferences to {(hide ? "true" : "false")}";
            if (hide)
            {
                // Write the marker first, so a crash right after hiding can still be recovered.
                File.WriteAllText(bar.MarkerPath, DateTime.Now.ToString("o"));
                if (RunOsascript(script, timeoutMs) != null)
                    bar.WeHid = true;
                else
                    File.Delete(bar.MarkerPath);
            }
            else
            {
                if (RunOsascript(script, timeoutMs) != null)
                {
                    bar.WeHid = false;
                    File.Delete(bar.MarkerPath);
                }
            }
        }

        private static bool? GetAutoHide(Bar bar)
        {
            string output = RunOsascript($"tell application \"System Events\" to get {bar.Property} of dock preferences", OsascriptTimeoutMs);
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
