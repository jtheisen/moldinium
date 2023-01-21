using CMinus.Delegates;
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
    public void Arg0ClosureTest()
    {
        var text = "Hello!";

        Object GetString(Object[] _) => text;

        var getString = DelegateCreation.CreateDelegate<Func<String>>(GetString);

        Assert.AreEqual(text, getString());
    }

    [TestMethod]
    public void Arg1ClosureTest()
    {
        var text = "Hello";

        Object GetString(Object[] args) => $"{text}, {args[0]}!";

        var getString = DelegateCreation.CreateDelegate<Func<String, String>>(GetString);

        Assert.AreEqual("Hello, you!", getString("you"));
    }

    [TestMethod]
    public void Arg2ClosureTest()
    {
        var text = "Hello";

        Object GetString(Object[] args) => $"{text}, {args[0]} {args[1]}!";

        var getString = DelegateCreation.CreateDelegate<Func<String, String, String>>(GetString);

        Assert.AreEqual("Hello, you there!", getString("you", "there"));
    }
}
