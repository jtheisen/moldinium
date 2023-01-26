using Moldinium.Baking;
using System.ComponentModel;

namespace Moldinium.Internals;

public struct NotifyingPropertyMixin : INotifyingPropertyMixin
{
    PropertyChangedEventHandler? backingPropertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            backingPropertyChanged += value;
        }
        remove
        {
            backingPropertyChanged -= value;
        }
    }

    public void NotifyPropertyChanged(object o) => backingPropertyChanged?.Invoke(o, new PropertyChangedEventArgs(""));
}

public interface INotifyingPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Container)] Container,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
    where Container : class
{
    void Init(Value def);

    Value Get();

    void Set(Value value, Container container, ref Mixin mixin);
}

public struct NotifyingPropertyImplementation<Value, Container> : INotifyingPropertyImplementation<Value, Container, NotifyingPropertyMixin>
    where Container : class
{
    Value value;

    public void Init(Value def) => value = def;

    public Value Get() => value;

    public void Set(Value value, Container container, ref NotifyingPropertyMixin mixin)
    {
        this.value = value;
        mixin.NotifyPropertyChanged(container);
    }
}

public interface INotifyingComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Container)] Container,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyWrapperImplementation
    where Container : class
{
    Boolean BeforeGet();

    void AfterSet(Container container, ref Mixin mixin);
    Boolean AfterErrorSet();
}

public struct NotifyingComputedPropertyImplementation<Value, Container>
    : INotifyingComputedPropertyImplementation<Value, Container, NotifyingPropertyMixin>
    where Container : class
{
    public Boolean BeforeGet() => true;

    public void AfterSet(Container container, ref NotifyingPropertyMixin mixin)
    {
        mixin.NotifyPropertyChanged(container);
    }

    public Boolean AfterErrorSet() => true;
}
