using Moldinium.Baking;
using Moldinium.Common.Defaulting;
using Moldinium.Common.Misc;
using Moldinium.Injection;
using Moldinium.Tracking;
using System.Collections.ObjectModel;

namespace Moldinium.Internals;

public record DefaultDependencyProviderConfiguration(
    MoldiniumDefaultMode? Mode = MoldiniumDefaultMode.Basic,
    Boolean BakeAbstract = false,
    Boolean InitializeInits = true,
    Boolean EnableFactories = true,
    Boolean AcceptDefaultConstructibles = false,
    (Type, Type)? DefaultIListAndICollectionTypes = null,
    IServiceProvider? Services = null,
    Predicate<Type>? IsMoldiniumType = null
);

public static class DependencyProvider
{
    public static IDependencyProvider Create(DefaultDependencyProviderConfiguration config)
    {
        var providers = new List<IDependencyProvider>();

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

            var genericTypes = GetDefaultGenericCollectionTypes(mode);

            if (config.DefaultIListAndICollectionTypes is (Type, Type) genericTypes2)
            {
                genericTypes = genericTypes2;

                var (genericListType, genericCollectionType) = genericTypes2;

                if (genericListType != genericCollectionType)
                {
                    moduleNameSuffix = $".{genericListType.Name}.{genericCollectionType.Name}";
                }
                else
                {
                    moduleNameSuffix = $".{genericListType.Name}";
                }
            }

            var defaultProvider = Defaults.GetDefaultDefaultProvider(genericTypes);

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

    static (Type, Type) GetDefaultGenericCollectionTypes(MoldiniumDefaultMode mode) => mode switch
    {
        MoldiniumDefaultMode.Basic => (typeof(List<>), typeof(List<>)),
        MoldiniumDefaultMode.NotifyPropertyChanged => (typeof(LiveList<>), typeof(ObservableCollection<>)),
        MoldiniumDefaultMode.Tracking or MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged
            => (typeof(TrackableList<>), typeof(TrackableList<>)),
        _ => throw new NotImplementedException()
    };

    static ComponentGenerators CreateBakeryComponentGenerators(MoldiniumDefaultMode mode) => mode switch
    {
        MoldiniumDefaultMode.Basic => ComponentGenerators.Create(
            typeof(SimplePropertyImplementation<>)),
        MoldiniumDefaultMode.NotifyPropertyChanged => ComponentGenerators.Create(
            typeof(NotifyingPropertyImplementation<,>),
            typeof(NotifyingComputedPropertyImplementation<,>)),
        MoldiniumDefaultMode.Tracking => ComponentGenerators.Create(
            typeof(TrackedPropertyImplementation<>),
            typeof(TrackedComputedPropertyImplementation<,>)),
        MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged => ComponentGenerators.Create(
            typeof(TrackedNotifyingPropertyImplementation<,>),
            typeof(TrackedNotifyingComputedPropertyImplementation<,,>)),
        _ => throw new NotImplementedException()
    };
}
