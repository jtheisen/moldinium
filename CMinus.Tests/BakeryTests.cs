using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests;

[TestClass]
public class BakeryTests
{
    static Bakery BasicFactory = new Bakery("Basic");

    public interface IPropertyTest
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

    public interface IPropertyTestWithInit
    {
        String Value { get; init; }

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
        public void Init() { }

        Value value;

        public Value Get(Object self, ref EmptyMixIn mixIn) => value;

        public void Set(Object self, ref EmptyMixIn mixIn, Value value) { this.value = value; }
    }

    [TestMethod]
    public void TrivialComplexTest() => BakeryConfiguration.Create(typeof(TrivialComplexPropertyImplementation<>))
        .CreateBakery(nameof(TrivialComplexTest))
        .Create<IPropertyTest>()
        .Validate();



    public interface INotifyPropertChangedMixin : INotifyPropertyChanged
    {
        Int32 ListenerCount { get; set; }
    }

    public struct NotifyPropertChangedMixin : INotifyPropertChangedMixin
    {
        public Int32 ListenerCount { get; set; }

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

        public void NotifyPropertyChanged(Object o) => backingPropertyChanged?.Invoke(o, new PropertyChangedEventArgs(""));
    }

    public struct NotifyPropertyChangedPropertyImplementation<Value> : IPropertyImplementation<Value, NotifyPropertChangedMixin>
    {
        Boolean initialized;

        public void Init() { initialized = true; }

        void AssertInitialized() { if (!initialized) throw new Exception(); }

        Value value;

        public Value Get(Object self, ref NotifyPropertChangedMixin mixIn) => value;

        public void Set(Object self, ref NotifyPropertChangedMixin mixIn, Value value)
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
