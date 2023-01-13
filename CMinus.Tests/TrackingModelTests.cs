using CMinus.Injection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Tests;

[TestClass]
public class TrackingModelTests
{
    public interface IModelInteger
    {
        Int32 Variable1 { get; set; }

        Int32 Computed1 { get { return Variable1 - 1; } set { Variable1 = value + 1; } }
    }

    IDependencyProvider provider;

    public TrackingModelTests()
    {
        provider = DependencyProvider.Create(new DefaultDependencyProviderConfiguration());
    }

    [TestMethod]
    public void ModelsFundamentalsInteger()
    {
        var model1 = provider.CreateInstance<IModelInteger>();

        var listener =  new PropertyChangeTestListener((model1 as INotifyPropertyChanged)!);

        Assert.AreEqual(0, model1.Variable1);
        Assert.AreEqual(-1, model1.Computed1);

        model1.Variable1 = 42;

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual(42, model1.Variable1);
        Assert.AreEqual(41, model1.Computed1);

        model1.Computed1 = 42;

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual(43, model1.Variable1);
        Assert.AreEqual(42, model1.Computed1);
    }
}
