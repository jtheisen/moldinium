namespace Testing.Baking;

[TestClass]
public class WrappingTests : BakingTestsBase
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
    > : IPropertyWrapperImplementation
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
        var instance = BakeryConfiguration.Create(typeof(TrivialWrappingPropertyImplementation<>))
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
        var instance = CreateTestModel<IWithImplementedProperty, Object>(
            out var _, typeof(TrivialDontDelegateWrappingPropertyImplementation<>)
        );

        Assert.IsNull(instance.Value);

        instance.Value = "foo";

        Assert.IsNull(instance.Value);
    }


    [TestMethod]
    public void EventTest()
    {
        var instance = CreateTestModel<IWithImplementedProperty, Object>(
            out var _, typeof(EventWrappingPropertyImplementation<,>)
        );

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
        var instance = CreateTestModel<IWithCountingProperty, ICachingPropertyMixin>(
            out var cacheControl, typeof(CachingWrappingPropertyImplementation<,>)
        );

        Assert.AreEqual(false, cacheControl.IsValid);

        Assert.AreEqual(0, instance.Value);

        Assert.AreEqual(true, cacheControl.IsValid);

        Assert.AreEqual(0, instance.Value);

        cacheControl.IsValid = false;

        Assert.AreEqual(1, instance.Value);

        instance.Value = 42;

        Assert.AreEqual(42, instance.Value);

        Assert.AreEqual(42, instance.Value);

        cacheControl.IsValid = false;

        Assert.AreEqual(43, instance.Value);

        cacheControl.LockSetter = true;

        instance.Value = 0;

        Assert.AreEqual(43, instance.Value);
    }

    [TestMethod]
    public void ExceptionWithEventsTest()
    {
        var instance = CreateTestModel<IThrowingModel, IWrappingPropertyNotificationMixin>(
            out var observable, typeof(EventWrappingPropertyImplementation<,>)
        );

        var events = new List<(WrappingPropertyNotificationEventType type, Object? value)>();

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
        var instance = CreateTestModel<IThrowingModel, IDelegatingWrappingPropertyMixin>(
            out var wrapperImplementation, typeof(DelegatingWrappingPropertyImplementation<,>)
        );

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
        var instance = CreateTestModel<IThrowingModel, IDelegatingWrappingPropertyMixin>(
            out var wrapperImplementation, typeof(DelegatingWrappingMethodImplementation<,>)
        );

        Exception? exception = null;

        wrapperImplementation.MethodWrappingMethods = new WrappingMethods<Int32>(
            AfterError: (Exception ex) => { exception = ex; return true; }
        );

        instance.SetValue(42);

        instance.DoThrowOnMethods = true;

        Assert.ThrowsException<TestException>(() => instance.SetValue(43));
    }
}
