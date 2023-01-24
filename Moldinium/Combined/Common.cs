using Moldinium.Injection;
using System;
using System.Collections.Generic;

namespace Moldinium;

[Flags]
public enum DefaultDependencyProviderBakingMode
{
    Basic = 0,

    Tracking = 1,
    NotifyPropertyChanged = 2,

    TrackingAndNotifyPropertyChanged = 3
}

public class DependencyProviderBuilder
{
    ConcreteImplementationDependencyProvider implementations = new ConcreteImplementationDependencyProvider();
    ConcreteDependencyProvider concretes = new ConcreteDependencyProvider();

    List<IDependencyProvider> extraProviders = new List<IDependencyProvider>();

    public DependencyProviderBuilder AddInstance(Type type, Object instance) => concretes.AddInstance(type, instance).Return(this);
    public DependencyProviderBuilder Accept(Type type) => concretes.Accept(type).Return(this);

    public DependencyProviderBuilder AddImplementation(Type interfaceType, Type implementationType)
        => implementations.Add(interfaceType, implementationType).Return(this);

    public DependencyProviderBuilder Add(IDependencyProvider provider) { extraProviders.Add(provider); return this; }

    public IDependencyProvider Build()
    {
        var providers = new List<IDependencyProvider>();

        if (!implementations.IsEmpty)
        {
            providers.Add(implementations);
        }

        if (!concretes.IsEmpty)
        {
            providers.Add(concretes);
        }

        providers.AddRange(this.extraProviders);

        var combined = new CombinedDependencyProvider(providers.ToArray());

        return combined;
    }
}

public record DefaultDependencyProviderConfiguration(
    DefaultDependencyProviderBakingMode? Baking = DefaultDependencyProviderBakingMode.Basic,
    Boolean BakeAbstract = false,
    Boolean InitializeInits = true,
    Boolean EnableFactories = true,
    Boolean AcceptDefaultConstructibles = false,
    Action<DependencyProviderBuilder>? Build = null,
    IServiceProvider? Services = null,
    Predicate<Type>? IsMoldiniumType = null
);

public static class DependencyProvider
{
    public static IDependencyProvider Create(DefaultDependencyProviderConfiguration config)
    {
        var providers = new List<IDependencyProvider>();

        if (config.Build is Action<DependencyProviderBuilder> build)
        {
            var builder = new DependencyProviderBuilder();
            build(builder);
            providers.Add(builder.Build());
        }

        if (config.Services is IServiceProvider services)
        {
            providers.Add(new ServiceProviderDependencyProvider(services));
        }

        providers.Add(new AcceptRootTypeDependencyProvider());

        if (config.AcceptDefaultConstructibles)
        {
            providers.Add(new AcceptingDefaultConstructiblesDependencyProvider());
        }

        if (config.Baking is DefaultDependencyProviderBakingMode bakingMode)
        {
            var componentGenerators = CreateBakeryComponentGenerators(bakingMode);

            var bakeryconfiguration = new BakeryConfiguration(componentGenerators, Defaults.GetDefaultDefaultProvider(), config.BakeAbstract);

            providers.Add(new BakeryDependencyProvider(new Bakery("TestBakery", bakeryconfiguration), config.IsMoldiniumType));
        }

        if (config.EnableFactories)
        {
            providers.Add(new FactoryDependencyProvider());
        }

        providers.Add(new ActivatorDependencyProvider());
        
        if (config.InitializeInits)
        {
            providers.Add(new InitSetterDependencyProvider());
        }

        return new CombinedDependencyProvider(providers.ToArray());
    }

    static ComponentGenerators CreateBakeryComponentGenerators(DefaultDependencyProviderBakingMode mode) => mode switch
    {
        DefaultDependencyProviderBakingMode.Basic => ComponentGenerators.Create(
            typeof(SimplePropertyImplementation<>),
            typeof(GenericEventImplementation<>)),
        DefaultDependencyProviderBakingMode.Tracking => ComponentGenerators.Create(
            typeof(TrackedPropertyImplementation<>),
            typeof(TrackedComputedPropertyImplementation<,>)),
        DefaultDependencyProviderBakingMode.TrackingAndNotifyPropertyChanged => ComponentGenerators.Create(
            typeof(TrackedNotifyingPropertyImplementation<,>),
            typeof(TrackedNotifyingComputedPropertyImplementation<,,>)),
        _ => throw new NotImplementedException()
    };
}

public static partial class Extensions
{
    /// <summary>
    /// Checks whether <paramref name="name"/> starts with <paramref name="prefix"/> and that the
    /// first letter following that prefix is in upper case.
    /// Eg. <code>name.StartsWithFollowedByCapital("I")</code> detects interfaces
    /// or styleguide violations
    /// </summary>
    public static Boolean StartsWithFollowedByCapital(this String name, String prefix)
        => name.Length > prefix.Length && name.StartsWith(prefix) && Char.IsUpper(name[prefix.Length]);
}
