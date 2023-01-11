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
        configuration = new DefaultDependencyProviderConfiguration(
            EnableOldModliniumModels: true
        );
    }

    public interface TodoListEntry
    {
        String Text { get; set; }
    }

    //public abstract class ATodoListEntry : TodoListEntry, IModel
    //{
    //    public abstract String Text { get; set; }
    //}

    [TestMethod]
    public void InterfaceTypeWithParameterizedFactoryInstanceTest()
        => DependencyProvider.Create(configuration)
        .CreateInstance<InterfaceTypeWithParameterizedFactory>().Validate();

    [TestMethod]
    public void ReactionTest()
    {
        new ConcreteDependencyProvider(new Dependency(typeof(TodoListEntry), DependencyRuntimeMaturity.OnlyType));

        var configuration = new DefaultDependencyProviderConfiguration(
            Baking: DefaultDependencyProviderBakingMode.Basic,
            EnableOldModliniumModels: true
            //Build: b =>
            //{
            //    b.AddImplementation(typeof(TodoListEntry), typeof(ATodoListEntry));
            //}
        );

        var instance = DependencyProvider.Create(configuration)
            .CreateInstance<TodoListEntry>();

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
