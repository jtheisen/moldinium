using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using Moldinium.Delegates;
using Moldinium.Misc;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Intrinsics.Arm;
using static System.Net.Mime.MediaTypeNames;

namespace Moldinium.Injection;

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

public enum RuntimeResolutionType
{
    Missing,
    Instance,
    TypeResolved
}

public struct RuntimeResolution
{
    public static RuntimeResolution Missing = new RuntimeResolution(RuntimeResolutionType.Missing);
    public static RuntimeResolution TypeResolved = new RuntimeResolution(RuntimeResolutionType.TypeResolved);

    public RuntimeResolutionType Type { get; }
    public Object? Instance { get; }

    public RuntimeResolution(Object instance)
    {
        Type = RuntimeResolutionType.Instance;
        Instance = instance;
    }

    public RuntimeResolution(RuntimeResolutionType type)
    {
        Type = type;
        Instance = null;
    }
}

public record Dependency(Type Type, DependencyRuntimeMaturity Maturity, Boolean IsOptional, Boolean IsRootDependency)
{
    public override string ToString()
    {
        return $"{Char.ToLower(Maturity.ToString()[0])}{(IsOptional ? 'o' : 'r')}`{Type.Name}";
    }
}

public static class FundamentallyOutlawedTypes
{
    static Type[] outlawedTypes = new[] { typeof(byte), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(long), typeof(ulong), typeof(Guid) };

    public static Boolean IsOutlawed(Type type)
    {
        if (type == typeof(String))
        {
            return true;
        }
        else if (!type.IsValueType)
        {
            return false;
        }
        else if (outlawedTypes.Contains(type))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

public enum DependencyRuntimeMaturity
{
    // There's only the type yet and no instance is available
    TypeOnly,

    // We have an uninitialized instance
    UntouchedInstance,

    // We're done
    InitializedInstance,
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

public delegate Scope SubscopeCreator(Scope scope);

public delegate RuntimeResolution InstanceGetter(RuntimeScope scope);

public record DependencyResolution(
    IDependencyProvider Provider,
    Dependency Dep,
    Dependency? SameInstanceDependency = null,
    DependencyBag? Dependencies = null,
    SubscopeCreator? MakeSubscope = null,
    InstanceGetter? Get = null
);

public interface IDependencyProvider
{
    DependencyResolution? Query(Dependency dep);
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

public class OldMoldiniumModelDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.UntouchedInstance) return null;

        var type = dep.Type;

        if (!type.IsClass || !type.IsAbstract) return null;

        return new DependencyResolution(
            this,
            dep,
            Get: scope => new RuntimeResolution(Models.Create(type))
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

        return new DependencyResolution(this, dep, null, DependencyBag.Empty, Get: _ => new RuntimeResolution(service));
    }
}

public class AcceptingDefaultConstructiblesDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.TypeOnly) return null;

        var traits = TypeTraits.Get(dep.Type);

        if (!traits.IsDefaultConstructible) return null;

        return new DependencyResolution(
            this,
            dep,
            null,
            DependencyBag.Empty,
            Get: _ => throw new InvalidOperationException("There is no instance")
        );
    }
}

public class AcceptRootTypeDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.IsRootDependency && dep.Maturity == DependencyRuntimeMaturity.TypeOnly)
        {
            return new DependencyResolution(this, dep);
        }
        else
        {
            return null;
        }
    }
}

public class ActivatorDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.UntouchedInstance) return null;

        var traits = TypeTraits.Get(dep.Type);

        if (!traits.IsDefaultConstructible) return null;

        var nestedDependency = dep with { Maturity = DependencyRuntimeMaturity.TypeOnly };

        RuntimeResolution Get(RuntimeScope scope)
        {
            var typeOnlyResolution = scope.Get(nestedDependency!);

            if (typeOnlyResolution.Type == RuntimeResolutionType.TypeResolved)
            {
                var obj = Activator.CreateInstance(dep.Type) ?? throw new Exception($"Activator failed to provide an instance");

                return new RuntimeResolution(obj);
            }
            else if (dep.IsOptional)
            {
                return RuntimeResolution.Missing;
            }
            else
            {
                throw new InternalErrorException($"Dependency was not optional");
            }
        }

        return new DependencyResolution(
            this,
            dep,
            nestedDependency,
            DependencyBag.Empty,
            Get: Get
        );
    }
}

