using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SampleApp.Wpf
{
    public class NestingLevelConverter : IValueConverter
    {
        public Int32 DefaultMargin { get; set; }
        public Double Offset { get; set; }
        public Double Factor { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var level = value is JobNestingLevel nestingLevel ? nestingLevel.Level : 0;

            return new Thickness(Offset + Factor * level, 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
