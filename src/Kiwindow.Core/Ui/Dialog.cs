using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Kiwindow.Ui
{
    public enum DialogKind { Primary, Danger, Secondary }

    public sealed class DialogButton
    {
        public DialogButton(string text, DialogKind kind) { Text = text; Kind = kind; }

        public string Text { get; }

        public DialogKind Kind { get; }
    }

    /// <summary>
    /// Modal confirmation drawn with the design system: a borderless card over the owner window.
    /// Buttons are given in reading order; the first is the default (Enter), Esc or a click outside cancels.
    /// Returns the clicked button's text, or null when cancelled.
    /// </summary>
    public static class Dialog
    {
        public static string Show(DependencyObject anchor, string title, string message, params DialogButton[] buttons)
        {
            var owner = anchor as Window ?? Window.GetWindow(anchor);
            string choice = null;

            var w = new Window
            {
                Owner = owner,
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                UseLayoutRounding = true,
            };
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);

            var card = new Border
            {
                Background = Res<Brush>(w, "Brush.Surface2"),
                CornerRadius = Res<CornerRadius>(w, "Radius.Xl"),
                Padding = new Thickness(28, 24, 24, 20),
                Margin = new Thickness(24),     // room for the shadow
                Width = 420,
                Effect = w.TryFindResource("Elevation.Overlay") as System.Windows.Media.Effects.Effect,
                Opacity = 0,
                RenderTransform = new TranslateTransform(0, 8),
            };
            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = title, Style = Res<Style>(w, "Text.Title"), Margin = new Thickness(0, 0, 0, 8) });
            root.Children.Add(new TextBlock { Text = message, Style = Res<Style>(w, "Text.Paragraph") });

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 24, 0, 0) };
            foreach (var spec in buttons)
            {
                var b = new Button
                {
                    Content = spec.Text,
                    Margin = new Thickness(8, 0, 0, 0),
                    MinWidth = 96,
                    Style = Res<Style>(w, spec.Kind == DialogKind.Primary ? "Button.Primary" : spec.Kind == DialogKind.Danger ? "Button.Danger" : "Button.Secondary"),
                };
                if (spec == buttons[0]) b.IsDefault = true;
                b.Click += (s, e) => { choice = spec.Text; w.Close(); };
                row.Children.Add(b);
            }
            var cancel = new Button { Content = Strings.Get("Dialog.Cancel"), Margin = new Thickness(8, 0, 0, 0), MinWidth = 96, IsCancel = true, Style = Res<Style>(w, "Button.Ghost") };
            cancel.Click += (s, e) => w.Close();
            row.Children.Insert(0, cancel);
            root.Children.Add(row);
            card.Child = root;
            w.Content = card;

            w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.MouseLeftButtonDown += (s, e) => { if (e.OriginalSource == w) w.Close(); };
            w.Loaded += (s, e) =>
            {
                var fast = Res<Duration>(w, "Motion.Slow", new Duration(TimeSpan.FromMilliseconds(280)));
                var ease = w.TryFindResource("Ease.Out") as IEasingFunction;
                card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, fast) { EasingFunction = ease });
                card.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, fast) { EasingFunction = ease });
                (row.Children[row.Children.Count - 1] as Button)?.Focus();
            };

            // Dim the owner while the dialog is up.
            var scrim = owner?.Content as UIElement;
            double previous = scrim?.Opacity ?? 1;
            if (scrim != null) scrim.Opacity = 0.55;
            try { w.ShowDialog(); }
            finally { if (scrim != null) scrim.Opacity = previous; }
            return choice;
        }

        static T Res<T>(FrameworkElement scope, string key, T fallback = default)
        {
            var value = scope.TryFindResource(key);
            if (value is T t) return t;
            if (typeof(T) == typeof(Duration) && value is TimeSpan ts) return (T)(object)new Duration(ts);
            return fallback;
        }
    }
}
