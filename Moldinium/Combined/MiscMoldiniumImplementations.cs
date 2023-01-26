using Moldinium.Baking;
using Moldinium.Common.Defaulting;
using Moldinium.Injection;
using Moldinium.Tracking;

namespace Moldinium.Internals;

public interface IScopedMethodImplementation : IImplementation
{
    bool Before();

    void After();

    bool AfterError();
}

public struct ScopedMethodImplementation : IScopedMethodImplementation
{
    // Tracking needs to implement defering scopes

    public bool Before() => true;

    public void After() { }

    public bool AfterError() => true;
}

public struct DefaultTrackableList<T> : IDefault<ICollection<T>>
{
    public ICollection<T> Default => new TrackableList<T>();
}

public class BakeryDependencyProvider : IDependencyProvider
{
    private readonly AbstractlyBakery bakery;
    private readonly Predicate<Type>? predicate;

    public BakeryDependencyProvider(AbstractlyBakery bakery, Predicate<Type>? predicate = null)
    {
        this.bakery = bakery;
        this.predicate = predicate;
    }

    public DependencyResolution? Query(Dependency dep)
    {
        if (!dep.Type.IsInterface) return null;

        if (!predicate?.Invoke(dep.Type) ?? false) return null;

        var bakedType = bakery.Resolve(dep.Type);

        var bakedInstanceDependency = dep with { Type = bakedType };

        return new DependencyResolution(
            this,
            dep,
            bakedInstanceDependency,
            DependencyBag.Empty,
            Get: scope => scope.Get(bakedInstanceDependency)
        );
    }
}
