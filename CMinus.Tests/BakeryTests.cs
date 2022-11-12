using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests
{
    [TestClass]
    public class BakeryTests
    {
        public interface ITest
        {
            String Value { get; set; }

            void SetValue(String value) => Value = value;
        }

        static Bakery classFactory = new Bakery("Funky", false);

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
    }
}
