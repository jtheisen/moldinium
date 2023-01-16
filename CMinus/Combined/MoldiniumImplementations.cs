using System;
using System.Diagnostics.CodeAnalysis;

namespace CMinus;

public interface ITracked { }

public struct TrackedPropertyMixin : ITracked { }

public interface ITrackedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
>
{
    void Init(Value def);

    Value Get();

    void Set(Value value);
}

public struct TrackedPropertyImplementation<Value> : ITrackedPropertyImplementation<Value, TrackedPropertyMixin>
{
    WatchableVariable<Value> variable;

    public void Init(Value def) => new WatchableVariable<Value>(def);

    public Value Get() => variable.Value;

    public void Set(Value value) => variable.Value = value;
}

public interface ITrackedComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Exception)] Exception,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
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

    public void Init()
    {
        watchable = new CachedBeforeAndAfterComputedWatchable<Value>();
    }

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
