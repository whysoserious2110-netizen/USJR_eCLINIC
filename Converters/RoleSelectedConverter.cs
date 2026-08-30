using System.Globalization;

namespace USJR_eCLINIC.Converters;

public class RoleSelectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = value as string;
        var thisRole = parameter as string;
        return selected == thisRole ? Color.FromArgb("#D5F5EF") : Colors.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class RoleSelectedTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = value as string;
        var thisRole = parameter as string;
        return selected == thisRole ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#12212E");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}