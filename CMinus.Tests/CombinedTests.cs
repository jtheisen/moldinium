using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CMinus.Tests;

[TestClass]
public class CombinedTests
{
    DefaultDependencyProviderConfiguration configuration;

    public CombinedTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new RootService());

        configuration = new DefaultDependencyProviderConfiguration(
            EnableOldModliniumModels: true,
            Services: services.BuildServiceProvider()
        );
    }

    public interface TodoListEntry
    {
        String Text { get; set; }
    }

    [TestMethod]
    public void InterfaceTypeWithParameterizedFactoryInstanceTest()
        => DependencyProvider.Create(configuration)
        .CreateInstance<InterfaceTypeWithParameterizedFactory>().Validate();

    [TestMethod]
    public void ReactionTest()
    {
        var instance = DependencyProvider.Create(configuration)
            .CreateInstance<TodoListEntry>();

        instance.Text = "do the dishes";

        Assert.AreEqual("do the dishes", instance.Text);

        var changeCount = 0;

        {
            using var reaction = Watchable.React(() =>
            {
                ++changeCount;
            });

            Assert.AreEqual("do the dishes", instance.Text);

            instance.Text = "take out the trash";

            Assert.AreEqual("take out the trash", instance.Text);

            Assert.AreEqual(1, changeCount);
        }

        instance.Text = "get the kids to bed";

        Assert.AreEqual("get the kids to bed", instance.Text);

        Assert.AreEqual(1, changeCount);
    }
}
