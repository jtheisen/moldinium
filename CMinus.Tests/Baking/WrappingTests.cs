using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static CMinus.Tests.Baking.WrappingTests;

namespace CMinus.Tests.Baking;

[TestClass]
public class WrappingTests : BakingTetsBase
{
    public interface IWithCountingProperty
    {
        Int32 Counter { get; set; }

        Int32 Value { get => Counter++; set => Counter = value; }
    }

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
        Boolean BeforeSet();
    }

    public struct TrivialWrappingPropertyImplementation<Value> : ITrivialWrappingPropertyImplementation<Value>
    {
        public bool BeforeGet() => true;
        public bool BeforeSet() => true;
    }

    [TestMethod]
    public void TrivialPropertyWrappingTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(TrivialWrappingPropertyImplementation<>))
            .CreateBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        instance.Validate();
    }

    public struct TrivialDontDelegateWrappingPropertyImplementation<Value> : ITrivialWrappingPropertyImplementation<Value>
    {
        public bool BeforeGet() => false;
        public bool BeforeSet() => false;
    }

    [TestMethod]
    public void TrivialDontDelegatePropertyWrappingTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(TrivialDontDelegateWrappingPropertyImplementation<>))
            .CreateBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        Assert.IsNull(instance.Value);

        instance.Value = "foo";

        Assert.IsNull(instance.Value);
    }


    public enum WrappingPropertyNotificationEventType
    {
        BeforeGet,
        AfterGet,
        AfterErrorGet,
        BeforeSet,
        AfterSet,
        AfterErrorSet
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
        Boolean BeforeSet(ref Value value, ref Mixin mixin);

        void AfterGet(ref Value value, ref Mixin mixin);
        void AfterSet(ref Value value, ref Mixin mixin);

        Boolean AfterErrorGet(Exception exception, ref Value value, ref Mixin mixin);
        Boolean AfterErrorSet(Exception exception, ref Value value, ref Mixin mixin);
    }

    public struct EventWrappingPropertyImplementation<Value> : IWrappingPropertyImplementation<Value, WrappingPropertyNotificationMixin>
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

        public Boolean AfterErrorGet(Exception exception, ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.AfterErrorGet, exception);

            return true;
        }

        public Boolean AfterErrorSet(Exception exception, ref Value value, ref WrappingPropertyNotificationMixin mixin)
        {
            mixin.Notify(WrappingPropertyNotificationEventType.AfterErrorSet, exception);

            return true;
        }
    }

    [TestMethod]
    public void PropertyEventWrappingTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(EventWrappingPropertyImplementation<>))
            .CreateBakery("Wrapping")
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

    public interface ICachingPropertyMixin
    {
        Boolean IsValid { get; set; }

        Boolean LockSetter { get; set; }
    }

    public struct CachingPropertyMixin : ICachingPropertyMixin
    {
        public Boolean IsValid { get; set; }

        public Boolean LockSetter { get; set; }
    }

    public struct CachingWrappingPropertyImplementation<Value> : IWrappingPropertyImplementation<Value, CachingPropertyMixin>
    {
        Value cache;
        Exception exception;

        public Boolean BeforeGet(ref Value value, ref CachingPropertyMixin mixin)
        {
            if (!mixin.IsValid)
            {
                return true;
            }
            else
            {
                value = cache;

                return false;
            }
        }

        public void AfterGet(ref Value value, ref CachingPropertyMixin mixin)
        {
            cache = value;

            mixin.IsValid = true;
        }

        public Boolean BeforeSet(ref Value value, ref CachingPropertyMixin mixin)
        {
            return !mixin.LockSetter;
        }

        public void AfterSet(ref Value value, ref CachingPropertyMixin mixin)
        {
            mixin.IsValid = false;
        }

        public Boolean AfterErrorGet(Exception exception, ref Value value, ref CachingPropertyMixin mixin)
        {
            this.exception = exception;

            return true;
        }

        public Boolean AfterErrorSet(Exception exception, ref Value value, ref CachingPropertyMixin mixin)
        {
            this.exception = exception;

            return true;
        }
    }

    [TestMethod]
    public void PropertyCacheWrappingTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(CachingWrappingPropertyImplementation<>))
            .CreateBakery("Wrapping")
            .Create<IWithCountingProperty>();

        var cacheControl = instance as ICachingPropertyMixin;

        Assert.IsNotNull(cacheControl);

        if (cacheControl is null) throw new Exception();

        Assert.AreEqual(cacheControl.IsValid, false);

        Assert.AreEqual(instance.Value, 0);

        Assert.AreEqual(cacheControl.IsValid, true);

        Assert.AreEqual(instance.Value, 0);

        cacheControl.IsValid = false;

        Assert.AreEqual(instance.Value, 1);

        instance.Value = 42;

        Assert.AreEqual(instance.Value, 42);

        Assert.AreEqual(instance.Value, 42);

        cacheControl.IsValid = false;

        Assert.AreEqual(instance.Value, 43);

        cacheControl.LockSetter = true;

        instance.Value = 0;

        Assert.AreEqual(instance.Value, 43);
    }
}
