using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap98: Formats a numeric context-usage value (0–100) as a right-aligned, rounded-up whole
    /// percent string in the shape "%2d%%" (e.g. a fill of 44.2% renders as "45%", a single-digit
    /// 1% as " 1%", and 99.8% as "100%"). The integer is right-aligned to a field width of 2 and
    /// the "%" is a literal suffix. The value is first rounded up via ceiling, so 44.2% shows 45,
    /// never 44.
    /// </summary>
    public class PercentToWidthTwoConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return " 0%";
            }

            try
            {
                double numValue = System.Convert.ToDouble(value, culture);
                if (numValue <= 0.0)
                {
                    return " 0%";
                }

                // Clamp to 0–100, then round UP to a whole number.
                numValue = Math.Max(0, Math.Min(100, numValue));
                int whole = (int)Math.Ceiling(numValue);

                // "%2d%%": the integer is at least two chars wide, right-aligned, with a literal
                // percent suffix. "{0,2}%" → "45%" (2-digit), " 1%" (1-digit padded), "100%".
                return string.Format(CultureInfo.InvariantCulture, "{0,2}%", whole);
            }
            catch
            {
                return " 0%";
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }
}
