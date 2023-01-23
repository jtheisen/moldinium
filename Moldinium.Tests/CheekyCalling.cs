using Moldinium.Delegates;
using Moldinium.Delegates.TestStuff;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;

namespace Moldinium.Tests.CheekyCalling;

[TestClass]
public class CheekyCallingTests
{
    public static Object DynamicInvoke<TConcrete, TTarget>(String methodName, params Object[] arguments)
        where TConcrete : TTarget, new()
    {
        return DynamicInvoke<TConcrete, TTarget>(typeof(TTarget).GetSingleMethod(methodName), arguments);
    }

    public static Object DynamicInvoke<TConcrete, TTarget>(MethodInfo method, params Object[] arguments)
        where TConcrete : TTarget, new()
    {
        var delegateTypeCreator = new DelegateTypeCreator();

        var useBind = false; // using bind gives MethodAccessException?

        var delegateCreator = delegateTypeCreator.CreateDelegateCreatorAsDynamicMethod(method, useBind);
        var cheekyDelegate = delegateCreator(new TConcrete());
        return cheekyDelegate.DynamicInvoke(arguments)!;
    }

    [TestMethod]
    public void GetSecretTest()
    {
        Assert.AreEqual("secret", DynamicInvoke<CWithImpls, CWithImpls>("GetSecret"));
    }

    [TestMethod]
    public void TestProp()
    {
        Assert.AreEqual("CWithImpls", (new CWithImpls() as IBase).FooProp);

        var fooProp = typeof(IDerived)
            .GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.Name.EndsWith(".FooProp"))
            .Single();

        Assert.AreEqual("IDerived", DynamicInvoke<CWithImpls, IDerived>(fooProp.GetMethod ?? throw new Exception()));
    }

    [TestMethod]
    public void TestS()
    {
        Assert.AreEqual("CWithImpls", (new CWithImpls() as IBase).FooS());
        Assert.AreEqual("IDerived", DynamicInvoke<CWithImpls, IDerived>("FooS"));
    }

    [TestMethod]
    public void TestSS()
    {
        Assert.AreEqual("CWithImpls (x)", (new CWithImpls() as IBase).FooSS("x"));
        Assert.AreEqual("IDerived (x)", DynamicInvoke<CWithImpls, IDerived>("FooSS", "x"));
    }

    [TestMethod]
    public void TestB()
    {
        var bs = new BoxedString();
        DynamicInvoke<CWithImpls, IDerived>("FooVB", bs);
        Assert.AreEqual("IDerived", bs.Value);
    }

    // Calli worked once in a LINQPad query, at least after first using the delegate way, but right now I can't get it to work at all
    //[TestMethod]
    //public void CalliCallerTest()
    //{
    //    Func<String> a = (new CInnocent() as IBase).FooS;

    //    var calliCaller = Cheeky.CreateCalliCaller<Func<IBase, String>, IBase>(typeof(IDerived), "FooS");

    //    var cWithImpls = new CWithImpls();

    //    // this is required for the below to work - I guess the delegate internally assures
    //    // that the private implementation is loaded
    //    a();

    //    Assert.AreEqual("IDerived", () => calliCaller(cWithImpls));
    //}
}

