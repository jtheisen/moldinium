using Moldinium.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace Moldinium.Tests;

[TestClass]
public class CombinedTests
{
    DefaultDependencyProviderConfiguration configuration;

    public CombinedTests()
    {
        configuration = new DefaultDependencyProviderConfiguration(
            EnableOldModliniumModels: true
        );
    }

    public interface ITodoListEntry
    {
        String Text { get; set; }
    }

    [TestMethod]
    public void InterfaceTypeWithParameterizedFactoryInstanceTest()
        => DependencyProvider.Create(configuration)
        .CreateInstance<InterfaceTypeWithParameterizedFactory>().Validate();

    IDependencyProvider GetProvider() => DependencyProvider.Create(
        new DefaultDependencyProviderConfiguration(
            Baking: DefaultDependencyProviderBakingMode.Tracking
        )
    );

    [TestMethod]
    public void BakedTypeTest()
    {
        var provider = GetProvider();

        var bakedType = provider.ResolveType(typeof(ITodoListEntry));

        var instance = (ITodoListEntry)Activator.CreateInstance(bakedType)!;

        Assert.IsNotNull(instance);

        instance.Text = "foo";

        Assert.AreEqual("foo", instance.Text);
    }

    [TestMethod]
    public void ReactionTest()
    {
        var provider = GetProvider();

        var instance = provider.CreateInstance<ITodoListEntry>();

        instance.Text = "wash the car";

        Assert.AreEqual("wash the car", instance.Text);

        instance.Text = "do the dishes";

        Assert.AreEqual("do the dishes", instance.Text);

        var changeCount = 0;

        {
            using var reaction = Watchable.React(() =>
            {
                var _ = instance.Text;

                ++changeCount;
            });

            Assert.AreEqual("do the dishes", instance.Text);

            Assert.AreEqual(1, changeCount);

            instance.Text = "take out the trash";

            Assert.AreEqual("take out the trash", instance.Text);

            Assert.AreEqual(2, changeCount);
        }

        instance.Text = "get the kids to bed";

        Assert.AreEqual("get the kids to bed", instance.Text);

        Assert.AreEqual(2, changeCount);
    }
}
