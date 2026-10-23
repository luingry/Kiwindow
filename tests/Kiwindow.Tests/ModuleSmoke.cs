using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SwissTechKnife.Sdk;

namespace Kiwindow.Tests
{
    /// <summary>Loads the module the way the host does: theme in Application.Resources, a context, the page and the tray menu.</summary>
    internal static class ModuleSmoke
    {
        sealed class FakeLog : IModuleLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message, Exception exception = null) { throw new Exception(message, exception); }
        }

        sealed class FakeContext : IModuleContext
        {
            public string ModuleId => "kiwindow";
            public Version ModuleVersion => new Version(1, 0, 0);
            public Version HostVersion => new Version(0, 4, 6);
            public string InstallDirectory => AppDomain.CurrentDomain.BaseDirectory;
            public string DataDirectory { get; set; }
            public Dispatcher Dispatcher => Dispatcher.CurrentDispatcher;
            public IModuleLog Log { get; } = new FakeLog();
            public void Notify(string message, NotificationKind kind = NotificationKind.Info) { }
            public void ShowModulePage() { }
        }

        public static void Run()
        {
            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/SwissTechKnife.Sdk;component/Theme/Theme.xaml"),
            });

            // Host layout: <home>\data\kiwindow next to <home>\settings.json.
            var home = Path.Combine(Path.GetTempPath(), "kiwindow-tests", "stk");
            var data = Path.Combine(home, "data", "kiwindow");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(home, "settings.json"), "{ \"language\": \"pt-BR\" }");

            var module = (IModule)Activator.CreateInstance(typeof(KiwindowModule));
            module.Initialize(new FakeContext { DataDirectory = data });
            if (Ui.Strings.Language != Ui.Strings.PortugueseBrazil) throw new Exception("host language not followed");

            var page = module.CreateView();
            page.Measure(new Size(1200, 800));
            page.Arrange(new Rect(0, 0, 1200, 800));
            page.UpdateLayout();
            if (page.ActualWidth <= 0) throw new Exception("page did not lay out");

            var tray = ((IModuleTrayMenu)module).CreateTrayMenuItems().ToList();
            if (tray.Count == 0) throw new Exception("tray menu is empty");
            module.Dispose();
            Ui.Strings.SetLanguage("en");
        }
    }
}
