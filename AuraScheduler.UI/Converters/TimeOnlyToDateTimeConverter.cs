using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AuraScheduler.UI
{
    [ValueConversion(typeof(TimeOnly), typeof(DateTime))]
    public class TimeOnlyToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var now = DateTime.Now;

            try
            {
                return new DateTime(now.Year, now.Month, now.Day) + ((TimeOnly)value).ToTimeSpan();
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return TimeOnly.FromDateTime((DateTime)value);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
