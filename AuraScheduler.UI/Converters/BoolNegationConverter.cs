using Microsoft.UI.Xaml.Data;

namespace AuraScheduler.UI
{
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => !(value is bool val && val);

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => !(value is bool val && val);
    }
}
