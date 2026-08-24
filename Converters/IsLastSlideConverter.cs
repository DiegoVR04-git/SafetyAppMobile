using System.Globalization;

namespace SafetyAppMobile.Converters;

/// <summary>
/// Converter that returns true if the slide title is "Lista para usarse" (the last slide).
/// </summary>
public class IsLastSlideConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string title)
        {
            return title == "Lista para usarse";
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
