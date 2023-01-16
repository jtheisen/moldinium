using System;

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
    Var<Value> variable;

    public void Init(Value def) => variable.Value = def;

    public Value Get() => variable.Value;

    public void Set(Value value) => variable.Value = value;
}

public interface ITrackedComputedPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value
> : IPropertyImplementation
{
    void Init(Value def, Func<Value> nestedGetter, Action<Value> nestedSetter);
    Value Get();
    void Set(Value value, Action<Value> nestedGetter);
}

