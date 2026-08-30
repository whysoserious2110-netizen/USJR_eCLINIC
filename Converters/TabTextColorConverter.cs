using System.Globalization;

namespace USJR_eCLINIC.Converters;

public class TabTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#9AA4AE");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}