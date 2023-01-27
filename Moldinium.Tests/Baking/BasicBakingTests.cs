using Moldinium.Common.Misc;

namespace Testing.Baking;

[TestClass]
public class BasicBakingTests : BakingTestsBase
{
    [TestMethod]
    public void SimpleTest()
    {
        var test = BasicFactory.Create<IHasNullableProperty>();

        Assert.AreEqual(null, test.Value);

        test.Value = "foo";

        Assert.AreEqual("foo", test.Value);

        test.SetValueByMethod("bar");

        Assert.AreEqual("bar", test.Value);
    }

    [TestMethod]
    public void DefaultValueTest()
    {
        var test = BasicFactory.Create<IHasPropertyWithDefault>();

        Assert.AreEqual(NullableFlag.NotNullable, typeof(IHasPropertyWithDefault).GetNullableContextFlag());
        Console.WriteLine(NullableAttributeReport.CreateReport(typeof(BakingTestsBase)));
        Console.WriteLine(NullableAttributeReport.CreateReport(test.GetType()));

        Assert.AreEqual("", test.Value);
    }

    [TestMethod]
    public void EventTypeCreationTest() => BasicFactory.Create<IHasEvent>();


    [TestMethod]
    public void WithInitTest() => BasicFactory.Create<IHasStringPropertyWithInit>();



    public abstract class AHasStringPropertyWithInit
    {
        public abstract String Value { get; init; }
    }

    // Not supporting abstract baking for now
    //[TestMethod]
    //public void WithInitWithManualAbstractBaseTest()
    //    => BakeryConfiguration.Create().CreateBakery("Concrete").Create<AHasStringPropertyWithInit>();
}
