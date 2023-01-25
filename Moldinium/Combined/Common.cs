using Microsoft.Extensions.DependencyInjection;
using Moldinium.Injection;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Moldinium;

[Flags]
public enum MoldiniumDefaultMode
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

public class MoldiniumConfigurationBuilder
{
    MoldiniumDefaultMode mode;
    Type? defaultIListAndICollationType;
    Predicate<Type>? isModliniumType;

    public MoldiniumConfigurationBuilder SetMode(MoldiniumDefaultMode mode)
        => Modify(() => this.mode = mode);

    public MoldiniumConfigurationBuilder SetIListAndICollectionImplementationType(Type defaultIListAndICollationType)
        => Modify(() => this.defaultIListAndICollationType = defaultIListAndICollationType);

    public MoldiniumConfigurationBuilder IdentifyMoldiniumTypes(Predicate<Type> isModliniumType)
        => Modify(() => this.isModliniumType = isModliniumType);

    MoldiniumConfigurationBuilder Modify(Action action)
    {
        action();
        return this;
    }

    public DefaultDependencyProviderConfiguration Build(IServiceProvider services)
        => new DefaultDependencyProviderConfiguration(mode,
            DefaultIListAndICollationType: defaultIListAndICollationType,
            IsMoldiniumType: isModliniumType,
            Services: services
        );
}

public record DefaultDependencyProviderConfiguration(
    MoldiniumDefaultMode? Mode = MoldiniumDefaultMode.Basic,
    Boolean BakeAbstract = false,
    Boolean InitializeInits = true,
    Boolean EnableFactories = true,
    Boolean AcceptDefaultConstructibles = false,
    Type? DefaultIListAndICollationType = null,
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

        if (config.Mode is MoldiniumDefaultMode mode)
        {
            var componentGenerators = CreateBakeryComponentGenerators(mode);

            String moduleNameSuffix = "";

            Type? genericCollectionType;

            if (config.DefaultIListAndICollationType is not null)
            {
                moduleNameSuffix = $".{config.DefaultIListAndICollationType.Name}";
                genericCollectionType = config.DefaultIListAndICollationType;
            }
            else
            {
                genericCollectionType = GetDefaultGenericCollectionType(mode);
            }

            var defaultProvider = Defaults.GetDefaultDefaultProvider(genericCollectionType);

            var bakeryconfiguration = new BakeryConfiguration(componentGenerators, defaultProvider, config.BakeAbstract);

            var bakery = new Bakery($"MoldiniumTypes.{mode}{moduleNameSuffix}", bakeryconfiguration);

            providers.Add(new BakeryDependencyProvider(bakery, config.IsMoldiniumType));
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

    static Type GetDefaultGenericCollectionType(MoldiniumDefaultMode mode) => mode switch
    {
        MoldiniumDefaultMode.Basic => typeof(List<>),
        MoldiniumDefaultMode.NotifyPropertyChanged => typeof(LiveList<>), // just because ObservableCollection doesn't implement IList<>
        MoldiniumDefaultMode.Tracking or MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged => typeof(TrackableList<>),
        _ => throw new NotImplementedException()
    };

    static ComponentGenerators CreateBakeryComponentGenerators(MoldiniumDefaultMode mode) => mode switch
    {
        MoldiniumDefaultMode.Basic => ComponentGenerators.Create(
            typeof(SimplePropertyImplementation<>),
            typeof(GenericEventImplementation<>)),
        MoldiniumDefaultMode.Tracking => ComponentGenerators.Create(
            typeof(TrackedPropertyImplementation<>),
            typeof(TrackedComputedPropertyImplementation<,>)),
        MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged => ComponentGenerators.Create(
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
    /// or styleguide violations such as <see cref="ILGenerator"/>
    /// </summary>
    public static Boolean StartsWithFollowedByCapital(this String name, String prefix)
        => name.Length > prefix.Length && name.StartsWith(prefix) && Char.IsUpper(name[prefix.Length]);

    static IDependencyProvider GetProvider(this Action<MoldiniumConfigurationBuilder> build, IServiceProvider services)
    {
        var builder = new MoldiniumConfigurationBuilder();
        build(builder);
        var configuration = builder.Build(services);
        return DependencyProvider.Create(configuration);
    }

    /// <summary>
    /// Adds the type <typeparamref name="T"/> as a resolvable type. It also adds <see cref="Scope{T}"/> as a singleton
    /// that can be resolved for early validation and to get the resolution report.
    /// </summary>
    public static IServiceCollection AddSingletonMoldiniumRoot<T>(this IServiceCollection services, Action<MoldiniumConfigurationBuilder> build)
        where T : class => services
        
        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.InitializedInstance))
        .AddSingleton<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)
        
    ;

    /// <summary>
    /// Adds the type <typeparamref name="T"/> as a resolvable type. It also adds <see cref="Scope{T}"/> as a singleton
    /// that can be resolved for early validation and to get the resolution report.
    /// </summary>
    public static IServiceCollection AddScopedMoldiniumRoot<T>(this IServiceCollection services, Action<MoldiniumConfigurationBuilder> build)
        where T : class => services

        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.InitializedInstance))
        .AddScoped<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)

    ;

    /// <summary>
    /// Adds the type <typeparamref name="T"/> as a resolvable type. It also adds <see cref="Scope{T}"/> as a singleton
    /// that can be resolved for early validation and to get the resolution report.
    /// </summary>
    public static IServiceCollection AddTransientMoldiniumRoot<T>(this IServiceCollection services, Action<MoldiniumConfigurationBuilder> build)
        where T : class => services

        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.InitializedInstance))
        .AddTransient<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)

    ;

    /// <summary>
    /// Validates dependencies early and gives a dependency report
    /// </summary>
    public static String ValidateMoldiniumRoot<T>(this IServiceProvider services)
        => services.GetRequiredService<Scope<T>>().CreateDependencyReport();
}
