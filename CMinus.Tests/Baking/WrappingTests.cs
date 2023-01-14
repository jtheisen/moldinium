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

    public interface IWrappingPropertyImplementation<
        [TypeKind(ImplementationTypeArgumentKind.Value)] Value
    > : IPropertyImplementation
    {
        Boolean BeforeGet([MaybeNullWhen(true)] out Value value);
        void AfterGet(ref Value value);
        Boolean BeforeSet(ref Value value);
        void AfterSet(ref Value value);
    }

    public struct WrappingPropertyImplementation<Value> : IWrappingPropertyImplementation<Value>
    {


        public Boolean BeforeGet(ref Value value);
        public void AfterGet(ref Value value);
        public Boolean BeforeSet(ref Value value);
        public void AfterSet(ref Value value);
    }

    [TestMethod]
    public void PropertyWrappingTest()
    {
        var instance = BakeryConfiguration.Create(typeof(WrappingPropertyImplementation<>))
            .CreateDoubleBakery("Wrapping")
            .Create<IWithImplementedProperty>();

        Dictionary<String, String>

        instance.Validate();
    }
}
