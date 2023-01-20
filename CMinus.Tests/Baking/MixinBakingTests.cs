using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests.Baking;

[TestClass]
public class MixinBakingTests : BakingTestsBase
{
    public struct TrivialStandardComplexPropertyImplementation<Value, Container>
        : IStandardPropertyImplementation<Value, Container, EmptyMixIn>
    {
        public void Init(Value def) => value = def;

        Value value;

        public Value Get(Container self, ref EmptyMixIn mixIn) => value;

        public void Set(Container self, ref EmptyMixIn mixIn, Value value) { this.value = value; }
    }

    [TestMethod]
    public void TrivialComplexTest()
    {
        var bakery = BakeryConfiguration.Create(typeof(TrivialStandardComplexPropertyImplementation<,>))
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

        void NotifyPropertyChanged(object o);
    }

    public struct NotifyPropertChangedMixin : INotifyPropertChangedMixin
    {
        public int ListenerCount { get; set; }

        Int32 INotifyPropertChangedMixin.ListenerCount { get => ListenerCount; set => ListenerCount = value; }

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

        void INotifyPropertChangedMixin.NotifyPropertyChanged(object o) => NotifyPropertyChanged(o);
    }

    public struct NotifyPropertyChangedPropertyImplementation<Value, Container>
        : IStandardPropertyImplementation<Value, Container, NotifyPropertChangedMixin>
    {
        bool initialized;

        public void Init(Value def) { initialized = true; value = def; }

        void AssertInitialized() { if (!initialized) throw new Exception(); }

        Value value;

        public Value Get(Container self, ref NotifyPropertChangedMixin mixIn) => value;

        public void Set(Container self, ref NotifyPropertChangedMixin mixIn, Value value)
        {
            AssertInitialized();

            this.value = value;

            mixIn.NotifyPropertyChanged(self);
        }
    }

    [TestMethod]
    public void NotifyPropertyChangedTest()
    {
        var instance = BakeryConfiguration.Create(typeof(NotifyPropertyChangedPropertyImplementation<,>))
            .CreateBakery(nameof(NotifyPropertyChangedTest))
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
