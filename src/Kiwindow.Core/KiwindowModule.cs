using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kiwindow.Engine;
using Kiwindow.Ui;
using SwissTechKnife.Sdk;
using SdkUi = SwissTechKnife.Sdk.Ui;

namespace Kiwindow
{
    /// <summary>
    /// SwissTechKnife entry point (<c>"activation": "onDemand"</c>): nothing runs until the page or the tray
    /// menu is opened. The page is the same view as the standalone app; profiles live in the shared
    /// %LOCALAPPDATA%\Kiwindow folder, so both see the same list.
    /// </summary>
    public sealed class KiwindowModule : IModule, IModuleTrayMenu
    {
        const int TrayProfiles = 8;

        IModuleContext _context;
        KiwindowView _view;
        bool _busy;

        public void Initialize(IModuleContext context)
        {
            _context = context;
            Strings.UseHostLanguage(context.DataDirectory);
        }

        public FrameworkElement CreateView()
        {
            _view = new KiwindowView(new ViewOptions());
            return _view;
        }

        public IEnumerable<Control> CreateTrayMenuItems()
        {
            var items = new List<Control>();
            var capture = new MenuItem { Header = Strings.Get("Tray.Capture"), IsEnabled = !_busy };
            SdkUi.SetIcon(capture, char.ConvertFromUtf32(0xE722));   // Camera
            capture.Click += async (s, e) =>
            {
                _busy = true;
                try
                {
                    var p = await KiwindowView.OnSta(() => Capture.Take(KiwindowView.DefaultName()));
                    ProfileStore.Save(p);
                    _view?.Reload(p.Arquivo);
                    _context.Notify(Strings.Format("Tray.Captured", p.Nome, p.Janelas.Count), NotificationKind.Success);
                }
                catch (Exception ex)
                {
                    _context.Log.Error("Capture failed", ex);
                    _context.Notify(Strings.Get("Toast.CaptureFailed"), NotificationKind.Error);
                }
                finally { _busy = false; }
            };
            items.Add(capture);

            List<Profile> profiles;
            try { profiles = ProfileStore.LoadAll().Take(TrayProfiles).ToList(); }
            catch (Exception ex) { _context.Log.Warn("Could not read profiles: " + ex.Message); profiles = new List<Profile>(); }
            if (profiles.Count > 0) items.Add(new Separator());
            foreach (var profile in profiles)
            {
                var item = new MenuItem { Header = Strings.Format("Tray.Apply", profile.Nome), IsEnabled = !_busy };
                SdkUi.SetIcon(item, char.ConvertFromUtf32(0xE768));  // Play
                var target = profile;
                item.Click += async (s, e) =>
                {
                    _busy = true;
                    try
                    {
                        var r = await Restorer.RunAsync(target);
                        _context.Notify(Strings.Format("Tray.Applied", target.Nome, r.Posicionadas, r.Total),
                                        r.Posicionadas == r.Total ? NotificationKind.Success : NotificationKind.Warning);
                    }
                    catch (Exception ex)
                    {
                        _context.Log.Error("Apply failed", ex);
                        _context.Notify(Strings.Get("Toast.ApplyFailed"), NotificationKind.Error);
                    }
                    finally { _busy = false; }
                };
                items.Add(item);
            }
            return items;
        }

        public void Dispose() => _view = null;
    }
}
