﻿using CMinus.Construction;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Injection;

/*
 * There are various IDependencyProviders with different demands and properties
 * 
 * 1. The Bakery
 * 
 * Requires only types (not instances) as dependencies to keep the door open for assembly weaving.
 * It also only provides types, not instances, but the provided types are default activatable.
 * 
 * 2. IServiceProvider
 * 
 * Requires nothing as dependencies as it is only used as a source for types. This is good, as
 * it can't tell you what resolved type you get for a service type until you request an instance.
 * 
 * 3. SimpleInjector
 * 
 * Could in principle be used as both a source and a sink as it allows resolving types without any instances.
 * Whether this can be used as a helper for the implementation remains an open question.
 * 
 * 4. Parent scope
 * 
 * Not sure what to say here.
 */

/*
 * Various steps of construction can also be separated into different providers:
 * 
 * 1. Baking interfaces to classes
 * 2. Activation (Construction)
 * 3. Setting initializer props
 * 
 */

public record Dependency(Type Type, DependencyRuntimeMaturity Maturity);

public enum DependencyRuntimeMaturity
{
    // There's only the type yet and no instance is available
    OnlyType,

    // We have an uninitialized instance
    UntouchedInstance,

    // We're done
    InitSettersSetInstance,

    Finished = InitSettersSetInstance
}

public class DependencyBag
{
    public static DependencyBag Empty = new DependencyBag(Enumerable.Empty<Dependency>());

    IImmutableSet<Dependency> dependencies;

    public IImmutableSet<Dependency> Items => dependencies;

    public DependencyBag(IImmutableSet<Dependency> dependencies)
        => this.dependencies = ImmutableHashSet.CreateRange(dependencies);

    public DependencyBag(IEnumerable<Dependency> dependencies)
        => this.dependencies = ImmutableHashSet.CreateRange(dependencies);

    public DependencyBag(Dependency dependency)
        => dependencies = ImmutableHashSet.Create(dependency);

    public DependencyBag Concat(DependencyBag rhs)
        => new DependencyBag(dependencies.Union(rhs.dependencies));
}

public delegate Object InstanceGetter(RuntimeScope scope);

public record DependencyResolution(
    IDependencyProvider Provider,
    Dependency Dep,
    Dependency? SameInstanceDependency,
    DependencyBag Dependencies,
    InstanceGetter Get
);

public interface IDependencyProvider
{
    DependencyResolution? Query(Dependency type);
}

public class BakeryDependencyProvider : IDependencyProvider
{
    private readonly Bakery bakery;

    public BakeryDependencyProvider(Bakery bakery)
    {
        this.bakery = bakery;
    }

    public DependencyResolution? Query(Dependency dep)
    {
        if (!dep.Type.IsInterface) return null;

        var bakedType = bakery.Resolve(dep.Type);

        var bakedInstanceDependency = new Dependency(bakedType, DependencyRuntimeMaturity.Finished);

        return new DependencyResolution(
            this,
            dep,
            new Dependency(bakedType, DependencyRuntimeMaturity.Finished),
            DependencyBag.Empty,
            Get: scope => scope.Get(bakedInstanceDependency)
        );
    }
}

public class ServiceProviderDependencyProvider : IDependencyProvider
{
    private readonly IServiceProvider provider;

    public ServiceProviderDependencyProvider(IServiceProvider provider)
    {
        this.provider = provider;
    }

    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.UntouchedInstance) return null;

        var service = provider.GetService(dep.Type);

        if (service is null) return null;

        return new DependencyResolution(this, dep, null, DependencyBag.Empty, Get: _ => service);
    }
}

public class AcceptingDefaultConstructiblesDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.OnlyType) return null;

        var reflection = Reflection.Get(dep.Type);

        if (!reflection.IsDefaultConstructible) return null;

        return new DependencyResolution(
            this,
            dep,
            null,
            DependencyBag.Empty,
            Get: _ => throw new InvalidOperationException("There is no instance")
        );
    }
}

