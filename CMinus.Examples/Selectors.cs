using System;
using System.Collections.Generic;
using System.Linq;

namespace CMinus.Examples
{
    public interface SelectorRequirements<T>
    {
        IEnumerable<T> Items { get; }
    }

    public interface Selector<T> : SelectorRequirements<T>
    {
        T SelectedItem { get; set; }

        IEnumerable<SelectorItem<T>> SelectorItems { get; }

        SelectorItem<T> SelectedSelectorItem { get; set; }
    }

    public interface SelectorItem<T>
    {
        Boolean IsSelected { get; set; }

        T Value { get; }
    }

    public interface SelectorImplementation<T> : Implementation<Selector<T>>, Selector<T>, Requires<SelectorItem<T>, T>
    {
        new IEnumerable<SelectorItem<T>> SelectorItems => Items.Select(i => Resolve<SelectorItem<T>>(i));

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

    public interface SelectorItemImplementation<T> : Implementation<SelectorItem<T>>, SelectorItem<T>, Requires<Selector<T>>
    {
        new Boolean IsSelected
        {
            set
            {
                var selector = Resolve<Selector<T>>();

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
}
