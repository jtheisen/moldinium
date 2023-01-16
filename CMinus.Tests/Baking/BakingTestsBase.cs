using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static CMinus.Tests.Baking.WrappingTests;

namespace CMinus.Tests.Baking;

public class BakingTestsBase
{
    protected static readonly AbstractBakery BasicFactory = new Bakery("Basic");

    protected T CreateTestModel<T, I>(ComponentGenerators generators, out I ifc)
        where I : class
    {
        var instance = new BakeryConfiguration(generators, Defaults.GetDefaultDefaultProvider())
            .CreateBakery("TestBakery")
            .Create<T>();

        var ifcMaybeNull = instance as I;

        Assert.IsNotNull(ifcMaybeNull);

        if (ifcMaybeNull is null) throw new Exception();

        ifc = ifcMaybeNull;

        return instance;
    }

    public interface IHasStringPropertyWithInit
    {
        String Value { get; init; }
    }

    public interface IHasPropertyWithDefault
    {
        String Value { get; set; }
    }

    public interface IHasNullableProperty
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

    public interface IHasEvent
    {
        event Action Event;
    }
}
