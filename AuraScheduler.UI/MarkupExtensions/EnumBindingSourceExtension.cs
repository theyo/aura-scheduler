using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

using AuraScheduler.Worker;

namespace AuraScheduler.UI
{
    public class EnumBindingSourceExtension : MarkupExtension
    {
        public Type EnumType { get; private set; }

        public EnumBindingSourceExtension(Type enumType)
        {
            ArgumentNullException.ThrowIfNull(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException("EnumType must of type enum");

            EnumType = enumType;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {

            return Enum.GetValues(EnumType).Cast<LightMode>();
        }
    }
}
