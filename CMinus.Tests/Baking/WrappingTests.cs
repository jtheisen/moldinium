using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static CMinus.Tests.Baking.WrappingTests;

namespace CMinus.Tests.Baking;

[TestClass]
public class WrappingTests : BakingTetsBase
{
    public interface IWithImplementedProperty
    {
        String? BackingValue { get; set; }

        Int32 Reads { get; set; }
        Int32 Writes { get; set; }

        String? Value
        {
            get { ++Reads; return BackingValue; }
            set
            {
                ++Writes;
                BackingValue = value;
            }
        }

        void Validate()
        {
            Assert.AreEqual(Reads, 0);
            Assert.AreEqual(Writes, 0);

            Assert.AreEqual(null, Value);

            Assert.AreEqual(Reads, 1);

            Value = "foo";

            Assert.AreEqual(Writes, 1);

            Assert.AreEqual("foo", Value);

            Assert.AreEqual(Reads, 2);

            Value = "bar";

            Assert.AreEqual(Writes, 2);

            Assert.AreEqual("bar", Value);

            Assert.AreEqual(Reads, 3);
        }
    }

    [TestMethod]
    public void PropertyNoWrappingTest()
    {
        var instance = BasicFactory.Create<IWithImplementedProperty>();

        instance.Validate();
    }


    public interface ITrivialWrappingPropertyImplementation<
        [TypeKind(ImplementationTypeArgumentKind.Value)] Value
    > : IPropertyImplementation
    {
        Boolean BeforeGet();
        void AfterGet();
        Boolean BeforeSet();
        void AfterSet();
    }

    public struct TrivialWrappingPropertyImplementation<Value> : ITrivialWrappingPropertyImplementation<Value>
    {
        public void AfterGet() { }

        public void AfterSet() { }

        public bool BeforeGet() => true;

        public bool BeforeSet() => true;
    }

    [TestMethod]
    public void TrivialPropertyWrappingTest()
    {
        var instance = BakeryConfiguration.Create(typeof(TrivialWrappingPropertyImplementation<>))
            .CreateDoubleBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        instance.Validate();
    }





    public enum WrappingPropertyNotificationEventType
    {
        BeforeGet,
        AfterGet,
        BeforeSet,
        AfterSet
    }

    public interface IWrappingPropertyNotificationMixin
    {
        event Action<WrappingPropertyNotificationEventType, Object?> OnEvent;
    }

    public struct WrappingPropertyNotificationMixin : IWrappingPropertyNotificationMixin
    {
        public event Action<WrappingPropertyNotificationEventType, Object?> OnEvent;

        public void Notify(WrappingPropertyNotificationEventType type, Object? value)
        {
            OnEvent?.Invoke(type, value);
        }
    }

    public interface IWrappingPropertyImplementation<
        [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
        [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
    > : IPropertyImplementation
    {
        Boolean BeforeGet([MaybeNullWhen(true)] ref Value value, ref Mixin mixin);
        void AfterGet(ref Value value, ref Mixin mixin);
        Boolean BeforeSet(ref Value value, ref Mixin mixin);
        void AfterSet(ref Value value, ref Mixin mixin);
    }

    public struct WrappingPropertyImplementation<Value> : IWrappingPropertyImplementation<Value, WrappingPropertyNotificationMixin>
    {
        public Boolean BeforeGet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.BeforeGet, value);

            return true;
        }

        public void AfterGet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.AfterGet, value);
        }

        public Boolean BeforeSet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.BeforeSet, value);

            return true;
        }

        public void AfterSet(ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.AfterSet, value);
        }
    }

    [TestMethod]
    public void PropertyWrappingTest()
    {
        var instance = BakeryConfiguration.Create(typeof(WrappingPropertyImplementation<>))
            .CreateDoubleBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        instance.Validate();

        var events = new List<(WrappingPropertyNotificationEventType type, Object? value)>();

        var observable = instance as IWrappingPropertyNotificationMixin;

        Assert.IsNotNull(observable);

        if (observable is null) throw new Exception();

        observable.OnEvent += (t, v) =>
        {
            events.Add((t, v));
        };

        Assert.AreEqual(0, events.Count);

        instance.Value = "foo";

        Assert.AreEqual(2, events.Count);
    }
}
