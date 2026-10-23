using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kiwindow.Engine;
using Kiwindow.Ui;

namespace Kiwindow.App
{
    /// <summary>Standalone shell: a native window (dark, captioned in the canvas color) around the shared view.</summary>
    public sealed class MainWindow : Window
    {
        readonly KiwindowView _view;

        public MainWindow()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = "Kiwindow";
            Width = 1320;
            Height = 840;
            MinWidth = 1040;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = (Brush)FindResource("Brush.Canvas");
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Kiwindow;component/kiwindow.ico"));
            UseLayoutRounding = true;

            _view = new KiwindowView(new ViewOptions
            {
                ShowBrand = true,
                Version = $"v{version.Major}.{version.Minor}.{version.Build}",
                CommandExecutable = App.ExecutablePath,
            });
            Content = _view;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                Native.UseDarkTitleBar(hwnd);
                Native.SetCaptionColor(hwnd, 0x14, 0x14, 0x13);   // Color.Canvas
            };

            // KIWINDOW_SNAPSHOT=<file.png>: render the window once it is idle, save it and exit (docs, visual review).
            var snapshot = Environment.GetEnvironmentVariable("KIWINDOW_SNAPSHOT");
            if (!string.IsNullOrEmpty(snapshot))
                ContentRendered += (s, e) => Dispatcher.BeginInvoke(new Action(() => SaveSnapshot(snapshot)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void SaveSnapshot(string path)
        {
            var element = (FrameworkElement)Content;
            var dpi = VisualTreeHelper.GetDpi(this);
            var bmp = new RenderTargetBitmap((int)(element.ActualWidth * dpi.DpiScaleX), (int)(element.ActualHeight * dpi.DpiScaleY),
                                             dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Background, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }
            bmp.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var stream = System.IO.File.Create(path)) encoder.Save(stream);
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_view.ConfirmLeave()) e.Cancel = true;
            base.OnClosing(e);
        }

        public void BringForward()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }
    }
}
