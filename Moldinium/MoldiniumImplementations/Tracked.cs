using Moldinium.Combined;

namespace Moldinium.MoldiniumImplementations;

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
    TrackableVariable<Value> variable;

    public void Init(Value def) => variable = new TrackableVariable<Value>(def);

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
    CachedBeforeAndAfterComputedTrackable<Value> trackable;

    public void Init() => trackable = new CachedBeforeAndAfterComputedTrackable<Value>();

    public bool BeforeGet(ref Value value, ref TrackedPropertyMixin mixin) => trackable.BeforeGet(ref value);

    public void AfterGet(ref Value value, ref TrackedPropertyMixin mixin) => trackable.AfterGet(ref value);

    public void AfterSet(ref TrackedPropertyMixin mixin) => trackable.AfterSet();

    public bool AfterErrorGet(Exception exception, ref TrackedPropertyMixin mixin) => trackable.AfterErrorGet(exception);

    public bool AfterErrorSet(ref TrackedPropertyMixin mixin)
    {
        trackable.AfterErrorSet();

        return true;
    }
}
