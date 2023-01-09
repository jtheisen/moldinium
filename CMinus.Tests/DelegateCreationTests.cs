using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests;

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
        var text = "Hello!";

        Object GetString(Object[] _) => text;

        var getString = DelegateCreation.CreateDelegate<Func<String>>(GetString);

        Assert.AreEqual(text, getString());
    }
}
