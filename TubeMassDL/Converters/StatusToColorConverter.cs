using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NoCloudware.UI.Core.ViewModels;

namespace TubeMassDL.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
            return status switch
            {
                FileStatus.Queued => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88)),
                FileStatus.Processing => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xA8, 0xFF)),
                FileStatus.Processed => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)),
                FileStatus.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE9, 0x45, 0x60)),
                FileStatus.PendingManual => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xA7, 0x26)),
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88))
            };
        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
