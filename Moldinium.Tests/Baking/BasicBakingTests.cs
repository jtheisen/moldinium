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

        Assert.AreEqual("", test.Value);
    }

    [TestMethod]
    public void DefaultCollectionsTest()
    {
        var test = BasicFactory.Create<IHasPropertiesWithCollection>();

        Assert.IsTrue(test.StringList.GetType() == typeof(List<String>));
        Assert.IsTrue(test.StringCollection.GetType() == typeof(List<String>));
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
