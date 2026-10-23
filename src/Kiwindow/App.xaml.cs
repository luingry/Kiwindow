using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Kiwindow.Engine;
using Kiwindow.Ui;

namespace Kiwindow.App
{
    public partial class App : Application
    {
        /*
          Command line (Windows shortcuts, hotkey tools), without opening the window:
            Kiwindow.exe --apply [profile name | path.json]     no name = most recent profile
            Kiwindow.exe --capture [name]
          The Portuguese verbs of the original tool (--aplicar, --restaurar, --capturar) still work.
        */
        const string InstanceName = "Local\\Kiwindow.App";
        const string ShowSignalName = "Local\\Kiwindow.App.Show";

        Mutex _instance;
        EventWaitHandle _showSignal;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var language = Environment.GetEnvironmentVariable("KIWINDOW_LANG");   // "en" or "pt-BR" overrides Windows
            if (string.IsNullOrEmpty(language)) Strings.UseSystemLanguage(); else Strings.SetLanguage(language);

            var args = e.Args;
            if (args.Length > 0)
            {
                var cmd = args[0].TrimStart('-', '/').ToLowerInvariant();
                var arg = string.Join(" ", args.Skip(1)).Trim().Trim('"');
                if (cmd == "apply" || cmd == "aplicar" || cmd == "restaurar")
                {
                    var p = Find(arg);
                    if (p != null) Restorer.Run(p);
                    Shutdown(p != null ? 0 : 1);
                    return;
                }
                if (cmd == "capture" || cmd == "capturar")
                {
                    ProfileStore.Save(Capture.Take(arg.Length > 0 ? arg : KiwindowView.DefaultName()));
                    Shutdown(0);
                    return;
                }
            }

            // One window: a second launch brings the first one forward.
            _instance = new Mutex(true, InstanceName, out bool first);
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
            if (!first)
            {
                _showSignal.Set();
                Shutdown(0);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            var listener = new Thread(() =>
            {
                while (_showSignal.WaitOne())
                    Dispatcher.BeginInvoke(new Action(window.BringForward));
            }) { IsBackground = true, Name = "Kiwindow show signal" };
            listener.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instance?.Dispose();
            base.OnExit(e);
        }

        static Profile Find(string arg)
        {
            if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(arg)) return ProfileStore.Load(arg);
            var all = ProfileStore.LoadAll();
            if (arg.Length == 0) return all.FirstOrDefault();
            return all.FirstOrDefault(p => string.Equals(p.Nome, arg, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(p => p.Nome.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static string ExecutablePath => Process.GetCurrentProcess().MainModule.FileName;
    }
}
