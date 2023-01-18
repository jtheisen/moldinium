using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests.Baking;

[TestClass]
public class MultipleInterfacesTests : BakingTestsBase
{
    public interface IPerson
    {
        public String Name { get; set; }
    }

    public interface INaturalPerson : IPerson
    {
        public Int32 Age { get; set; }
    }

    [TestMethod]
    public void AdditionalBaseInterfaceTest()
    {
        var person = CreateTestModel<INaturalPerson>();

        person.Name = "Gilgamesh";
    }
}
