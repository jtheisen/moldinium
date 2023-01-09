﻿using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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

    public interface InterfaceTypeWithFactory
    {
        Func<InterfaceType> Create { get; init; }

        void Validate()
        {
            var instance = Create();

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
                new BakeryDependencyProvider(new Bakery("TestBakery", makeAbstract: false)),
                new ActivatorDependencyProvider(),
                new InitSetterDependencyProvider()
            );
        }

        [TestMethod]
        public void InterfaceTypeResolutionTests()
        {
            var resolvedType = provider.ResolveType(typeof(InterfaceType));

            Assert.IsTrue(resolvedType.IsClass);
            Assert.IsTrue(resolvedType.GetInterface(nameof(InterfaceType)) is not null);
        }

        [TestMethod]
        public void ClassInstanceTests()
        {
            var instance = provider.CreateInstance<ClassType>();

            instance.Validate();
        }

        [TestMethod]
        public void InterfaceInstanceTests()
        {
            var instance = provider.CreateInstance<InterfaceType>();

            instance.Validate();
        }

        //[TestMethod]
        //public void InterfaceWithFactoryInstanceTests()
        //{
        //    var instance = provider.CreateInstance<InterfaceTypeWithFactory>();

        //    instance.Validate();
        //}
    }
}