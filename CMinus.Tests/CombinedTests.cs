using CMinus.Injection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;

namespace CMinus.Tests;

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

    [TestMethod]
    public void BakedTypeTest()
    {
        var configuration = new DefaultDependencyProviderConfiguration(
            Baking: DefaultDependencyProviderBakingMode.Basic,
            BakeAbstract: false,
            EnableOldModliniumModels: true
        );

        var provider = DependencyProvider.Create(configuration);

        var bakedType = provider.ResolveType(typeof(ITodoListEntry));

        var instance = (ITodoListEntry)Activator.CreateInstance(bakedType)!;

        Assert.IsNotNull(instance);

        instance.Text = "foo";

        Assert.AreEqual("foo", instance.Text);
    }

    [TestMethod]
    public void ReactionTest()
    {
        var configuration = new DefaultDependencyProviderConfiguration(
            Baking: DefaultDependencyProviderBakingMode.Basic,
            BakeAbstract: true,
            EnableOldModliniumModels: true
        );

        var provider = DependencyProvider.Create(configuration);

        var instance = provider.CreateInstance<ITodoListEntry>();

        instance.Text = "do the dishes";

        Assert.AreEqual("do the dishes", instance.Text);

        instance.Text = "do the dishes again";

        Assert.AreEqual("do the dishes again", instance.Text);

        var changeCount = 0;

        {
            using var reaction = Watchable.React(() =>
            {
                var _ = instance.Text;

                ++changeCount;
            });

            Assert.AreEqual("do the dishes again", instance.Text);

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
