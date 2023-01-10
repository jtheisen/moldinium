using CMinus.Construction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests
{
    [TestClass]
    public class BakeryTests
    {
        static Bakery classFactory = new Bakery("Funky", new BakeryConfiguration(new PropertyGenerator()));

        public interface ITest
        {
            String Value { get; set; }

            void SetValue(String value) => Value = value;
        }

        [TestMethod]
        public void SimpleTests()
        {
            var test = classFactory.Create<ITest>();

            Assert.AreEqual(null, test.Value);

            test.Value = "foo";

            Assert.AreEqual("foo", test.Value);

            test.SetValue("bar");

            Assert.AreEqual("bar", test.Value);
        }

        public interface ITestWithInit
        {
            String Value { get; init; }
        }

        [TestMethod]
        public void WithInitTests()
        {
            classFactory.Create<ITestWithInit>();
        }
    }
}
