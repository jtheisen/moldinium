using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests.Baking;

[TestClass]
public class MultipleInterfacesTests : BakingTestsBase
{
    public interface IPerson
    {
        public String Name { get; }
    }

    public interface INaturalPerson : IPerson
    {
        public String FirstName { get; set; }

        public String LastName { get; set; }

        String IPerson.Name => $"{FirstName} {LastName}";
    }

    [TestMethod]
    public void AdditionalBaseInterfaceTest()
    {
        var person = CreateTestModel<INaturalPerson>();

        person.FirstName = "Peer";
        person.LastName = "Nullman";

        Assert.AreEqual("Peer Nullman", person.Name);
    }
}
