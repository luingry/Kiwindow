using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Kiwindow.Ui
{
    public sealed class FuncConverter : IValueConverter
    {
        readonly Func<object, object> _convert;

        public FuncConverter(Func<object, object> convert) { _convert = convert; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => _convert(value);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b ? !b : Binding.DoNothing;
    }

    public static class Converters
    {
        static Color ByIndex(object v) => LayoutPreview.Paleta[Math.Abs(v is int i ? i : 0) % LayoutPreview.Paleta.Length];

        /// <summary>Window index → its preview color.</summary>
        public static readonly IValueConverter Tint = new FuncConverter(v => Frozen(new SolidColorBrush(ByIndex(v))));

        /// <summary>Window index → a soft fill of its preview color, behind the app icon.</summary>
        public static readonly IValueConverter TintFill = new FuncConverter(v =>
        {
            var c = ByIndex(v);
            return Frozen(new SolidColorBrush(Color.FromArgb(0x2E, c.R, c.G, c.B)));
        });

        public static readonly IValueConverter Not = new FuncConverter(v => !(v is bool b && b));

        /// <summary>true → Visible, false → Hidden (keeps the slot so rows do not shift on hover).</summary>
        public static readonly IValueConverter ShowWhenTrue = new FuncConverter(v => v is bool b && b ? Visibility.Visible : Visibility.Hidden);

        static SolidColorBrush Frozen(SolidColorBrush b) { b.Freeze(); return b; }
    }
}
