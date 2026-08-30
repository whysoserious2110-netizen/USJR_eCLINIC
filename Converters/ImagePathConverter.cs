using System.Globalization;

namespace USJR_eCLINIC.Converters;

public class ImagePathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = value as string;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        // Cache-bust: append last-write time so MAUI treats each save as a new image
        var lastWrite = File.GetLastWriteTimeUtc(path).Ticks;
        return ImageSource.FromStream(() => File.OpenRead(path));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}