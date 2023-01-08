using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CMinus.Tests
{
    public class RootService
    {

    }

    public class ClassType
    {
        public RootService RootService { get; init; }

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
                new BakeryDependencyProvider(new Bakery("TestBakery", makeAbstract: false)),
                new ActivatorDependencyProvider(),
                new InitSetterDependencyProvider()
            );
        }

        [TestMethod]
        public void ClassTests()
        {
            var scope = new Scope<ClassType>(provider);

            var classTypeInstance = scope.InstantiateRootType();

            classTypeInstance.Validate();
        }

        [TestMethod]
        public void InterfaceTests()
        {
            var scope = new Scope<InterfaceType>(provider);

            var interfaceTypeInstance = scope.InstantiateRootType();

            interfaceTypeInstance.Validate();
        }
    }
}
