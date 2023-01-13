using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests.Baking;

[TestClass]
public class BakeryTests
{
    static AbstractBakery BasicFactory = new DoubleBakery("Basic");

    public interface IPropertyTest
    {
        string DefaultInitializedValue { get; set; }

        string? Value { get; set; }

        void SetValue(string value) => Value = value;

        void Validate()
        {
            Assert.AreEqual(null, Value);

            Value = "foo";

            Assert.AreEqual("foo", Value);

            Value = "bar";

            Assert.AreEqual("bar", Value);
        }
    }

    public interface IPropertyTestWithInit
    {
        string Value { get; init; }

        void Validate()
        {
            // TODO: this should give a "" default
            Assert.AreEqual(null, Value);
        }
    }

    public interface IEventTest
    {
        event Action Event;
    }

    [TestMethod]
    public void SimpleTest()
    {
        var test = BasicFactory.Create<IPropertyTest>();

        Assert.AreEqual("", test.DefaultInitializedValue);

        Assert.AreEqual(null, test.Value);

        test.Value = "foo";

        Assert.AreEqual("foo", test.Value);

        test.SetValue("bar");

        Assert.AreEqual("bar", test.Value);
    }

    [TestMethod]
    public void EventTypeCreationTest() => BasicFactory.Create<IEventTest>();

    [TestMethod]
    public void WithInitTest() => BasicFactory.Create<IPropertyTestWithInit>();



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
        var instance = BakeryConfiguration.Create(typeof(TrivialComplexPropertyImplementation<>))
            .CreateBakery(nameof(TrivialComplexTest))
            .Create<IPropertyTest>();

        Assert.AreEqual("", instance.DefaultInitializedValue);

        instance.Validate();
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
            .CreateBakery(nameof(NotifyPropertyChangedTest))
            .Create<IPropertyTest>();

        Assert.AreEqual("", instance.DefaultInitializedValue);

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
