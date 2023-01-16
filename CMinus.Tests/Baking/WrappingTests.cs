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

        Int32 SetCounter(Int32 counter) => Counter = counter;
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

        void SetValue(String? value) => BackingValue = value;

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

    public class TestException : Exception { }

    public interface IThrowingModel
    {
        Boolean DoThrowOnMethods { get; set; }
        Boolean DoThrowOnGet { get; set; }
        Boolean DoThrowOnSet { get; set; }

        Int32 BackingValue { get; set; }

        Int32 Value
        {
            get
            {
                if (DoThrowOnGet) throw new TestException();

                return BackingValue;
            }

            set
            {
                if (DoThrowOnSet) throw new TestException();

                BackingValue = value;
            }
        }

        void SetValue(Int32 value)
        {
            if (DoThrowOnMethods) throw new TestException();

            BackingValue = value;
        }
    }

    [TestMethod]
    public void NoWrappingTest()
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
    public void TrivialDontDelegateTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(TrivialDontDelegateWrappingPropertyImplementation<>))
            .CreateBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        Assert.IsNull(instance.Value);

        instance.Value = "foo";

        Assert.IsNull(instance.Value);
    }


    [TestMethod]
    public void EventTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(EventWrappingPropertyImplementation<,>))
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

    public struct CachingWrappingPropertyImplementation<Value, Exception>
        : IWrappingPropertyImplementation<Value, Exception, CachingPropertyMixin>
        where Exception : System.Exception
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
    public void CacheTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(CachingWrappingPropertyImplementation<,>))
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

    [TestMethod]
    public void ExceptionWithEventsTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(EventWrappingPropertyImplementation<,>))
            .CreateBakery("Wrapping")
            .Create<IThrowingModel>();

        var events = new List<(WrappingPropertyNotificationEventType type, Object? value)>();

        var observable = instance as IWrappingPropertyNotificationMixin;

        Assert.IsNotNull(observable);

        if (observable is null) throw new Exception();

        observable.OnEvent += (t, v) =>
        {
            events.Add((t, v));
        };

        Assert.AreEqual(0, events.Count);

        instance.Value = 42;

        Assert.AreEqual(2, events.Count);

        instance.DoThrowOnSet = true;

        Assert.ThrowsException<TestException>(() => instance.Value = 43);

        Assert.AreEqual(4, events.Count);
    }

    [TestMethod]
    public void ExceptionWithDelegatesTest()
    {
        var instance = BakeryConfiguration.Create(propertyWrappingType: typeof(DelegatingWrappingPropertyImplementation<,>))
            .CreateBakery("Wrapping")
            .Create<IThrowingModel>();

        var wrapperImplementation = instance as IDelegatingWrappingPropertyMixin;

        Assert.IsNotNull(wrapperImplementation);

        if (wrapperImplementation is null) throw new Exception();

        Exception? exception = null;

        wrapperImplementation.GetWrappingMethods = new WrappingMethods<Int32>(
            AfterError: (Exception ex) => { exception = ex; return true; }
        );

        wrapperImplementation.SetWrappingMethods = new WrappingMethods<Int32>(
            AfterError: (Exception ex) => { exception = ex; return true; }
        );

        instance.Value = 42;

        Assert.AreEqual(42, instance.Value);

        instance.DoThrowOnGet = true;

        Assert.ThrowsException<TestException>(() => instance.Value);

        Assert.IsNotNull(exception);

        instance.DoThrowOnGet = false;
        instance.DoThrowOnSet = true;
        exception = null;

        wrapperImplementation.SetWrappingMethods = new WrappingMethods<Int32>(
            AfterError: (Exception ex) => { exception = ex; return false; }
        );

        instance.Value = 43;

        Assert.AreEqual(42, instance.Value);

        Assert.IsNotNull(exception);
    }

    [TestMethod]
    public void ExceptionOnMethodWithDelegatesTest()
    {
        var instance = BakeryConfiguration.Create(
            methodWrapperType: typeof(DelegatingWrappingMethodImplementation<,>),
            propertyWrappingType: null
        )
            .CreateBakery("Wrapping")
            .Create<IThrowingModel>();

        var wrapperImplementation = instance as IDelegatingWrappingPropertyMixin;

        Assert.IsNotNull(wrapperImplementation);

        if (wrapperImplementation is null) throw new Exception();

        Exception? exception = null;

        wrapperImplementation.MethodWrappingMethods = new WrappingMethods<Int32>(
            AfterError: (Exception ex) => { exception = ex; return true; }
        );

        instance.SetValue(42);

        instance.DoThrowOnMethods = true;

        Assert.ThrowsException<TestException>(() => instance.SetValue(43));
    }
}
