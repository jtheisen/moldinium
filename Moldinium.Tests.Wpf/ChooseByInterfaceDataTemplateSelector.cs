using Moldinium.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Moldinium.Tests.Wpf
{
    [ContentProperty("DataTemplates")]
    public class ChooseByInterfaceDataTemplateSelector : DataTemplateSelector
    {
        public List<DataTemplate> DataTemplates { get; set; } = new List<DataTemplate>();

        public override DataTemplate? SelectTemplate(Object? item, DependencyObject container)
        {
            if (item is null) return null;

            var type = item.GetType();

            var candidates = DataTemplates.Where(c => c.DataType is Type t && t.IsAssignableFrom(type)).ToArray();

            return candidates.SingleOrDefault();
        }
    }
}

