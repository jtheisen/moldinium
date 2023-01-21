using CMinus.Baking.CheekyCalling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Tests.CheekyCalling;

[TestClass]
public class CheekyCallingTests
{
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

    public static Object DynamicInvoke<TConcrete, TTarget>(String methodName, params Object[] arguments)
        where TConcrete : TTarget, new()
    {
        var delegateCreator = typeof(TTarget).GetSingleMethod(methodName).CreateDelegateCreator();
        var cheekyDelegate = delegateCreator(new TConcrete());
        return cheekyDelegate.DynamicInvoke(arguments)!;
    }

    [TestMethod]
    public void GetSecretTest()
    {
        Assert.AreEqual("secret", DynamicInvoke<CWithImpls, CWithImpls>("GetSecret"));
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
        Cheeky.DynamicInvoke<CWithImpls, IDerived>("FooVB", bs);
        Assert.AreEqual("IDerived", bs.Value);
    }
}

