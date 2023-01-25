using Microsoft.VisualStudio.TestTools.UnitTesting;
using SampleApp;
using System;
using System.Reflection;

namespace Moldinium.Tests.Baking;

public interface INullabilityMixedTestInterface
{
    String? NullableInit { get; init; }

    String NotNullableInit { get; init; }

    String? Nullable { get; set; }

    String NotNullable { get; set; }

    Int32? NullableValue { get; set; }

    Int32 NotNullableValue { get; set; }

    String NotNullable1 { get; set; }
    String NotNullable2 { get; set; }

    Func<String?, Int32?, String, String>? Foo { get; set; }
}

public interface INullabilityMoreNullableTestInterface
{
    String? NullableInit { get; init; }

    String? Nullable { get; set; }
}

public interface LocalSimpleJob
{
    String? Config { get; init; }
}

[TestClass]
public class NullabilityTests : BakingTestsBase
{
    NullabilityInfoContext nullabilityInfoContext = new NullabilityInfoContext();

    [TestMethod]
    public void TestInterface()
    {
        var type = typeof(INullabilityMixedTestInterface);

        AssertState(NullabilityState.Nullable, type, nameof(INullabilityMixedTestInterface.Nullable));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityMixedTestInterface.NotNullable));
        AssertState(NullabilityState.Nullable, type, nameof(INullabilityMixedTestInterface.NullableInit));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityMixedTestInterface.NotNullableInit));
    }

    [TestMethod]
    public void TestBakedAnalyzing()
    {
        Console.WriteLine(NullableAttributeReport.CreateReport(typeof(INullabilityMixedTestInterface)));
        Console.WriteLine(NullableAttributeReport.CreateReport(typeof(INullabilityMoreNullableTestInterface)));
        Console.WriteLine(NullableAttributeReport.CreateReport(CreateTestModelType<INullabilityMixedTestInterface>()));
        Console.WriteLine(NullableAttributeReport.CreateReport(CreateTestModelType<INullabilityMoreNullableTestInterface>()));
    }

    [TestMethod]
    public void TestBaked()
    {
        var type = CreateTestModelType<INullabilityMixedTestInterface>();

        AssertState(NullabilityState.Nullable, type, nameof(INullabilityMixedTestInterface.Nullable));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityMixedTestInterface.NotNullable));
        AssertState(NullabilityState.Nullable, type, nameof(INullabilityMixedTestInterface.NullableInit));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityMixedTestInterface.NotNullableInit));
    }

    [TestMethod]
    public void TestSampleAppSimpleJob()
    {
        var type = CreateTestModelType<SimpleJob>();

        AssertState(NullabilityState.Nullable, type, nameof(SimpleJob.Config));
    }

    [TestMethod]
    public void TestSampleAppLocalSimpleJob()
    {
        var type = CreateTestModelType<LocalSimpleJob>();

        AssertState(NullabilityState.Nullable, type, nameof(LocalSimpleJob.Config));
    }

    void AssertState(NullabilityState state, Type type, String name)
    {
        var info = nullabilityInfoContext.Create(type.GetProperty(name)!);

        Assert.AreEqual(state, info.ReadState);
        Assert.AreEqual(state, info.WriteState);
    }
}
