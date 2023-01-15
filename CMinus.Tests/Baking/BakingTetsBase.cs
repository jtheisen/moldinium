using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests.Baking;

public class BakingTetsBase
{
    protected static readonly AbstractBakery BasicFactory = new Bakery("Basic");

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
