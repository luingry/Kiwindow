using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kiwindow.Engine;

namespace Kiwindow.Ui
{
    /// <summary>How the view is hosted.</summary>
    public sealed class ViewOptions
    {
        /// <summary>Show the logo, name and version above the profiles (standalone app; the host has its own header).</summary>
        public bool ShowBrand { get; set; }

        public string Version { get; set; }

        /// <summary>Executable for the "copy shortcut command" item; null hides the item.</summary>
        public string CommandExecutable { get; set; }
    }

    public partial class KiwindowView : UserControl
    {
        static readonly ConcurrentDictionary<string, ImageSource> Icons = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        readonly ViewOptions _options;
        readonly ObservableCollection<Profile> _profiles = new ObservableCollection<Profile>();
        readonly DispatcherTimer _toastTimer = new DispatcherTimer();
        ObservableCollection<WindowEntry> _windows = new ObservableCollection<WindowEntry>();
        Profile _current;
        bool _dirty, _loading, _ignoreSelection, _applying;

        public KiwindowView(ViewOptions options = null)
        {
            _options = options ?? new ViewOptions();
            InitializeComponent();

            if (_options.ShowBrand)
            {
                BrandRow.Visibility = Visibility.Visible;
                VersionText.Text = _options.Version;
            }
            ProfileList.ItemsSource = _profiles;
            Preview.SelecaoMudou += (s, w) => { WindowList.SelectedItem = w; if (w != null) WindowList.ScrollIntoView(w); };
            Preview.RemoverPedido += (s, w) => Remove(w);
            Preview.Editado += (s, e) => MarkDirty();
            _toastTimer.Tick += (s, e) => HideToast();
            PreviewKeyDown += View_PreviewKeyDown;
            Reload(null);
        }

        /// <summary>Raised after a profile is captured, saved, renamed or deleted (the module refreshes its tray menu from disk anyway).</summary>
        public event EventHandler ProfilesChanged;

        // ---------------------------------------------------------------- profiles

        public void Reload(string selectFile)
        {
            _ignoreSelection = true;
            _profiles.Clear();
            foreach (var p in ProfileStore.LoadAll()) _profiles.Add(p);
            _ignoreSelection = false;
            ProfileCount.Text = _profiles.Count == 0 ? "" : _profiles.Count.ToString(Strings.Culture);
            var target = _profiles.FirstOrDefault(p => string.Equals(p.Arquivo, selectFile, StringComparison.OrdinalIgnoreCase)) ?? _profiles.FirstOrDefault();
            if (target != null) ProfileList.SelectedItem = target;
            else Open(null);
        }

        void Open(Profile p)
        {
            _loading = true;
            _current = p;
            Empty.Visibility = p == null ? Visibility.Visible : Visibility.Collapsed;
            Editor.Visibility = p == null ? Visibility.Collapsed : Visibility.Visible;

            foreach (var w in _windows) w.PropertyChanged -= Window_PropertyChanged;
            _windows = new ObservableCollection<WindowEntry>(p?.Janelas.OrderBy(w => w.OrdemZ) ?? Enumerable.Empty<WindowEntry>());
            int i = 0;
            foreach (var w in _windows) { w.Indice = i++; w.PropertyChanged += Window_PropertyChanged; }
            _windows.CollectionChanged += (s, e) => UpdateCounts();
            WindowList.ItemsSource = _windows;

            var monitors = p == null ? null : (p.Monitores.Count > 0 ? p.Monitores : Engine.WindowList.Monitors());
            Preview.Carregar(monitors, _windows);
            NameBox.Text = p?.Nome ?? "";
            UpdateCounts();
            LoadIcons(_windows.ToList());
            _loading = false;
            SetDirty(false);
        }

        void UpdateCounts()
        {
            WindowCount.Text = _windows.Count.ToString(Strings.Culture);
            if (_current == null) { MetaText.Text = ""; return; }
            var when = _current.CapturadoEm.ToString("g", Strings.Culture);
            MetaText.Text = _current.Monitores.Count == 1
                ? Strings.Format("Meta.One", _windows.Count, when)
                : Strings.Format("Meta", _windows.Count, _current.Monitores.Count, when);
        }

