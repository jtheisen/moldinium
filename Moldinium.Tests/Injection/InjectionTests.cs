﻿using Moldinium.Baking;
using Moldinium.Internals;

namespace Testing.Injection;

public record Config(string Name);

public class RootService
{
}

public class ClassType
{
    public RootService RootService { get; init; } = null!;

    public void Validate()
    {
        Assert.IsNotNull(RootService);
    }
}

public interface InterfaceType
{
    RootService RootService { get; init; }

    void Validate()
    {
        Assert.IsNotNull(RootService);
    }
}

public interface InterfaceTypeWithSimpleFactory
{
    Func<InterfaceType> Create { get; init; }

    void Validate()
    {
        var instance = Create();

        instance.Validate();
    }
}

public struct IntWrapper
{
    public int Value;
}

public interface NestedInterfaceType
{
    IntWrapper MagicNumber { get; init; }

    Config Config { get; init; }

    void Validate()
    {
        Assert.AreEqual("Moldinium", Config.Name);
        Assert.AreEqual(42, MagicNumber.Value);
    }
}

public interface InterfaceTypeWithParameterizedFactory
{
    Func<Config, IntWrapper, NestedInterfaceType> Create { get; init; }

    void Validate()
    {
        var instance = Create(new Config("Moldinium"), new IntWrapper { Value = 42 });

        instance.Validate();
    }
}

[TestClass]
public class InjectionTests
{
    IDependencyProvider provider;

    public InjectionTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RootService());

        // Not used right now as we're blindly accepting all default constructible types anyway
        var knownTypesProvider = new ConcreteDependencyProvider(typeof(ClassType));

        provider = new CombinedDependencyProvider(
            new ServiceProviderDependencyProvider(services.BuildServiceProvider()),
            new AcceptingDefaultConstructiblesDependencyProvider(), // We really should only allow "baked" types to be blindly constructed
            new BakeryDependencyProvider(new Bakery("TestBakery")),
            new FactoryDependencyProvider(),
            new ActivatorDependencyProvider(),
            new InitSetterDependencyProvider()
        );
    }

    [TestMethod]
    public void InterfaceTypeResolutionTest()
    {
        var resolvedType = provider.ResolveType(typeof(InterfaceType));

        Assert.IsTrue(resolvedType.IsClass);
        Assert.IsTrue(resolvedType.GetInterface(nameof(InterfaceType)) is not null);
    }

    [TestMethod]
    public void ClassInstanceTest()
        => provider.CreateInstance<ClassType>().Validate();

    [TestMethod]
    public void InterfaceInstanceTest() => provider.CreateInstance<InterfaceType>().Validate();

    [TestMethod]
    public void InterfaceWithSimpleFactoryInstanceTest()
        => provider.CreateInstance<InterfaceTypeWithSimpleFactory>().Validate();

    [TestMethod]
    public void InterfaceTypeWithParameterizedFactoryInstanceTest()
        => provider.CreateInstance<InterfaceTypeWithParameterizedFactory>().Validate();
}
