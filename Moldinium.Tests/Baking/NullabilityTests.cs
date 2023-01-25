using Microsoft.VisualStudio.TestTools.UnitTesting;
using SampleApp;
using System;
using System.Linq;
using System.Reflection;

namespace Moldinium.Tests.Baking;

public interface INullabilityTestInterface
{
    String? NullableInit { get; init; }

    String NotNullableInit { get; init; }

    String? Nullable { get; set; }

    String NotNullable { get; set; }
}

public interface INullabilityTestInterface2
{
    String? NullableInit { get; init; }

    String NotNullableInit { get; init; }

    String? Nullable { get; set; }

    String NotNullable { get; set; }

    Func<String?, Int32?, String, String> Foo { get; set; }
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
        var type = typeof(INullabilityTestInterface);

        AssertState(NullabilityState.Nullable, type, nameof(INullabilityTestInterface.Nullable));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityTestInterface.NotNullable));
        AssertState(NullabilityState.Nullable, type, nameof(INullabilityTestInterface.NullableInit));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityTestInterface.NotNullableInit));
    }

    [TestMethod]
    public void TestBakedAnalyzing()
    {
        var model = CreateTestModel<INullabilityTestInterface>();

        var type = model.GetType();

        Console.WriteLine(NullableAttributeReport.CreateReport(typeof(INullabilityTestInterface)));

        Console.WriteLine(NullableAttributeReport.CreateReport(typeof(INullabilityTestInterface2)));
    }

    [TestMethod]
    public void TestBaked()
    {
        var type = CreateTestModelType<INullabilityTestInterface>();

        AssertState(NullabilityState.Nullable, type, nameof(INullabilityTestInterface.Nullable));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityTestInterface.NotNullable));
        AssertState(NullabilityState.Nullable, type, nameof(INullabilityTestInterface.NullableInit));
        AssertState(NullabilityState.NotNull, type, nameof(INullabilityTestInterface.NotNullableInit));
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