// FIXME: this provider often attempts to resolve types that it can't handle when the real dependency is missing
// - this then gives a confusing error
public class InitSetterDependencyProvider : IDependencyProvider
{
    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Maturity != DependencyRuntimeMaturity.InitializedInstance) return null;

        var reflection = TypeProperties.Get(dep.Type);

        var dependencies = (
            from p in reflection.Properties
            where p.hasInitSetter
            select new Dependency(p.info.PropertyType, DependencyRuntimeMaturity.InitializedInstance, !p.isNotNullable, false)
        ).ToArray();

        var embryoDependency = dep with { Maturity = DependencyRuntimeMaturity.UntouchedInstance };

        return new DependencyResolution(
            this,
            dep,
            embryoDependency,
            new DependencyBag(dependencies),
            Get: scope =>
            {
                var embryo = scope.Get(embryoDependency);

                if (embryo.Instance is null)
                {
                    return dep.IsOptional ? RuntimeResolution.Missing : throw new InternalErrorException($"Got null for an embryo");
                }

                foreach (var p in reflection.Properties)
                {
                    if (!p.hasInitSetter) continue;

                    var runtimeResolution = scope.Get(new Dependency(p.info.PropertyType, DependencyRuntimeMaturity.InitializedInstance, !p.requiresDefault, false));

                    var instance = runtimeResolution.Instance;

                    if (instance is not null)
                    {
                        p.info.SetValue(embryo.Instance, instance);
                    }
                    else if (p.requiresDefault)
                    {
                        throw new InternalErrorException("Should have an instance");
                    }
                }

                return embryo;
            });
    }
}

public class FactoryDependencyProvider : IDependencyProvider
{
    class FactoryArgumentsDependencyProvider : IDependencyProvider
    {
        HashSet<Dependency> dependencies;

        public Dictionary<Dependency, Object>? Arguments { get; set; }

        public FactoryArgumentsDependencyProvider(Type[] types)
        {
            dependencies = types.Select(t => new Dependency(t, DependencyRuntimeMaturity.InitializedInstance, false, false))
                .ToHashSet();
        }

        public DependencyResolution? Query(Dependency dependency)
        {
            var lookupDependency = dependency with { IsOptional = false };

            if (!dependencies.Contains(lookupDependency)) return null;

            return new DependencyResolution(
                this,
                dependency,
                Get: _ =>
                {
                    if (Arguments is null) throw new Exception($"{nameof(FactoryArgumentsDependencyProvider)} was not provided with arguments yet");

                    var argument = Arguments[lookupDependency];

                    return argument is not null ? new RuntimeResolution(argument) : RuntimeResolution.Missing;
                }
            );
        }
    }

    public DependencyResolution? Query(Dependency dep)
    {
        if (dep.Type.BaseType != typeof(Delegate) && dep.Type.BaseType != typeof(MulticastDelegate)) return null;

        var method = dep.Type.GetMethod("Invoke");

        if (method is null) throw new Exception("Could not get method for delegate");

        if (method.ReturnType is not Type returnType) return null;

        var parameters = method.GetParameters();

        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        var factoryArgumentsProvider = new FactoryArgumentsDependencyProvider(parameterTypes);

        var subScopeDependency = new Dependency(returnType, DependencyRuntimeMaturity.InitializedInstance, false, true);

        Scope MakeSubscope(Scope parent)
        {
            if (factoryArgumentsProvider is null) throw new Exception($"Internal error in {nameof(FactoryDependencyProvider)}");

            var provider = new CombinedDependencyProvider(
                factoryArgumentsProvider,
                parent.Provider
            );

            var subScope = new Scope(provider, subScopeDependency);

            return subScope;
        }

        RuntimeResolution Get(RuntimeScope runtimeScope)
        {
            var scope = runtimeScope.Scope;

            Scope subScope = scope.GetSubscope(dep);

            Object Create(Object[] args)
            {
                factoryArgumentsProvider.Arguments = parameterTypes
                    .Select((type, i) => (type, instance: args[i]))
                    .ToDictionary(d => new Dependency(d.type, DependencyRuntimeMaturity.InitializedInstance, false, false), d => d.instance);

                var runtimeScope = subScope.CreateRuntimeScope();

                return runtimeScope.Root;
            }

            var dlg = DelegateCreation.CreateDelegate(dep.Type, Create);

            return new RuntimeResolution(dlg);
        }

        return new DependencyResolution(this, dep, MakeSubscope: MakeSubscope, Get: Get);
    }
}

