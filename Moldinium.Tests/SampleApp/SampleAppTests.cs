using SampleApp;
using Testing.Moldinium;

namespace Testing.SampleApp;

[TestClass]
public class SampleAppTests : MoldiniumTestsBase
{
    [TestMethod]
    public void SampleAppDependencyTest()
    {
        // just check if we can at all create it for now
        CreateTestModel<JobList>();
    }
}

