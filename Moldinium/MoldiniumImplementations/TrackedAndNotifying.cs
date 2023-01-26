using System.ComponentModel;
using Moldinium.Combined;

namespace Moldinium.MoldiniumImplementations;

public struct TrackedNotifyingPropertyMixin : ITracked, INotifyingPropertyMixin
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

public interface ITrackedNotifyingPropertyImplementation<
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

public struct TrackedNotifyingPropertyImplementation<Value, Container> : ITrackedNotifyingPropertyImplementation<Value, Container, TrackedNotifyingPropertyMixin>
    where Container : class
{
    TrackableVariable<Value> variable;

    public void Init(Value def) => variable = new TrackableVariable<Value>(def);

    public Value Get() => variable.Value;

    public void Set(Value value, Container container, ref TrackedNotifyingPropertyMixin mixin)
    {
        variable.Value = value;

        mixin.NotifyPropertyChanged(container);
    }
}

public interface ITrackedNotifyingComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Container)] Container,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] Exception,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyWrapperImplementation
    where Container : class
    where Exception : System.Exception
{
    void Init();

    Boolean BeforeGet(ref Value value, ref Mixin mixin);

    void AfterGet(ref Value value, Container container, ref Mixin mixin);
    void AfterSet(ref Mixin mixin);

    Boolean AfterErrorGet(Exception exception, Container container, ref Mixin mixin);
    Boolean AfterErrorSet(ref Mixin mixin);
}

public struct TrackedNotifyingComputedPropertyImplementation<Value, Container, Exception>
    : ITrackedNotifyingComputedPropertyImplementation<Value, Container, Exception, TrackedNotifyingPropertyMixin>
    where Container : class
    where Exception : System.Exception
{
    CachedBeforeAndAfterComputedTrackable<Value> trackable;

    public void Init() => trackable = new CachedBeforeAndAfterComputedTrackable<Value>();

    public bool BeforeGet(ref Value value, ref TrackedNotifyingPropertyMixin mixin) => trackable.BeforeGet(ref value);

    public void AfterGet(ref Value value, Container container, ref TrackedNotifyingPropertyMixin mixin)
        => trackable.AfterGet(ref value, () => (container as INotifyingPropertyMixin)!.NotifyPropertyChanged(container));

    public void AfterSet(ref TrackedNotifyingPropertyMixin mixin) => trackable.AfterSet();

    public bool AfterErrorGet(Exception exception, Container container, ref TrackedNotifyingPropertyMixin mixin)
        => trackable.AfterErrorGet(exception, () => (container as INotifyingPropertyMixin)!.NotifyPropertyChanged(container));

    public bool AfterErrorSet(ref TrackedNotifyingPropertyMixin mixin)
    {
        trackable.AfterErrorSet();

        return true;
    }
}