        void Window_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_loading || e.PropertyName == nameof(WindowEntry.Icone) || e.PropertyName == nameof(WindowEntry.Resumo)) return;
            var w = (WindowEntry)sender;
            if (e.PropertyName == nameof(WindowEntry.Maximizada) && !w.Maximizada) FitWhenRestored(w);
            if (!w.Maximizada && (e.PropertyName == nameof(WindowEntry.X) || e.PropertyName == nameof(WindowEntry.Y) ||
                                  e.PropertyName == nameof(WindowEntry.Largura) || e.PropertyName == nameof(WindowEntry.Altura)))
            {
                var m = MonitorAtCenter(w);
                if (m != null && m.Dispositivo != w.MonitorDispositivo) LayoutPreview.AtribuirMonitor(w, m, false);
            }
            MarkDirty();
        }

        MonitorInfo MonitorAtCenter(WindowEntry w)
        {
            var monitors = _current?.Monitores;
            if (monitors == null || monitors.Count == 0) return null;
            int cx = w.X + w.Largura / 2, cy = w.Y + Math.Min(20, w.Altura / 2);
            return monitors.FirstOrDefault(m => m.Bounds.Contains(cx, cy));
        }

        // Unchecking "Maximized" on a window as big as its monitor gives it a sensible restored size.
        void FitWhenRestored(WindowEntry w)
        {
            var m = _current?.MonitorDe(w);
            if (m == null) return;
            if (w.Largura >= m.Work.W * 0.9 && w.Altura >= m.Work.H * 0.9)
            {
                int width = (int)(m.Work.W * 0.7), height = (int)(m.Work.H * 0.7);
                w.X = m.Work.X + (m.Work.W - width) / 2; w.Y = m.Work.Y + (m.Work.H - height) / 2; w.Largura = width; w.Altura = height;
            }
        }

        void MarkDirty() { if (!_loading && _current != null) SetDirty(true); }

        void SetDirty(bool value)
        {
            _dirty = value;
            var v = value ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Visibility = v;
            DiscardButton.Visibility = v;
            DirtyChip.Visibility = v;
        }

        bool Save()
        {
            if (_current == null) return true;
            try
            {
                _current.Nome = string.IsNullOrWhiteSpace(NameBox.Text) ? Strings.Get("Untitled") : NameBox.Text.Trim();
                _current.Janelas = _windows.ToList();
                ProfileStore.Save(_current);
                _current.Refresh();
                SetDirty(false);
                UpdateCounts();
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                ShowToast(Strings.Get("Toast.SaveFailed"), ex.Message, ToastKind.Error);
                return false;
            }
        }

        /// <summary>Asks about unsaved changes. False when the user cancelled.</summary>
        public bool ConfirmLeave()
        {
            if (!_dirty || _current == null) return true;
            var save = Strings.Get("Save");
            var discard = Strings.Get("Discard");
            var r = Dialog.Show(this, Strings.Get("Dialog.SaveTitle"), Strings.Format("Dialog.SaveText", _current.Nome),
                                new DialogButton(discard, DialogKind.Secondary), new DialogButton(save, DialogKind.Primary));
            if (r == save) return Save();
            if (r == discard) { DiscardCurrent(); return true; }
            return false;
        }

        void DiscardCurrent()
        {
            if (_current?.Arquivo == null) return;
            try
            {
                var fresh = ProfileStore.Load(_current.Arquivo);
                int idx = _profiles.IndexOf(_current);
                _ignoreSelection = true;
                if (idx >= 0) _profiles[idx] = fresh;
                ProfileList.SelectedItem = fresh;
                _ignoreSelection = false;
                Open(fresh);
            }
            catch { }
        }

        void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ignoreSelection) return;
            var next = ProfileList.SelectedItem as Profile;
            if (next == _current) return;
            if (!ConfirmLeave())
            {
                _ignoreSelection = true;
                ProfileList.SelectedItem = _current;
                _ignoreSelection = false;
                return;
            }
            // DiscardCurrent may have swapped the instance: read the selection again.
            next = ProfileList.SelectedItem as Profile ?? next;
            if (next != _current) Open(next);
        }

        void View_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { if (_dirty) Save(); e.Handled = true; }
        }

        void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _current == null) return;
            MarkDirty();
        }

        // ---------------------------------------------------------------- actions

        async void Capture_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmLeave()) return;
            CaptureButton.IsEnabled = false;
            try
            {
                var p = await OnSta(() => Capture.Take(DefaultName()));
                ProfileStore.Save(p);
                Reload(p.Arquivo);
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
                ShowToast(Strings.Get("Toast.Captured"), Strings.Format("Toast.CapturedText", p.Janelas.Count, p.Nome), ToastKind.Success);
                // The name is a placeholder: put the caret in it so renaming is one keystroke away.
                _ = Dispatcher.BeginInvoke(new Action(() => { NameBox.Focus(); NameBox.SelectAll(); }), DispatcherPriority.Input);
            }
            catch (Exception ex) { ShowToast(Strings.Get("Toast.CaptureFailed"), ex.Message, ToastKind.Error); }
            finally { CaptureButton.IsEnabled = true; }
        }

        /// <summary>"Layout 3" style name that is not taken yet.</summary>
        internal static string DefaultName(IEnumerable<string> taken = null)
        {
            var names = new HashSet<string>(taken ?? ProfileStore.LoadAll().Select(p => p.Nome), StringComparer.OrdinalIgnoreCase);
            for (int n = names.Count + 1; ; n++)
            {
                var candidate = "Layout " + n.ToString(Strings.Culture);
                if (!names.Contains(candidate)) return candidate;
            }
        }

        async void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var replace = Strings.Get("Dialog.Replace");
            var r = Dialog.Show(this, Strings.Get("Dialog.ReplaceTitle"), Strings.Format("Dialog.ReplaceText", _current.Nome),
                                new DialogButton(replace, DialogKind.Primary));
            if (r != replace) return;
            var fresh = await OnSta(() => Capture.Take(_current.Nome));
            var target = _current;
            target.Monitores = fresh.Monitores;
            target.Janelas = fresh.Janelas;
            target.CapturadoEm = fresh.CapturadoEm;
            Open(target);
            SetDirty(true);
            ShowToast(Strings.Get("Toast.Replaced"), Strings.Format("Toast.ReplacedText", fresh.Janelas.Count), ToastKind.Success);
        }

        void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || !ConfirmLeave()) return;
            try
            {
                var copy = ProfileStore.Load(_current.Arquivo);
                copy.Arquivo = null;
                copy.Nome = Strings.Format("Copy.Suffix", _current.Nome);
                copy.Carimbo = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                ProfileStore.Save(copy);
                Reload(copy.Arquivo);
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { ShowToast(Strings.Get("Toast.DuplicateFailed"), ex.Message, ToastKind.Error); }
        }

        void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;
            var delete = Strings.Get("Dialog.Delete");
            var r = Dialog.Show(this, Strings.Get("Dialog.DeleteTitle"), Strings.Format("Dialog.DeleteText", _current.Nome),
                                new DialogButton(delete, DialogKind.Danger));
            if (r != delete) return;
            try
            {
                ProfileStore.Delete(_current);
                _dirty = false;
                Reload(null);
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { ShowToast(Strings.Get("Toast.DeleteFailed"), ex.Message, ToastKind.Error); }
        }

        void Save_Click(object sender, RoutedEventArgs e)
        {
            if (Save()) ShowToast(Strings.Get("Toast.Saved"), _current.Nome, ToastKind.Success);
        }

        void Discard_Click(object sender, RoutedEventArgs e) => DiscardCurrent();

        void ApplyItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is Profile p)
            {
                if (p != _current) ProfileList.SelectedItem = p;
                if (ProfileList.SelectedItem == p) Apply_Click(sender, e);
            }
            e.Handled = true;
        }

        async void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _applying) return;
            _applying = true;
            ApplyButton.IsEnabled = false;
            ApplyText.Text = Strings.Get("Apply.Busy");
            try
            {
                // Applies what is on screen, unsaved edits included.
                var snapshot = new Profile
                {
                    Nome = _current.Nome, Monitores = _current.Monitores,
                    Janelas = _windows.Select((w, i) => { var c = w.Clone(); c.OrdemZ = i; return c; }).ToList(),
                };
                var r = await Restorer.RunAsync(snapshot);
                var parts = new List<string>();
                if (r.JaAbertas > 0) parts.Add(Strings.Format("Toast.AlreadyOpen", r.JaAbertas));
                if (r.Abertas > 0) parts.Add(Strings.Format("Toast.Opened", r.Abertas));
                var body = string.Join("  ·  ", parts);
                if (r.Avisos.Count > 0) body += (body.Length > 0 ? "\n" : "") + string.Join("\n", r.Avisos.Take(6));
                ShowToast(Strings.Format("Toast.Applied", r.Posicionadas, r.Total, r.Segundos), body,
                          r.Posicionadas == r.Total ? ToastKind.Success : ToastKind.Warning);
            }
            catch (Exception ex) { ShowToast(Strings.Get("Toast.ApplyFailed"), ex.Message, ToastKind.Error); }
            finally
            {
                _applying = false;
                ApplyButton.IsEnabled = true;
                ApplyText.Text = Strings.Get("Apply");
            }
        }

        void More_Click(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)FindResource("ProfileMenu");
            menu.PlacementTarget = MoreButton;
            menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = MoreButton.ActualWidth;
            menu.FlowDirection = FlowDirection.LeftToRight;
            menu.IsOpen = true;
        }

        void ProfileMenu_Opened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                item.IsEnabled = _current != null;
                if (Equals(item.Tag, "CopyCommand"))
                    item.Visibility = _options.CommandExecutable != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(ProfileStore.Pasta);
            Process.Start(new ProcessStartInfo(ProfileStore.Pasta) { UseShellExecute = true });
        }

        void ShowFile_Click(object sender, RoutedEventArgs e)
        {
            if (_current?.Arquivo == null) return;
            Process.Start("explorer.exe", $"/select,\"{_current.Arquivo}\"");
        }

        void CopyCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null || _options.CommandExecutable == null) return;
            try
            {
                Clipboard.SetText($"\"{_options.CommandExecutable}\" --apply \"{_current.Nome}\"");
                ShowToast(Strings.Get("Toast.CommandCopied"), Strings.Get("Toast.CommandCopiedText"), ToastKind.Success);
            }
            catch (COMException) { }   // clipboard held by another app
        }

        // ---------------------------------------------------------------- windows of the profile

        WindowEntry Selected => WindowList.SelectedItem as WindowEntry;

        void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e) => Preview.Selecionada = Selected;

        void WindowList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && !(e.OriginalSource is TextBox) && Selected != null) { Remove(Selected); e.Handled = true; }
        }

        void RemoveWindow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is WindowEntry w) Remove(w);
        }

        void RemoveSelected_Click(object sender, RoutedEventArgs e) { if (Selected != null) Remove(Selected); }

        void Remove(WindowEntry w)
        {
            int idx = _windows.IndexOf(w);
            if (idx < 0) return;
            w.PropertyChanged -= Window_PropertyChanged;
            _windows.RemoveAt(idx);
            if (_windows.Count > 0) WindowList.SelectedIndex = Math.Min(idx, _windows.Count - 1);
            MarkDirty();
        }

        void MoveTo(int to)
        {
            var w = Selected;
            if (w == null) return;
            int from = _windows.IndexOf(w);
            to = Math.Max(0, Math.Min(_windows.Count - 1, to));
            if (from == to) return;
            _windows.Move(from, to);
            WindowList.SelectedItem = w;
            WindowList.ScrollIntoView(w);
            Preview.InvalidateVisual();
            MarkDirty();
        }

        void Front_Click(object sender, RoutedEventArgs e) => MoveTo(0);
        void Back_Click(object sender, RoutedEventArgs e) => MoveTo(_windows.Count - 1);
        void Up_Click(object sender, RoutedEventArgs e) { if (Selected != null) MoveTo(_windows.IndexOf(Selected) - 1); }
        void Down_Click(object sender, RoutedEventArgs e) { if (Selected != null) MoveTo(_windows.IndexOf(Selected) + 1); }
        void ToggleMax_Click(object sender, RoutedEventArgs e) { if (Selected != null) Selected.Maximizada = !Selected.Maximizada; }

        void WindowMenu_Opened(object sender, RoutedEventArgs e)
        {
            foreach (var item in ((ContextMenu)sender).Items.OfType<MenuItem>()) item.IsEnabled = Selected != null;
        }

        // Copies position/size from a matching open window.
        void UseCurrent_Click(object sender, RoutedEventArgs e)
        {
            var w = Selected;
            if (w == null) return;
            var self = (uint)Process.GetCurrentProcess().Id;
            var live = Engine.WindowList.Visible(true).Where(v => v.Pid != self && v.Processo == w.Processo).ToList();
            var title = Engine.WindowList.NormalizeTitle(w.Titulo);
            var match = live.FirstOrDefault(v => Engine.WindowList.NormalizeTitle(v.Titulo) == title) ?? (live.Count == 1 ? live[0] : null);
            if (match == null)
            {
                ShowToast(Strings.Get("Toast.NotFound"),
                          live.Count > 1 ? Strings.Format("Toast.NotFoundMany", live.Count, w.Processo) : Strings.Format("Toast.NotRunning", w.Processo),
                          ToastKind.Warning);
                return;
            }
            var wp = new Native.WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(Native.WINDOWPLACEMENT)) };
            Native.GetWindowPlacement(match.Handle, ref wp);
            var mi = Native.MonitorInfo(Native.MonitorFromWindow(match.Handle, 2));
            var monitor = Engine.WindowList.Monitors().FirstOrDefault(m => m.Dispositivo == mi.szDevice);
            w.Maximizada = match.Maximizada;
            if (match.Minimizada || match.Maximizada)
            {
                // Restored position (rcNormalPosition is in workspace coordinates).
                var primary = Engine.WindowList.Monitors().FirstOrDefault(m => m.Principal);
                int ox = primary == null ? 0 : primary.Work.X - primary.Bounds.X, oy = primary == null ? 0 : primary.Work.Y - primary.Bounds.Y;
                w.X = wp.rcNormal.Left + ox; w.Y = wp.rcNormal.Top + oy;
                w.Largura = wp.rcNormal.Right - wp.rcNormal.Left; w.Altura = wp.rcNormal.Bottom - wp.rcNormal.Top;
            }
            else { w.X = match.Rect.X; w.Y = match.Rect.Y; w.Largura = match.Rect.W; w.Altura = match.Rect.H; }
            if (monitor != null) LayoutPreview.AtribuirMonitor(w, monitor, false);
            MarkDirty();
        }

        // ---------------------------------------------------------------- icons

        void LoadIcons(List<WindowEntry> windows)
        {
            var dispatcher = Dispatcher;
            Task.Run(() =>
            {
                foreach (var w in windows)
                {
                    var exe = w.Executavel;
                    if (string.IsNullOrEmpty(exe)) continue;
                    if (!Icons.TryGetValue(exe, out var img))
                    {
                        img = ExtractIcon(exe);
                        Icons[exe] = img;
                    }
                    if (img != null) dispatcher.BeginInvoke(new Action(() => w.Icone = img));
                }
            });
        }

        static ImageSource ExtractIcon(string exe)
        {
            try
            {
                if (!File.Exists(exe)) return null;
                using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (ico == null) return null;
                    var bs = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    bs.Freeze();
                    return bs;
                }
            }
            catch { return null; }
        }

        // ---------------------------------------------------------------- utilities

        internal static Task<T> OnSta<T>(Func<T> f)
        {
            var tcs = new TaskCompletionSource<T>();
            var t = new Thread(() => { try { tcs.SetResult(f()); } catch (Exception ex) { tcs.SetException(ex); } }) { IsBackground = true };
            t.SetApartmentState(ApartmentState.STA);   // Shell COM (Explorer folders)
            t.Start();
            return tcs.Task;
        }

        enum ToastKind { Success, Warning, Error }

        void ShowToast(string title, string body, ToastKind kind)
        {
            ToastTitle.Text = title;
            ToastBody.Text = body ?? "";
            ToastBody.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
            // Segoe Fluent Icons: CheckMark, Warning, StatusErrorFull
            ToastIcon.Text = char.ConvertFromUtf32(kind == ToastKind.Success ? 0xE73E : kind == ToastKind.Warning ? 0xE7BA : 0xEA39);
            ToastIcon.Foreground = (Brush)FindResource(kind == ToastKind.Success ? "Brush.Success" : kind == ToastKind.Warning ? "Brush.Warning" : "Brush.Danger");
            Toast.Visibility = Visibility.Visible;
            var slow = (Duration)FindResource("Motion.Slow");
            var ease = (IEasingFunction)FindResource("Ease.Out");
            Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, slow) { EasingFunction = ease });
            ToastShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, slow) { EasingFunction = ease });
            _toastTimer.Stop();
            _toastTimer.Interval = TimeSpan.FromSeconds(body != null && body.Contains("\n") ? 9 : 4.5);
            _toastTimer.Start();
        }

        void HideToast()
        {
            _toastTimer.Stop();
            var normal = (Duration)FindResource("Motion.Normal");
            var fade = new DoubleAnimation(0, normal);
            fade.Completed += (s, e) => { if (Toast.Opacity == 0) Toast.Visibility = Visibility.Collapsed; };
            Toast.BeginAnimation(OpacityProperty, fade);
            ToastShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, normal));
        }

        void Toast_Click(object sender, MouseButtonEventArgs e) => HideToast();
    }
}
