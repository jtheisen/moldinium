using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests.Moldinium;

public interface Root<THasDependency>
    where THasDependency : IHasDependency
{
    Func<THasDependency> NewWithoutDependency { get; init; }
    Func<OurDependency, THasDependency> NewWithDependency { get; init; }

    Boolean Test(OurDependency ourDependency, Boolean passDependecy)
    {
        if (passDependecy)
        {
            var hasDepdency = NewWithDependency(ourDependency);

            return hasDepdency.DoesDependencyExist();
        }
        else
        {
            return NewWithoutDependency().DoesDependencyExist();
        }
    }
}

public class OurDependency
{
    public Boolean Exists => true;
}

public interface IHasDependency
{
    Boolean DoesDependencyExist();
}

public interface HasRequiredDependency : IHasDependency
{
    OurDependency Dependency { get; init; }

    Boolean IHasDependency.DoesDependencyExist() => Dependency.Exists;
}

public interface HasOptionalDependency : IHasDependency
{
    OurDependency? Dependency { get; init; }

    Boolean IHasDependency.DoesDependencyExist() => Dependency?.Exists ?? false;
}

[TestClass]
public class OptionalDependenciesTests : MoldiniumTestsBase
{
    [TestMethod]
    public void RequiredDependencyTest()
        => Assert.IsTrue(CreateTestModel<Root<HasRequiredDependency>>().Test(new OurDependency(), true));

    [TestMethod]
    public void OptionalPresentDependencyTest() =>
        Assert.IsTrue(CreateTestModel<Root<HasOptionalDependency>>().Test(new OurDependency(), true));

    [TestMethod]
    public void OptionalMissingDependencyTest()
        => Assert.IsFalse(CreateTestModel<Root<HasOptionalDependency>>(logReport: true).Test(new OurDependency(), false));
}
