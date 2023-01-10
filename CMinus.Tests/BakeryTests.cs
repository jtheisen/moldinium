using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests;

[TestClass]
public class BakeryTests
{
    static Bakery BasicFactory = new Bakery("Basic");

    public interface ITest
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

    [TestMethod]
    public void SimpleTest()
    {
        var test = BasicFactory.Create<ITest>();

        Assert.AreEqual(null, test.Value);

        test.Value = "foo";

        Assert.AreEqual("foo", test.Value);

        test.SetValue("bar");

        Assert.AreEqual("bar", test.Value);
    }

    public interface ITestWithInit
    {
        String Value { get; init; }

        void Validate()
        {
            // TODO: this should give a "" default
            Assert.AreEqual(null, Value);
        }
    }

    [TestMethod]
    public void WithInitTest() => BasicFactory.Create<ITestWithInit>();

    public struct TrivialComplexPropertyImplementation<Value, Container> : IPropertyImplementation<Value, Container, EmptyMixIn>
    {
        Value value;

        public Value Get(Container self, ref EmptyMixIn mixIn) => value;

        public void Set(Container self, ref EmptyMixIn mixIn, Value value) => this.value = value;
    }

    [TestMethod]
    public void TrivialComplexTest() => BakeryConfiguration.Create(typeof(TrivialComplexPropertyImplementation<,>))
        .CreateBakery("Complex")
        .Create<ITest>()
        .Validate();




    public struct TestMixin : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyPropertyChanged(Object o) => PropertyChanged?.Invoke(o, new PropertyChangedEventArgs(""));
    }

    public struct NotifyPropertyChangedPropertyImplementation<Value, Container> : IPropertyImplementation<Value, Container, TestMixin>
        where Container : class
    {
        Value value;

        public Value Get(Container self, ref TestMixin mixIn) => value;

        public void Set(Container self, ref TestMixin mixIn, Value value)
        {
            this.value = value;

            mixIn.NotifyPropertyChanged(self);
        }
    }

    [TestMethod]
    public void NotifyPropertyChangedTest()
    {
        var instance = BakeryConfiguration.Create(typeof(NotifyPropertyChangedPropertyImplementation<,>))
            .CreateBakery("Complex")
            .Create<ITest>();

        var instanceAsNotifyPropertyChanged = instance as INotifyPropertyChanged;

        var changeCount = 0;

        Assert.IsNotNull(instanceAsNotifyPropertyChanged);

        instanceAsNotifyPropertyChanged!.PropertyChanged += (o, e) =>
        {
            Assert.AreSame(instance, o);

            ++changeCount;
        };

        Assert.AreEqual(0, changeCount);

        instance.Value = "Foo";

        Assert.AreEqual("Foo", instance.Value);

        Assert.AreEqual(1, changeCount);
    }
}
