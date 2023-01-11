using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CMinus.Tests;

[TestClass]
public class CombinedTests
{
    IDependencyProvider provider;

    public CombinedTests()
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
    public void InterfaceTypeWithParameterizedFactoryInstanceTest()
        => provider.CreateInstance<InterfaceTypeWithParameterizedFactory>().Validate();
}
