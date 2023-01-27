using Moldinium.Internals;

namespace Testing.Moldinium;

public class MoldiniumTestsBase
{
    protected T CreateTestModel<T>(Action<MoldiniumConfigurationBuilder> build)
        where T : class
    {
        var builder = new MoldiniumConfigurationBuilder();
        build(builder);
        var configuration = builder.Build();
        var provider = DependencyProvider.Create(configuration);

        return CreateTestModel<T>(provider);
    }

    protected T CreateTestModel<T>(MoldiniumDefaultMode mode = MoldiniumDefaultMode.TrackingAndNotifyPropertyChanged, Boolean logReport = false)
    {
        var config = new DefaultDependencyProviderConfiguration(mode) { IsMoldiniumType = t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I") };

        var provider = DependencyProvider.Create(config);

        return CreateTestModel<T>(provider);
    }

    protected T CreateTestModel<T>(IDependencyProvider provider, Boolean logReport = false)
    {
        if (logReport)
        {
            Console.WriteLine(provider.CreateReport(typeof(T)));
        }

        var instance = provider.CreateInstance<T>();

        return instance;
    }
}
