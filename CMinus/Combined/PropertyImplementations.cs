using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static CMinus.MethodCreation;

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

public struct TrackedComputedPropertyImplementation<Value> : ITrackedComputedPropertyImplementation<Value>
{
    CachedComputedWatchable<Value>? watchable;

    public void Init(Value def, Func<Value> nestedGetter, Action<Value> nestedSetter)
    {
        watchable = new CachedComputedWatchable<Value>(nestedGetter);

        nestedSetter(def);
    }

    public Value Get() => watchable!.Value;

    public void Set(Value value, Action<Value> nestedGetter)
    {
        nestedGetter(value);

        watchable!.InvalidateAndNotify();
    }
}