public class ActivatorDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.UntouchedInstance) return null;

        var reflection = Reflection.Get(dep.Type);

        if (!reflection.IsDefaultConstructible) return null;

        return new DependencyResolution(
            this,
            dep,
            dep with { Maturity = DependencyRuntimeMaturity.OnlyType },
            DependencyBag.Empty,
            Get: _ => Activator.CreateInstance(dep.Type) ?? throw new Exception($"Activator failed to provide a type")
        );
    }
}

public class InitSetterDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.InitSettersSetInstance) return null;

        var reflection = Reflection.Get(dep.Type);

        var dependencies =
            from p in reflection.Properties
            where p.hasInitSetter
            select new Dependency(p.info.PropertyType, DependencyRuntimeMaturity.Finished);

        var embryoDependency = dep with { Maturity = DependencyRuntimeMaturity.UntouchedInstance };

        return new DependencyResolution(
            this,
            dep,
            embryoDependency,
            new DependencyBag(dependencies),
            Get: scope =>
            {
                var embryo = scope.Get(embryoDependency);

                foreach (var prop in reflection.Properties)
                {
                    if (!prop.hasInitSetter) continue;

                    prop.info.SetValue(embryo, scope.Get(new Dependency(prop.info.PropertyType, DependencyRuntimeMaturity.Finished)));
                }

                return embryo;
            });
    }
}

//public class FactoryDependencyProvider : IDependencyProvider
//{
//    public DependencyResolution? Query(Dependency dep)
//    {
//        if (dep.Type.BaseType != typeof(Delegate)) return null;

//        var method = dep.Type.GetMethod("Invoke");

//        if (method is null) throw new Exception("Could not get method for delegate");

//        var parameters = method.GetParameters();

//        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

//        /* Either
//         * 
//         * - pass the scope to Query so we can recursively create subscopes
//         * - add a new method GetStatic besides Get to resolve scoped dependencies here
//         * 
//         */

//        Object Get(RuntimeScope runtimeScope)
//        {
//            var scope = runtimeScope.Scope;

//            Object Create(Object[] args)
//            {

//            }

//        }


//        return new DependencyResolution(dep, Get: s => DelegateCreation.CreateDelegate(dep, Create));
//    }
//}

public class ConcreteDependencyProvider : IDependencyProvider
{
    Dictionary<Dependency, Object?> dependencies;

    public ConcreteDependencyProvider(params (Dependency dependency, Object? instance)[] dependencies)
        => this.dependencies = dependencies.ToDictionary(p => p.dependency, p => p.instance);

    public ConcreteDependencyProvider(params Dependency[] dependencies)
        => this.dependencies = dependencies.ToDictionary(p => p, p => null as Object);

    public ConcreteDependencyProvider(params Type[] dependencies)
        => this.dependencies = dependencies.ToDictionary(t => new Dependency(t, DependencyRuntimeMaturity.OnlyType), t => null as Object);

    public DependencyResolution? Query(Dependency type)
    {
        if (dependencies.TryGetValue(type, out var instance))
        {
            return new DependencyResolution(
                this,
                type,
                null,
                DependencyBag.Empty,
                Get: _ => instance ?? throw new InvalidOperationException("There is no instance")
            );
        }
        else
        {
            return null;
        }
    }
}

public class CombinedDependencyProvider : IDependencyProvider
{
    private readonly IDependencyProvider[] providers;

    public CombinedDependencyProvider(params IDependencyProvider[] providers)
    {
        this.providers = providers;
    }

    public DependencyResolution? Query(Dependency dependency)
    {
        foreach (var provider in providers)
        {
            var resolution = provider.Query(dependency);

            if (resolution is not null) return resolution;
        }

        return null;
    }
}

public class Scope<T> : Scope
{
    public Scope(IDependencyProvider provider, DependencyRuntimeMaturity maturity)
        : base(provider, new Dependency(typeof(T), maturity))
    {
    }

    public T InstantiateRootType() => new RuntimeScope<T>(this).Root;
}

public class Scope
{
    private readonly IDependencyProvider provider;

    Dependency root;

    Queue<Dependency> pending = new Queue<Dependency>();

    Dictionary<Dependency, DependencyResolution> resolutions = new Dictionary<Dependency, DependencyResolution>();

    Dictionary<Dependency, DependencyResolution> sources = new Dictionary<Dependency, DependencyResolution>();

