using System.Globalization;

namespace USJR_eCLINIC.Converters;

public class ChipBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
            return isSelected ? Color.FromArgb("#D5F5EF") : Colors.White;

        return IsMatchingService(value, parameter)
            ? Color.FromArgb("#F4C400")
            : Colors.White;
    }

    private static bool IsMatchingService(object value, object parameter) =>
        value is string selected && parameter is string expected &&
        string.Equals(selected, expected, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ChipBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
            return isSelected ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#E2E6EA");

        return IsMatchingService(value, parameter)
            ? Color.FromArgb("#087A3D")
            : Color.FromArgb("#DCE5DD");
    }

    private static bool IsMatchingService(object value, object parameter) =>
        value is string selected && parameter is string expected &&
        string.Equals(selected, expected, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ChipTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
            return isSelected ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#12212E");

        return IsMatchingService(value, parameter)
            ? Color.FromArgb("#087A3D")
            : Color.FromArgb("#526057");
    }

    private static bool IsMatchingService(object value, object parameter) =>
        value is string selected && parameter is string expected &&
        string.Equals(selected, expected, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    => !string.IsNullOrWhiteSpace(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