public class ConcreteImplementationDependencyProvider : IDependencyProvider
{
    Dictionary<Type, Type> implementations = new Dictionary<Type, Type>();

    Boolean isClosed;

    public Boolean IsEmpty => implementations.Count == 0;

    public ConcreteImplementationDependencyProvider Add(Type interfaceType, Type implementationType)
    {
        if (isClosed) throw new Exception($"{nameof(ConcreteImplementationDependencyProvider)} has already been used and can't be modified");

        implementations.Add(interfaceType, implementationType);

        return this;
    }

    public DependencyResolution? Query(Dependency dep)
    {
        if (implementations.TryGetValue(dep.Type, out var implementationType))
        {
            var implementation = dep with { Type = implementationType, Maturity = dep.Maturity };

            return new DependencyResolution(this, dep, implementation, Get: scope => scope.Get(implementation));
        }
        else
        {
            return null;
        }
    }
}

public class ConcreteDependencyProvider : IDependencyProvider
{
    Dictionary<Dependency, DependencyResolution> dependencies = new Dictionary<Dependency, DependencyResolution>();

    Boolean isClosed;

    public ConcreteDependencyProvider() { }

    public ConcreteDependencyProvider(params (Type type, Object instance)[] dependencies)
        => dependencies.ToList().ForEach(p => AddInstance(p.type, p.instance));

    public ConcreteDependencyProvider(params Type[] dependencies)
        => dependencies.ToList().ForEach(t => Accept(t));

    public Boolean IsEmpty => dependencies.Count == 0;

    public ConcreteDependencyProvider AddInstance(Type type, Object instance) => Add(type, DependencyRuntimeMaturity.InitializedInstance, _ => new RuntimeResolution(instance));
    public ConcreteDependencyProvider Accept(Type type) => Add(type, DependencyRuntimeMaturity.TypeOnly, null);

    public ConcreteDependencyProvider Add(Type type, DependencyRuntimeMaturity maturity, InstanceGetter? get)
    {
        if (isClosed) throw new Exception($"{nameof(ConcreteDependencyProvider)} has already been used and can't be modified");

        Add(type, maturity, get, true);
        Add(type, maturity, get, false);

        return this;
    }

    void Add(Type type, DependencyRuntimeMaturity maturity, InstanceGetter? get, Boolean isOptional)
    {
        var dep = new Dependency(type, maturity, isOptional, false);
        dependencies.Add(dep, new DependencyResolution(this, dep, Get: get));
    }

