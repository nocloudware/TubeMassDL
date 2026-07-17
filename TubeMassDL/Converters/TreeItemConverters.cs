using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VM = NoCloudware.UI.Core.ViewModels;

namespace TubeMassDL.Converters;

public class TreeItemIconVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NoCloudware.UI.Core.ViewModels.BaseFileItem item && item.SourceText == "📁 Playlist")
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DeleteIconVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NoCloudware.UI.Core.ViewModels.BaseFileItem item && item.SourceText != "📁 Playlist")
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TreeItemIconForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VM.BaseFileItem item && item.SourceText == "📁 Playlist")
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class TreeItemCommandConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VM.BaseFileItem item)
        {
            if (item.SourceText == "📁 Playlist")
                return item.TogglePlaylistCommand ?? (object)item.DeleteCommand!;
            return item.DeleteCommand ?? (object)"";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}