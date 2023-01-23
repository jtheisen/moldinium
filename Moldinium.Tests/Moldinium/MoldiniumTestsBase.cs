using Moldinium.Injection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Moldinium.Tests.Moldinium;

public class MoldiniumTestsBase
{
    protected T CreateTestModel<T>(DefaultDependencyProviderBakingMode mode = DefaultDependencyProviderBakingMode.TrackingAndNotifyPropertyChanged, Boolean logReport = false)
    {
        var config = new DefaultDependencyProviderConfiguration(mode) { IsMoldiniumType = t => t.IsInterface && !t.Name.StartsWithFollowedByCapital("I") };

        var provider = DependencyProvider.Create(config);

        if (logReport)
        {
            Console.WriteLine(provider.CreateReport(typeof(T)));
        }

        var instance = provider.CreateInstance<T>();

        return instance;
    }
}
