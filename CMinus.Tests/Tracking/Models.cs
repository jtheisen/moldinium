using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.Collections.Generic;

namespace CMinus.Tests;

class PropertyChangeTestListener
{
    public PropertyChangeTestListener(INotifyPropertyChanged obj)
    {
        obj.PropertyChanged += Handle;
    }

    public void AssertChangeSetAndClear(params String[] properties)
    {
        foreach (var prop in properties)
        {
            if (!propertiesChanged.Contains(prop))
                Assert.Fail($"Property {prop} unexpectedly unchanged.");
        }

        foreach (var prop in propertiesChanged)
        {
            if (!properties.Contains(prop))
                Assert.Fail($"Property {prop} unexpectedly unchanged.");
        }

        Clear();
    }

    public void AssertChanged(params String[] properties)
    {
        foreach (var prop in properties)
        {
            if (!propertiesChanged.Contains(prop))
                Assert.Fail($"Property {prop} unexpectedly unchanged.");
        }
    }

    public void AssertUnchanged(params String[] properties)
    {
        foreach (var prop in properties)
        {
            if (propertiesChanged.Contains(prop))
                Assert.Fail($"Property {prop} unexpectedly changed.");
        }
    }

    public void Clear()
    {
        propertiesChanged.Clear();
    }

    void Handle(object sender, PropertyChangedEventArgs e)
    {
        propertiesChanged.Add(e.PropertyName);
    }

    HashSet<String> propertiesChanged = new HashSet<String>();
}

[TestClass]
public class ModelsTests
{
    public abstract class Model1 : IModel
    {
        public Int32 PlainOld1 { get; set; }

        public abstract Int32 Variable1 { get; set; }

        public virtual Int32 Computed1 { get { return Variable1 - 1; } set { Variable1 = value + 1; } }
    }

    [TestMethod]
    public void ModelsFundamentals()
    {
        var model1 = Models.Create<Model1>();

        var listener = new PropertyChangeTestListener(model1 as INotifyPropertyChanged);

        Assert.AreEqual(0, model1.PlainOld1);
        Assert.AreEqual(0, model1.Variable1);
        Assert.AreEqual(-1, model1.Computed1);

        model1.Variable1 = 42;

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual(0, model1.PlainOld1);
        Assert.AreEqual(42, model1.Variable1);
        Assert.AreEqual(41, model1.Computed1);

        model1.Computed1 = 42;

        listener.AssertChangeSetAndClear("Variable1", "Computed1");

        Assert.AreEqual(0, model1.PlainOld1);
        Assert.AreEqual(43, model1.Variable1);
        Assert.AreEqual(42, model1.Computed1);

        model1.PlainOld1 = 42;

        listener.AssertChangeSetAndClear();

        Assert.AreEqual(42, model1.PlainOld1);
        Assert.AreEqual(43, model1.Variable1);
        Assert.AreEqual(42, model1.Computed1);
    }
}
