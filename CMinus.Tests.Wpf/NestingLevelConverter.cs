using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace CMinus.Tests.Wpf
{
    public class NestingLevelConverter : IValueConverter
    {
        public Int32 DefaultMargin { get; set; }
        public Double Offset { get; set; }
        public Double Factor { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Int32 nestingLevel)
            {
                return new Thickness(Offset + Factor * nestingLevel, 0, 0, 0);
            }
            else
            {
                return DefaultMargin;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
