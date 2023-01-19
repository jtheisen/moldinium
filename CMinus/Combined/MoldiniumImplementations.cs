using SimpleInjector;
using System;
using System.ComponentModel;

namespace CMinus;

public interface INotifyingPropertyMixin : INotifyPropertyChanged
{
    Int32 ListenerCount { get; set; }

    void NotifyPropertyChanged(Object o);
}

public struct NotifyingPropertyMixin : INotifyingPropertyMixin
{
    public Int32 ListenerCount { get; set; }

    event PropertyChangedEventHandler? backingPropertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            ListenerCount++;

            backingPropertyChanged += value;
        }
        remove
        {
            backingPropertyChanged -= value;

            ListenerCount--;
        }
    }

    public void NotifyPropertyChanged(object o) => backingPropertyChanged?.Invoke(o, new PropertyChangedEventArgs(""));
}

public interface ITracked { }

public struct TrackedPropertyMixin : ITracked { }

public interface ITrackedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
{
    void Init(Value def);

    Value Get();

    void Set(Value value);
}

public struct TrackedPropertyImplementation<Value> : ITrackedPropertyImplementation<Value, TrackedPropertyMixin>
{
    WatchableVariable<Value> variable;

    public void Init(Value def) => variable = new WatchableVariable<Value>(def);

    public Value Get() => variable.Value;

    public void Set(Value value) => variable.Value = value;
}

public interface ITrackedComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] Exception,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyWrapperImplementation
    where Exception : System.Exception
{
    void Init();

    Boolean BeforeGet(ref Value value, ref Mixin mixin);

    void AfterGet(ref Value value, ref Mixin mixin);
    void AfterSet(ref Mixin mixin);

    Boolean AfterErrorGet(Exception exception, ref Mixin mixin);
    Boolean AfterErrorSet(ref Mixin mixin);
}

public struct TrackedComputedPropertyImplementation<Value, Exception>
    : ITrackedComputedPropertyImplementation<Value, Exception, TrackedPropertyMixin>
    where Exception : System.Exception
{
    CachedBeforeAndAfterComputedWatchable<Value> watchable;

    public void Init() => watchable = new CachedBeforeAndAfterComputedWatchable<Value>();

    public bool BeforeGet(ref Value value, ref TrackedPropertyMixin mixin) => watchable.BeforeGet(ref value);

    public void AfterGet(ref Value value, ref TrackedPropertyMixin mixin) => watchable.AfterGet(ref value);

    public void AfterSet(ref TrackedPropertyMixin mixin) => watchable.AfterSet();

    public bool AfterErrorGet(Exception exception, ref TrackedPropertyMixin mixin) => watchable.AfterErrorGet(exception);

    public bool AfterErrorSet(ref TrackedPropertyMixin mixin)
    {
        watchable.AfterErrorSet();

        return true;
    }
}

public struct TrackedNotifyingPropertyMixin : ITracked, INotifyingPropertyMixin
{
    public Int32 ListenerCount { get; set; }

    event PropertyChangedEventHandler? backingPropertyChanged;

    public event PropertyChangedEventHandler? PropertyChanged
    {
        add
        {
            ListenerCount++;

            backingPropertyChanged += value;
        }
        remove
        {
            backingPropertyChanged -= value;

            ListenerCount--;
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
    WatchableVariable<Value> variable;

    public void Init(Value def) => variable = new WatchableVariable<Value>(def);

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
    where Container : class, INotifyingPropertyMixin
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
    where Container : class, INotifyingPropertyMixin
    where Exception : System.Exception
{
    CachedBeforeAndAfterComputedWatchable<Value> watchable;

    public void Init() => watchable = new CachedBeforeAndAfterComputedWatchable<Value>();

    public bool BeforeGet(ref Value value, ref TrackedNotifyingPropertyMixin mixin) => watchable.BeforeGet(ref value);

    public void AfterGet(ref Value value, Container container, ref TrackedNotifyingPropertyMixin mixin)
        => watchable.AfterGet(ref value, () => container.NotifyPropertyChanged(container));

    public void AfterSet(ref TrackedNotifyingPropertyMixin mixin) => watchable.AfterSet();

    public bool AfterErrorGet(Exception exception, Container container, ref TrackedNotifyingPropertyMixin mixin)
        => watchable.AfterErrorGet(exception, () => container.NotifyPropertyChanged(container));

    public bool AfterErrorSet(ref TrackedNotifyingPropertyMixin mixin)
    {
        watchable.AfterErrorSet();

        return true;
    }
}

public interface IScopedMethodImplementation : IImplementation
{
    Boolean Before();

    void After();

    Boolean AfterError();
}

public struct ScopedMethodImplementation : IScopedMethodImplementation
{
    // Tracking needs to implement defering scopes

    public Boolean Before() => true;

    public void After() { }

    public Boolean AfterError() => true;
}
