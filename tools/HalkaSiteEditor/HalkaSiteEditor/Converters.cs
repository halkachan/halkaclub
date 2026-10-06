using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HalkaSiteEditor;

/// <summary>"#ffdc18" のような文字列を色見本の塗りに変えます。読めない色は斜線なしの灰色。</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Brush Unknown = new SolidColorBrush(Color.FromRgb(0x3A, 0x41, 0x4C));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch (FormatException) { }
        }
        return Unknown;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>中身があるときだけ表示します（入力の誤りメッセージ用）。</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>欄の種類が ConverterParameter と同じときだけ表示します。</summary>
public sealed class KindMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
        var wantMatch = !string.Equals(parameter?.ToString(), "!Boolean", StringComparison.Ordinal);
        if (!wantMatch) matches = !string.Equals(value?.ToString(), "Boolean", StringComparison.Ordinal);
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>変更があった欄だけ色を変えます。</summary>
public sealed class ChangedToBrushConverter : IValueConverter
{
    private static readonly Brush Changed = new SolidColorBrush(Color.FromRgb(0xFF, 0xCB, 0x75));
    private static readonly Brush Same = new SolidColorBrush(Color.FromRgb(0x58, 0x61, 0x70));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Changed : Same;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
