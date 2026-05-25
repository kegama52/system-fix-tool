using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TreasuryFixTool.Diagnostics;

namespace TreasuryFixTool.Controls
{
    public class CheckStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CheckStatus status)
            {
                return status switch
                {
                    CheckStatus.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")),
                    CheckStatus.Critical => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")),
                    CheckStatus.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FD7E14")),
                    CheckStatus.Info => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0097A7")),
                    CheckStatus.Ok => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    CheckStatus.Healthy => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"))
                };
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}