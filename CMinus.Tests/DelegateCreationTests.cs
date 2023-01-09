using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests;

[TestClass]
public class DelegateCreationTests
{
    [TestMethod]
    public void Args0Tests()
    {
        Object GetString(Object[] _) => "Hello!";

        var getString = DelegateCreation.CreateDelegate<Func<String>>(GetString);

        Assert.AreEqual("Hello!", getString());
    }

    [TestMethod]
    public void Args1Tests()
    {
        Object GetString(Object[] args) => $"Hello, {args[0]}";

        var getString = DelegateCreation.CreateDelegate<Func<String, String>>(GetString);

        Assert.AreEqual("Hello, you!", getString("you!"));
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