    public Dependency Root => root;

    public DependencyResolution GetResolution(Dependency dep) => resolutions[dep];

    public Scope(IDependencyProvider provider, Dependency root)
    {
        this.provider = provider;
        this.root = root;

        pending.Enqueue(root);

        Resolve();
    }

    public RuntimeScope Instantiate()
    {
        return  new RuntimeScope(this);
    }

    public Dependency GetFinalSameInstanceDependency(Dependency dependency)
    {
        while (resolutions.TryGetValue(dependency, out var resolution) && resolution.SameInstanceDependency is Dependency sameInstanceDependency)
        {
            dependency = sameInstanceDependency;
        }

        return dependency;
    }

    void Resolve()
    {
        while (pending.Count > 0)
        {
            var next = pending.Dequeue();

            var resolution = provider.Query(next);

            if (resolution is null) throw new Exception($"Can't resolve dependency {next}\n\n{CreateDependencyIssueReport(next)}");

            resolutions.Add(next, resolution);

            if (resolution.SameInstanceDependency is Dependency sameInstanceDependency)
            {
                AddDependency(sameInstanceDependency, resolution);
            }

            foreach (var dep in resolution.Dependencies.Items)
            {
                AddDependency(dep, resolution);
            }
        }
    }

    void AddDependency(Dependency dep, DependencyResolution source)
    {
        if (!resolutions.ContainsKey(dep))
        {
            sources.Add(dep, source);

            pending.Enqueue(dep);
        }
    }

    String CreateDependencyIssueReport(Dependency dependency)
    {
        var writer = new StringWriter();

        while (true)
        {
            var resolution = sources.GetValueOrDefault(dependency);

            if (resolution is null && dependency != root) throw new Exception($"Unexpectedly can't trace path to root dependency");

            if (resolution is null)
            {
                writer.WriteLine($"{dependency} was the root");

                break;
            }

            writer.WriteLine($"{dependency} required by {resolution.Dep} provided by {resolution.Provider.GetType().Name}");

            dependency = resolution.Dep;
        }

        return writer.ToString();
    }
}

public class RuntimeScope
{
    Scope scope;

    Dictionary<Dependency, Object> instances = new Dictionary<Dependency, Object>();

    Object root;

    public Scope Scope => Scope;

    public Object Root => root;

    public RuntimeScope(Scope scope)
    {
        this.scope = scope;

        var resolution = scope.GetResolution(scope.Root);

        root = resolution.Get(this);
    }

    public Object Get(Dependency dependency)
    {
        if (!instances.TryGetValue(dependency, out var instance))
        {
            var resolution = scope.GetResolution(dependency);

            instances[dependency] = instance = resolution.Get(this);
        }

        return instance;
    }
}

public class RuntimeScope<T> : RuntimeScope
{
    public new T Root => (T)base.Root;

    public RuntimeScope(Scope scope)
        : base(scope)
    {
    }
}

public static class Extensions
{
    public static RuntimeScope CreateRuntimeScope(this Scope scope)
        => new RuntimeScope(scope);

    public static IDependencyProvider AcceptDefaultConstructibles(this IDependencyProvider provider)
        => new CombinedDependencyProvider(provider, new AcceptingDefaultConstructiblesDependencyProvider());

    public static Type ResolveType(this IDependencyProvider provider, Type type)
    {
        var root = new Dependency(type, DependencyRuntimeMaturity.OnlyType);

        var scope = new Scope(provider.AcceptDefaultConstructibles(), root);

        var final = scope.GetFinalSameInstanceDependency(root);

        return final.Type;
    }

    public static Object CreateInstance(this IDependencyProvider provider, Type type, DependencyRuntimeMaturity maturity = DependencyRuntimeMaturity.InitSettersSetInstance)
        => new Scope(provider, new Dependency(type, maturity)).CreateRuntimeScope().Root;

    public static T CreateInstance<T>(this IDependencyProvider provider, DependencyRuntimeMaturity maturity = DependencyRuntimeMaturity.InitSettersSetInstance) where T : class
        => (T)provider.CreateInstance(typeof(T), maturity);
}