    public DependencyResolution? Query(Dependency dep)
    {
        isClosed = true;

        if (dependencies.TryGetValue(dep, out var resolution))
        {
            return resolution;
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
        : base(provider, new Dependency(typeof(T), maturity, false, true))
    {
    }

    public T InstantiateRootType() => new RuntimeScope<T>(this).Root;
}

public class Scope
{
    readonly IDependencyProvider provider;

    readonly Dependency root;

    readonly Queue<Dependency> pending = new Queue<Dependency>();

    readonly Dictionary<Dependency, DependencyResolution> resolutions = new Dictionary<Dependency, DependencyResolution>();

    readonly Dictionary<Dependency, DependencyResolution> sources = new Dictionary<Dependency, DependencyResolution>();

    readonly Dictionary<Dependency, Scope> subscopes = new Dictionary<Dependency, Scope>();

    readonly Queue<DependencyResolution> pendingSubscopes = new Queue<DependencyResolution>();

    public Dependency Root => root;

    public IDependencyProvider Provider => provider;

    public DependencyResolution? GetResolutionOrNull(Dependency dep) => resolutions.GetValueOrDefault(dep);

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

    public Scope GetSubscope(Dependency dependency) => subscopes[dependency];

    void Resolve()
    {
        while (pending.Count > 0)
        {
            var next = pending.Dequeue();

            HandleNext(next);
        }

        ResolveSubscopes();
    }

    Boolean HandleNext(Dependency next)
    {
        DependencyResolution? resolution = null;

        try
        {
            resolution = provider.Query(next);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Exception during dependency resolution for {next} on provider {provider.GetType().Name}\n\n"
                + CreateDependencyIssueReport(next), ex
            );
        }

        if (resolution is null)
        {
            if (next.IsOptional) return false;

            throw new Exception($"Can't resolve dependency {next}\n\n{CreateDependencyIssueReport(next)}");
        }

        resolutions.Add(next, resolution);

        if (resolution.SameInstanceDependency is Dependency sameInstanceDependency)
        {
            // We're handling same-instance-dependencies before all others to skip
            // enlisting the other dependencies when there's ultimately no instance

            AddDependency(sameInstanceDependency, resolution, dontEnqueue: true);

            if (!HandleNext(sameInstanceDependency)) return false;
        }

        if (resolution.Dependencies is DependencyBag dependencies)
        {
            foreach (var dep in dependencies.Items)
            {
                if (FundamentallyOutlawedTypes.IsOutlawed(dep.Type))
                {
                    throw new Exception(
                        $"{resolution.Provider.GetName()} request dependency {dep} for {next}," +
                        $" which has an outlawed type\n\n{CreateDependencyIssueReport(next)}");
                }

                AddDependency(dep, resolution);
            }
        }

        if (resolution.MakeSubscope is not null)
        {
            pendingSubscopes.Enqueue(resolution);
        }

        return true;
    }

    void ResolveSubscopes()
    {
        while (pendingSubscopes.Count > 0)
        {
            var next = pendingSubscopes.Dequeue();

            var subScope = next.MakeSubscope!(this);

            subscopes.Add(next.Dep, subScope);
        }
    }

    void AddDependency(Dependency dep, DependencyResolution source, Boolean dontEnqueue = false)
    {
        if (!resolutions.ContainsKey(dep))
        {
            sources.Add(dep, source);

            if (!dontEnqueue)
            {
                pending.Enqueue(dep);
            }
        }
    }

    public String CreateDependencyReport()
    {
        var reportWriter = new ReportWriter();
        reportWriter.WriteDependencyReport(this, root, 0);
        return reportWriter.GetReport();
    }

    public class ReportWriter
    {
        StringWriter writer = new StringWriter();

        public String GetReport() => writer.ToString();

        public void WriteDependencyReport(Scope scope, Dependency dependency, Int32 nesting = 0)
        {
            var reported = new HashSet<Dependency>();

            WriteDependencyReportForDependencies(scope, dependency, reported, nesting);

            foreach (var (subScopeDependency, subScope) in scope.subscopes)
            {
                AddNesting(nesting);
                writer.WriteLine($"\n- subscope for {subScopeDependency}");
                WriteDependencyReport(subScope, subScope.root, nesting + 1);
            }
        }

        void AddNesting(Int32 nesting)
        {
            writer.Write(new String(' ', nesting * 2));
        }

        void WriteDependencyReportForDependencies(Scope scope, Dependency dependency, HashSet<Dependency> reported, Int32 nesting = 0)
        {
            if (reported.Contains(dependency))
            {
                AddNesting(nesting);
                writer.Write($"- see above for {dependency}");

                return;
            }

            reported.Add(dependency);

            if (scope.resolutions.TryGetValue(dependency, out var resolution))
            {
                AddNesting(nesting);
                writer.WriteLine($"- {resolution.Provider.GetName()} resolved {dependency}");

                if (resolution.SameInstanceDependency is Dependency sameInstanceDependency)
                {
                    WriteDependencyReportForDependencies(scope, sameInstanceDependency, reported, nesting + 1);
                }

                if (resolution.Dependencies is DependencyBag bag)
                {
                    foreach (var nestedDependency in bag.Items)
                    {
                        WriteDependencyReportForDependencies(scope, nestedDependency, reported, nesting + 1);
                    }
                }
            }
            else
            {
                AddNesting(nesting);
                writer.Write($"- unresolved {dependency}");
            }
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

            writer.WriteLine($"{resolution.Provider.GetType().Name} provided {dependency} required by {resolution.Dep}");

            dependency = resolution.Dep;
        }

        return writer.ToString();
    }
}

public class RuntimeScope
{
    Scope scope;

