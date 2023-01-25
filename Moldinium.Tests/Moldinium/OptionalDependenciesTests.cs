namespace Testing.Moldinium;

public interface RootWithoutDependency<THasDependency>
    where THasDependency : IHasDependency
{
    Func<THasDependency> NewWithoutDependency { get; init; }

    Boolean Test(Boolean passDependecy)
    {
        return NewWithoutDependency().DoesDependencyExist();
    }
}

public interface RootWithDependency<THasDependency>
    where THasDependency : IHasDependency
{
    Func<OurDependency, THasDependency> NewWithDependency { get; init; }

    Boolean Test(OurDependency ourDependency)
    {
        return NewWithDependency(ourDependency).DoesDependencyExist();
    }
}

public interface RootWithBoth<THasDependency>
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
        => Assert.IsTrue(CreateTestModel<RootWithDependency<HasRequiredDependency>>().Test(new OurDependency()));

    [TestMethod]
    public void OptionalPresentDependencyTest() =>
        Assert.IsTrue(CreateTestModel<RootWithBoth<HasOptionalDependency>>().Test(new OurDependency(), true));

    [TestMethod]
    public void OptionalMissingDependencyTest()
        => Assert.IsFalse(CreateTestModel<RootWithBoth<HasOptionalDependency>>(logReport: true).Test(new OurDependency(), false));
}
