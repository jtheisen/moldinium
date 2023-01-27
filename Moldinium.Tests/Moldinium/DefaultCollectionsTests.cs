using Moldinium.Common.Misc;
using Moldinium.Tracking;
using System.Collections.ObjectModel;

namespace Testing.Moldinium;

[TestClass]
public class DefaultCollectionsTests : MoldiniumTestsBase
{
    public interface TestModel
    {
        public IList<String> List { get; set; }
        public ICollection<String> Collection { get; set; }
    }

    [TestMethod]
    public void BasicTest()
    {
        var instance = CreateTestModel<TestModel>(MoldiniumDefaultMode.Basic);

        Assert.AreEqual(typeof(List<String>), instance.List.GetType());
        Assert.AreEqual(typeof(List<String>), instance.Collection.GetType());
    }

    [TestMethod]
    public void NotifyingTest()
    {
        var instance = CreateTestModel<TestModel>(MoldiniumDefaultMode.NotifyPropertyChanged);

        Assert.AreEqual(typeof(LiveList<String>), instance.List.GetType());
        Assert.AreEqual(typeof(ObservableCollection<String>), instance.Collection.GetType());
    }

    [TestMethod]
    public void TrackingTest()
    {
        var instance = CreateTestModel<TestModel>(MoldiniumDefaultMode.Tracking);

        Assert.AreEqual(typeof(TrackableList<String>), instance.List.GetType());
        Assert.AreEqual(typeof(TrackableList<String>), instance.Collection.GetType());
    }

    [TestMethod]
    public void TrackingAndNotifyingTest()
    {
        var instance = CreateTestModel<TestModel>(MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged);

        Assert.AreEqual(typeof(TrackableList<String>), instance.List.GetType());
        Assert.AreEqual(typeof(TrackableList<String>), instance.Collection.GetType());
    }

    [TestMethod]
    public void CustomTest()
    {
        var instance = CreateTestModel<TestModel>(c => c
            .SetMode(MoldiniumDefaultMode.Basic)
            .SetDefaultIListAndICollectionTypes(typeof(LiveList<>), typeof(TrackableList<>)));

        Assert.AreEqual(typeof(LiveList<String>), instance.List.GetType());
        Assert.AreEqual(typeof(TrackableList<String>), instance.Collection.GetType());
    }
}
