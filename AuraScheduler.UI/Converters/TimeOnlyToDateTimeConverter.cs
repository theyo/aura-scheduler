using Microsoft.UI.Xaml.Data;

namespace AuraScheduler.UI
{
    // WinUI 3 TimePicker.SelectedTime uses TimeSpan?, not DateTime
    public class TimeOnlyToTimeSpanConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            try { return ((TimeOnly)value).ToTimeSpan(); }
            catch { return TimeSpan.Zero; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            try { return TimeOnly.FromTimeSpan((TimeSpan)value); }
            catch { return TimeOnly.MinValue; }
        }
    }
}
