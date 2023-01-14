using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests.Baking;

[TestClass]
public class BakeryTests
{
    static AbstractBakery BasicFactory = new DoubleBakery("Basic");

    public interface IHasStringPropertyWithInit
    {
        String Value { get; init; }
    }

    public interface IHasPropertyWithDefault
    {
        String Value { get; set; }
    }

    public interface IHasNullableProperty
    {
        String? Value { get; set; }

        void SetValue(String value) => Value = value;

        void Validate()
        {
            Assert.AreEqual(null, Value);

            Value = "foo";

            Assert.AreEqual("foo", Value);

            Value = "bar";

            Assert.AreEqual("bar", Value);
        }
    }

    public interface IHasEvent
    {
        event Action Event;
    }

    [TestMethod]
    public void SimpleTest()
    {
        var test = BasicFactory.Create<IHasNullableProperty>();

        Assert.AreEqual(null, test.Value);

        test.Value = "foo";

        Assert.AreEqual("foo", test.Value);

        test.SetValue("bar");

        Assert.AreEqual("bar", test.Value);
    }

    [TestMethod]
    public void DefaultValueTest()
    {
        var test = BasicFactory.Create<IHasPropertyWithDefault>();

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

    [TestMethod]
    public void WithInitWithManualAbstractBaseTest()
        => BakeryConfiguration.Create().CreateBakery("Concrete").Create<IHasStringPropertyWithInit>();




    public struct TrivialComplexPropertyImplementation<Value> : IPropertyImplementation<Value, EmptyMixIn>
    {
        public void Init(Value def) => value = def;

        Value value;

        public Value Get(object self, ref EmptyMixIn mixIn) => value;

        public void Set(object self, ref EmptyMixIn mixIn, Value value) { this.value = value; }
    }

    [TestMethod]
    public void TrivialComplexTest()
    {
        var bakery = BakeryConfiguration.Create(typeof(TrivialComplexPropertyImplementation<>))
            .CreateBakery(nameof(TrivialComplexTest));

        {
            var instance = bakery.Create<IHasPropertyWithDefault>();

            Assert.AreEqual("", instance.Value);
        }

        bakery.Create<IHasNullableProperty>().Validate();
    }

    public interface INotifyPropertChangedMixin : INotifyPropertyChanged
    {
        int ListenerCount { get; set; }
    }

    public struct NotifyPropertChangedMixin : INotifyPropertChangedMixin
    {
        public int ListenerCount { get; set; }

        event PropertyChangedEventHandler? backingPropertyChanged;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                ListenerCount++;

                backingPropertyChanged += value;
            }
            remove
            {
                backingPropertyChanged -= value;

                ListenerCount--;
            }
        }

        public void NotifyPropertyChanged(object o) => backingPropertyChanged?.Invoke(o, new PropertyChangedEventArgs(""));
    }

    public struct NotifyPropertyChangedPropertyImplementation<Value> : IPropertyImplementation<Value, NotifyPropertChangedMixin>
    {
        bool initialized;

        public void Init(Value def) { initialized = true; value = def; }

        void AssertInitialized() { if (!initialized) throw new Exception(); }

        Value value;

        public Value Get(object self, ref NotifyPropertChangedMixin mixIn) => value;

        public void Set(object self, ref NotifyPropertChangedMixin mixIn, Value value)
        {
            AssertInitialized();

            this.value = value;

            mixIn.NotifyPropertyChanged(self);
        }
    }

    [TestMethod]
    public void NotifyPropertyChangedTest()
    {
        var instance = BakeryConfiguration.Create(typeof(NotifyPropertyChangedPropertyImplementation<>))
            .CreateDoubleBakery(nameof(NotifyPropertyChangedTest))
            .Create<IHasNullableProperty>();

        var instanceAsNotifyPropertyChanged = instance as INotifyPropertChangedMixin;

        var changeCount = 0;

        Assert.IsNotNull(instanceAsNotifyPropertyChanged);

        Assert.AreEqual(0, instanceAsNotifyPropertyChanged!.ListenerCount);

        PropertyChangedEventHandler handler = (o, e) =>
        {
            Assert.AreSame(instance, o);

            ++changeCount;
        };

        instanceAsNotifyPropertyChanged!.PropertyChanged += handler;

        Assert.AreEqual(1, instanceAsNotifyPropertyChanged!.ListenerCount);

        Assert.AreEqual(0, changeCount);

        instance.Value = "Foo";

        Assert.AreEqual("Foo", instance.Value);

        Assert.AreEqual(1, changeCount);

        instanceAsNotifyPropertyChanged!.PropertyChanged -= handler;

        Assert.AreEqual(0, instanceAsNotifyPropertyChanged!.ListenerCount);
    }
}
