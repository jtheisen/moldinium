using System.Windows.Input;

namespace Testing.Baking;

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


    public interface Command : System.Windows.Input.ICommand
    {
        event EventHandler? System.Windows.Input.ICommand.CanExecuteChanged { add { } remove { } }

        new Boolean CanExecute(Object? parameter = null) => false;

        new void Execute(Object? parameter = null) => throw new NotImplementedException();

        Boolean System.Windows.Input.ICommand.CanExecute(Object? parameter) => true;

        void System.Windows.Input.ICommand.Execute(Object? parameter) { }
    }

    // Hmm, not sure if we can do that at all
    //[TestMethod]
    //public void CommandMethodReplacementTest()
    //{
    //    var command = CreateTestModel<Command, ICommand>(out var icommand);

    //    Assert.IsFalse(command.CanExecute());
    //    Assert.IsTrue(icommand.CanExecute(null));
    //    Assert.ThrowsException<NotImplementedException>(() => command.Execute());
    //    icommand.Execute(null);
    //}

}
