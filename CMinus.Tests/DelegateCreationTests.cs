using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests
{
    [TestClass]
    public class DelegateCreationTests
    {
        [TestMethod]
        public void BasicTests()
        {
            Object GetString(Object[] _) => "Hello!";

            var getString = DelegateCreation.CreateDelegate<Func<String>>(GetString);

            Assert.AreEqual("Hello!", getString());
        }

        [TestMethod]
        public void ClosureTest()
        {
            // This doesn't work yet as CreateDelegate can't bake the target into the 

            var text = "Hello!";

            Object GetString(Object[] _) => text;

            var getString = DelegateCreation.CreateDelegate<Func<String>>(GetString);

            Assert.AreEqual(text, getString());
        }
    }
}