    Dictionary<Dependency, RuntimeResolution> instances = new Dictionary<Dependency, RuntimeResolution>();

    Object root;

    public Scope Scope => scope;

    public Object Root => root;

    public RuntimeScope(Scope scope)
    {
        this.scope = scope;

        var resolution = scope.GetResolutionOrNull(scope.Root);

        if (resolution?.Get is null) throw new Exception($"Runtime scope can't be created for root without a resolution with a {nameof(DependencyResolution.Get)} method");

        var rootResolution = resolution.Get(this);

        root = rootResolution.Instance ?? throw new InternalErrorException("Internal error: Got null for root instance");
    }

    public RuntimeResolution Get(Dependency dependency)
    {
        if (!instances.TryGetValue(dependency, out var instance))
        {
            var resolution = scope.GetResolutionOrNull(dependency);

            if (resolution is null)
            {
                instances[dependency] = instance = RuntimeResolution.Missing;
            }
            else if (resolution?.Get is null)
            {
                instances[dependency] = instance =
                    dependency.Maturity != DependencyRuntimeMaturity.TypeOnly ? RuntimeResolution.Missing : RuntimeResolution.TypeResolved;
            }
            else
            {
                instances[dependency] = instance = resolution.Get(this);
            }

            if (dependency.Maturity != DependencyRuntimeMaturity.TypeOnly && !dependency.IsOptional && instance.Instance is null)
            {
                throw new InternalErrorException("Got no instance for optional dependency");
            }
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
        var root = new Dependency(type, DependencyRuntimeMaturity.TypeOnly, true, true);

        var scope = new Scope(provider.AcceptDefaultConstructibles(), root);

        var final = scope.GetFinalSameInstanceDependency(root);

        return final.Type;
    }

    public static String GetName(this IDependencyProvider provider)
    {
        var name = provider.GetType().Name;

        if (name.EndsWith("DependencyProvider"))
        {
            name = name[..(name.Length - "DependencyProvider".Length)];
        }

        return name;
    }

    public static IDependencyProvider Prepend(this IDependencyProvider provider, IDependencyProvider other)
        => new CombinedDependencyProvider(other, provider);

    public static String CreateReport(this IDependencyProvider provider, Type type, DependencyRuntimeMaturity maturity = DependencyRuntimeMaturity.InitializedInstance)
        => new Scope(provider, new Dependency(type, maturity, false, true)).CreateDependencyReport();

    public static Object CreateInstance(this IDependencyProvider provider, Type type, DependencyRuntimeMaturity maturity = DependencyRuntimeMaturity.InitializedInstance)
        => new Scope(provider, new Dependency(type, maturity, false, true)).CreateRuntimeScope().Root;

    public static T CreateInstance<T>(this IDependencyProvider provider, DependencyRuntimeMaturity maturity = DependencyRuntimeMaturity.InitializedInstance)
        => (T)provider.CreateInstance(typeof(T), maturity);
}
