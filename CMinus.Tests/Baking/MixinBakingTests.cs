using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests.Baking;

[TestClass]
public class MixinBakingTests : BakingTetsBase
{
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
