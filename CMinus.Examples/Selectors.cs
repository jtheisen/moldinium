using System;
using System.Collections.Generic;
using System.Linq;

namespace CMinus.Examples.Selectors;

public interface Selector<T>
{
    IEnumerable<T> ItemsSource { get; init; }

    T SelectedItem { get; set; }

    IEnumerable<SelectorItem<T>> SelectorItems { get; }

    SelectorItem<T> SelectedSelectorItem { get; set; }
}

public interface SelectorItem<T>
{
    Boolean IsSelected { get; set; }

    T Value { get; }
}

public interface SelectorImplementation<T> : Selector<T>
{
    Func<T, SelectorItem<T>> CreateItem { get; init; }

    new IEnumerable<SelectorItem<T>> SelectorItems => ItemsSource.Select(i => CreateItem(i));

    new T SelectedItem { set => SelectedSelectorItem = SelectorItems.Single(i => i.Value.Equals(value)); }

    new SelectorItem<T> SelectedSelectorItem
    {
        set
        {
            var selector = this as Selector<T>;

            foreach (var item in SelectorItems)
            {
                if (item.Equals(value)) continue;

                item.IsSelected = false;
            }

            if (value != null)
            {
                value.IsSelected = true;

                selector.SelectedItem = value.Value;
            }
            else
            {
                selector.SelectedItem = default;
            }
        }
    }
}

public interface SelectorItemImplementation<T> : SelectorItem<T>
{
    Selector<T> Selector { get; init; }

    new Boolean IsSelected
    {
        set
        {
            var selector = Selector;

            if (value)
            {
                selector.SelectedSelectorItem = this;
            }
            else if (this.Equals(selector.SelectedSelectorItem))
            {
                selector.SelectedSelectorItem = null;
            }
        }
    }
}
