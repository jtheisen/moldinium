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

    public abstract class ATodoListEntry : ITodoListEntry
    {
        static String text;

        public virtual String Text { get => text; set => text = value; }
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

    [TestMethod]
    public void ManualModelReactionTest()
    {
        var instance = (ATodoListEntry)Models.Create(typeof(ATodoListEntry));

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

    public abstract class ModelBroken : IModel
    {
        String text;

        public virtual String Text { get => text; set => text = value; }

        public Int32 PlainOld1 { get; set; }

        public abstract Int32 Variable1 { get; set; }

        public virtual Int32 Computed1 { get { return Variable1 - 1; } set { Variable1 = value + 1; } }
    }

    [TestMethod]
    public void ModelsFundamentalsBroken()
    {
        var instance = Models.Create<ModelBroken>();

        instance.Text = "do the dishes";

        Assert.AreEqual("do the dishes", instance.Text);

        instance.Text = "do the dishes again";

        Assert.AreEqual("do the dishes again", instance.Text);

        Assert.AreEqual(0, instance.PlainOld1);
        Assert.AreEqual(0, instance.Variable1);
        Assert.AreEqual(-1, instance.Computed1);

        instance.Variable1 = 42;

        Assert.AreEqual(0, instance.PlainOld1);
        Assert.AreEqual(42, instance.Variable1);
        Assert.AreEqual(41, instance.Computed1);

        instance.Computed1 = 42;

        Assert.AreEqual(0, instance.PlainOld1);
        Assert.AreEqual(43, instance.Variable1);
        Assert.AreEqual(42, instance.Computed1);

        instance.PlainOld1 = 42;

        Assert.AreEqual(42, instance.PlainOld1);
        Assert.AreEqual(43, instance.Variable1);
        Assert.AreEqual(42, instance.Computed1);
    }

    public abstract class ModelString : IModel
    {
        public ModelString()
        {
            Variable1 = "";
        }

        public abstract String Text { get; set; }

        public String PlainOld1 { get; set; } = "";

        public abstract String Variable1 { get; set; }

        public virtual String Computed1 { get { return "_" + Variable1; } set { Variable1 = value.TrimStart('_'); } }
    }

    [TestMethod]
    public void ModelsFundamentalsString()
    {
        var instance = Models.Create<ModelString>();

        instance.Text = "do the dishes";

        Assert.AreEqual("do the dishes", instance.Text);

        instance.Text = "do the dishes again";

        Assert.AreEqual("do the dishes again", instance.Text);

        instance.Text = "";

        var listener = new PropertyChangeTestListener(instance as INotifyPropertyChanged);

        Assert.AreEqual("", instance.PlainOld1);
        Assert.AreEqual("", instance.Variable1);
        Assert.AreEqual("_", instance.Computed1);

        instance.Variable1 = "foo";

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual("", instance.PlainOld1);
        Assert.AreEqual("foo", instance.Variable1);
        Assert.AreEqual("_foo", instance.Computed1);

        instance.Computed1 = "_bar";

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual("", instance.PlainOld1);
        Assert.AreEqual("bar", instance.Variable1);
        Assert.AreEqual("_bar", instance.Computed1);

        instance.PlainOld1 = "bar";

        listener.AssertChangeSetAndClear();

        Assert.AreEqual("bar", instance.PlainOld1);
        Assert.AreEqual("bar", instance.Variable1);
        Assert.AreEqual("_bar", instance.Computed1);
    }

}
