using Moldinium.Delegates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Testing.Misc;

[TestClass]
public class DelegateCreationTests
{
    [TestMethod]
    public void Args0Tests()
    {
        object GetString(object[] _) => "Hello!";

        var getString = DelegateCreation.CreateDelegate<Func<string>>(GetString);

        Assert.AreEqual("Hello!", getString());
    }

    [TestMethod]
    public void Args1Tests()
    {
        object GetString(object[] args) => $"Hello, {args[0]}";

        var getString = DelegateCreation.CreateDelegate<Func<string, string>>(GetString);

        Assert.AreEqual("Hello, you!", getString("you!"));
    }

    [TestMethod]
    public void Arg0ClosureTest()
    {
        var text = "Hello!";

        object GetString(object[] _) => text;

        var getString = DelegateCreation.CreateDelegate<Func<string>>(GetString);

        Assert.AreEqual(text, getString());
    }

    [TestMethod]
    public void Arg1ClosureTest()
    {
        var text = "Hello";

        object GetString(object[] args) => $"{text}, {args[0]}!";

        var getString = DelegateCreation.CreateDelegate<Func<string, string>>(GetString);

        Assert.AreEqual("Hello, you!", getString("you"));
    }

    [TestMethod]
    public void Arg2ClosureTest()
    {
        var text = "Hello";

        object GetString(object[] args) => $"{text}, {args[0]} {args[1]}!";

        var getString = DelegateCreation.CreateDelegate<Func<string, string, string>>(GetString);

        Assert.AreEqual("Hello, you there!", getString("you", "there"));
    }
}
