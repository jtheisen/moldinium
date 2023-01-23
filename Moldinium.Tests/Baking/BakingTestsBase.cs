using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using static Moldinium.Tests.Baking.WrappingTests;

namespace Moldinium.Tests.Baking;

public class BakingTestsBase
{
    protected static readonly AbstractBakery BasicFactory = new Bakery("Basic");

    protected T CreateTestModel<T>(params Type[] implementations)
    {
        var instance = new BakeryConfiguration(ComponentGenerators.Create(implementations), Defaults.GetDefaultDefaultProvider())
            .CreateBakery("TestBakery")
            .Create<T>();

        return instance;
    }

    protected T CreateTestModel<T, I>(out I ifc, params Type[] implementations)
        where I : class
    {
        var instance = new BakeryConfiguration(ComponentGenerators.Create(implementations), Defaults.GetDefaultDefaultProvider())
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

    public interface IHasPropertiesWithCollection
    {
        IList<String> StringList { get; set; }

        ICollection<String> StringCollection { get; set; }
    }

    public interface IHasNullableProperty
    {
        String? Value { get; set; }

        void SetValueByMethod(String value) => Value = value;

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
