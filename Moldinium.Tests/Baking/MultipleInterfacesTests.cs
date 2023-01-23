using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Moldinium.Tests.Baking;

[TestClass]
public class MultipleInterfacesTests : BakingTestsBase
{
    public interface IPerson
    {
        String Name { get; }
    }

    public interface INaturalPerson : IPerson
    {
        String FirstName { get; set; }

        String LastName { get; set; }

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
