using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ContinueVS.Core.Types;

namespace ContinueVS.UI.Converters
{
    /// <summary>
    /// IValueConverter that converts ContextBudgetState to SolidColorBrush for UI indicator.
    /// Safe → Green (#00AA00)
    /// Caution → Yellow (#FFAA00)
    /// Locked → Red (#FF0000)
    /// </summary>
    public class ContextBudgetStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ContextBudgetState state))
                return new SolidColorBrush(Colors.Gray);

            return state switch
            {
                ContextBudgetState.Safe => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00AA00")),
                ContextBudgetState.Caution => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA00")),
                ContextBudgetState.Locked => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0000")),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
