using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
    public void SimpleTests()
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
    public void WithInitTests() => BasicFactory.Create<ITestWithInit>();

    public struct TrivialComplexPropertyImplementation<Value, Container> : IPropertyImplementation<Value, Container, EmptyMixIn>
    {
        Value value;

        public Value Get(Container self, ref EmptyMixIn mixIn) => value;

        public void Set(Container self, ref EmptyMixIn mixIn, Value value) => this.value = value;
    }

    [TestMethod]
    public void WithTrivialComplexTests() => BakeryConfiguration.Create(typeof(TrivialComplexPropertyImplementation<,>))
        .CreateBakery("Complex")
        .Create<ITest>()
        .Validate();
}
