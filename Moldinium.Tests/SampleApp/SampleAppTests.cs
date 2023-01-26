using SampleApp;
using Testing.Moldinium;

namespace Testing.SampleApp;

[TestClass]
public class SampleAppTests : MoldiniumTestsBase
{
    [DataTestMethod]
    [DataRow(MoldiniumDefaultMode.Basic)]
    [DataRow(MoldiniumDefaultMode.Tracking)]
    [DataRow(MoldiniumDefaultMode.NotifyPropertyChanged)]
    [DataRow(MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged)]
    public void SampleAppDependencyTest(MoldiniumDefaultMode mode)
    {
        // just check if we can at all create it for now

        CreateTestModel<JobList>(mode);
    }
}

