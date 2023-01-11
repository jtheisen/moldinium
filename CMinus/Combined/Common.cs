﻿using CMinus.Injection;
using System;
using System.Collections.Generic;

namespace CMinus;

public struct TrackedPropertyImplementation<T>
{
    Var<T> variable;

    public T Value { get => variable.Value; set => variable.Value = value; }
}

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
    public DependencyProviderBuilder AddInstance(Dependency dep, Object instance) => concretes.AddInstance(dep, instance).Return(this);
    public DependencyProviderBuilder Accept(Dependency dep) => concretes.Accept(dep).Return(this);
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
    Boolean EnableOldModliniumModels = false,
    Boolean InitializeInits = true,
    Boolean EnableFactories = true,
    Action<DependencyProviderBuilder>? Build = null,
    IServiceProvider? Services = null
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

        if (config.Baking is not null && config.Baking != DefaultDependencyProviderBakingMode.Basic)
        {
            throw new NotImplementedException($"The simple bakery is not configurable");
        }

        if (config.Services is IServiceProvider services)
        {
            providers.Add(new ServiceProviderDependencyProvider(services));
        }

        if (config.EnableOldModliniumModels)
        {
            providers.Add(new OldMoldiniumModelDependencyProvider());
        }

        providers.Add(new AcceptingDefaultConstructiblesDependencyProvider());

        if (config.Baking is DefaultDependencyProviderBakingMode bakingMode)
        {
            providers.Add(new BakeryDependencyProvider(new Bakery("TestBakery")));
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

    //static BakeryConfiguration CreateBakeryConfiguration(DefaultDependencyProviderBakingMode mode) => mode switch
    //{
    //    DefaultDependencyProviderBakingMode.Basic => BakeryConfiguration.Create(typeof(GenericPropertyImplementation<>)),
    //    DefaultDependencyProviderBakingMode.Tracking => BakeryConfiguration.Create(typeof(TrackedPropertyImplementation<>)),
    //    _ => throw new NotImplementedException()
    //};
}
