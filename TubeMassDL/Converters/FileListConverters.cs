using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NoCloudware.UI.Core.ViewModels;

namespace TubeMassDL.Converters;

public class IconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name && name.Length > 0)
        {
            if (char.IsSurrogatePair(name, 0) && name.Length >= 2)
                return name.Substring(0, 2);
            return name[0].ToString();
        }
        return "🎬";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ExtensionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(path);
                return string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.');
            }
            catch { }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long size && size > 0)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            double dsize = size;
            while (dsize >= 1024 && unit < units.Length - 1)
            {
                dsize /= 1024;
                unit++;
            }
            return $"{dsize:0.##} {units[unit]}";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusDotColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
            return status switch
            {
                FileStatus.Processed => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)), // green
                FileStatus.Queued or FileStatus.PendingManual => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xA7, 0x26)), // yellow
                FileStatus.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60)), // red
                FileStatus.Processing => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xA8, 0xFF)), // blue
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88))
            };
        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PlaylistCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is BaseFileItem item)
        {
            if (item.SourceText == "📁 Playlist")
            {
                // Extract count from FileName: "📁 Title (15)" → "15"
                var name = item.FileName;
                var start = name.LastIndexOf('(');
                var end = name.LastIndexOf(')');
                if (start >= 0 && end > start)
                    return name.Substring(start + 1, end - start - 1);
                return "";
            }
            // Regular item: show extension
            try
            {
                var ext = System.IO.Path.GetExtension(item.FilePath);
                return string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.');
            }
            catch { }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DeleteCommandConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
