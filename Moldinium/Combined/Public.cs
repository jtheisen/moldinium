using Microsoft.Extensions.DependencyInjection;
using Moldinium.Common.Misc;
using Moldinium.Injection;
using Moldinium.Internals;

namespace Moldinium;

[Flags]
public enum MoldiniumDefaultMode
{
    Basic = 0,

    Tracking = 1,
    NotifyPropertyChanged = 2,

    TrackingAndNotifyPropertyChanged = 3
}

public static class MoldiniumServices
{
    public static IServiceProvider Create<T>(Action<MoldiniumConfigurationBuilder> build, Action<IServiceCollection> services)
        where T : class
    {
        var collection = new ServiceCollection();
        if (services is not null) services(collection);
        collection.AddSingletonMoldiniumRoot<T>(build);
        var serviceProvider = collection.BuildServiceProvider();
        serviceProvider.ValidateMoldiniumRoot<T>();
        return serviceProvider;
    }
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

    static IDependencyProvider GetProvider(this Action<MoldiniumConfigurationBuilder> build, IServiceProvider? services = null)
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

        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.FinishedInstance))
        .AddSingleton<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)

    ;

    /// <summary>
    /// Adds the type <typeparamref name="T"/> as a resolvable type. It also adds <see cref="Scope{T}"/> as a singleton
    /// that can be resolved for early validation and to get the resolution report.
    /// </summary>
    public static IServiceCollection AddScopedMoldiniumRoot<T>(this IServiceCollection services, Action<MoldiniumConfigurationBuilder> build)
        where T : class => services

        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.FinishedInstance))
        .AddScoped<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)

    ;

    /// <summary>
    /// Adds the type <typeparamref name="T"/> as a resolvable type. It also adds <see cref="Scope{T}"/> as a singleton
    /// that can be resolved for early validation and to get the resolution report.
    /// </summary>
    public static IServiceCollection AddTransientMoldiniumRoot<T>(this IServiceCollection services, Action<MoldiniumConfigurationBuilder> build)
        where T : class => services

        .AddSingleton<Scope<T>>(sp => new Scope<T>(build.GetProvider(sp), DependencyRuntimeMaturity.FinishedInstance))
        .AddTransient<T>(sp => (T)sp.GetRequiredService<Scope<T>>().CreateRuntimeScope().Root)

    ;

    /// <summary>
    /// Validates dependencies early
    /// </summary>
    public static void ValidateMoldiniumRoot<T>(this IServiceProvider services)
        => services.GetRequiredService<Scope<T>>();

    /// <summary>
    /// Validates dependencies early and gives a dependency report
    /// </summary>
    public static void ValidateMoldiniumRoot<T>(this IServiceProvider services, out String dependencyReport)
        => dependencyReport = services.GetRequiredService<Scope<T>>().DependencyReport;
}

public class MoldiniumConfigurationBuilder
{
    List<IServiceProvider> serviceProviders = new List<IServiceProvider>();
    MoldiniumDefaultMode mode;
    (Type, Type)? DefaultIListAndICollectionTypes;
    Predicate<Type>? isModliniumType;

    public MoldiniumConfigurationBuilder SetMode(MoldiniumDefaultMode mode)
        => Modify(() => this.mode = mode);

    public MoldiniumConfigurationBuilder SetDefaultIListAndICollectionTypes(Type iListType, Type iCollectionType)
    {
        if (!TypeInterfaces.Get(iListType).DoesTypeImplement(typeof(IList<>))) throw new Exception($"{iListType} does not implement IList<>");
        if (!TypeInterfaces.Get(iCollectionType).DoesTypeImplement(typeof(ICollection<>))) throw new Exception($"{iListType} does not implement IList<>");
        DefaultIListAndICollectionTypes = (iListType, iCollectionType);
        return this;
    }

    public MoldiniumConfigurationBuilder SetDefaultIListAndICollectionType(Type iListType)
        => SetDefaultIListAndICollectionTypes(iListType, iListType);

    public MoldiniumConfigurationBuilder IdentifyMoldiniumTypes(Predicate<Type> isModliniumType)
        => Modify(() => this.isModliniumType = isModliniumType);

    public MoldiniumConfigurationBuilder AddServices(IServiceProvider services)
        => Modify(() => this.serviceProviders.Add(services));

    public MoldiniumConfigurationBuilder AddServices(Action<IServiceCollection> services)
        => Modify(() => this.serviceProviders.Add(BuildServiceProvider(services)));

    IServiceProvider BuildServiceProvider(Action<IServiceCollection> services)
    {
        var collection = new ServiceCollection();
        services(collection);
        return collection.BuildServiceProvider();
    }

    MoldiniumConfigurationBuilder Modify(Action action)
    {
        action();
        return this;
    }

    public DefaultDependencyProviderConfiguration Build(IServiceProvider? services = null)
        => new DefaultDependencyProviderConfiguration(mode,
            DefaultIListAndICollectionTypes: DefaultIListAndICollectionTypes,
            IsMoldiniumType: isModliniumType,
            Services: services
        );
}
