using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Moldinium.Tests;

[TestClass]
public class TrackablesTests
{
    [TestMethod]
    public void Fundamentals()
    {
        var foo = Trackable.Var(42);

        Assert.AreEqual(42, foo.Value);

        foo.Value = 43;

        Assert.AreEqual(43, foo.Value);

        var bar = Trackable.React(() => foo.Value);

        Assert.AreEqual(43, bar.Value);

        foo.Value = 44;

        Assert.AreEqual(44, foo.Value);
        Assert.AreEqual(44, bar.Value);
    }

    [TestMethod]
    public void Caching()
    {
        var counter = new Counter();

        var foo = Trackable.Eval(() => counter.Get(42));

        Assert.AreEqual(0, counter.count);

        // Only alert trackables can cache, as they're the only
        // ones who know when they become dirty.
        using (Trackable.React(() => foo.Value))
        {
            Assert.AreEqual(1, counter.count);

            Ignore(foo.Value);

            // FIXME
            // We're getting a spurious second evaluation. This is because
            // the first evaluation still happened while relaxing, although
            // it theoretically could be trusted to be the correct one
            // after the computation becomes active shortly after.
            Assert.AreEqual(2, counter.count);

            Ignore(foo.Value);

            Assert.AreEqual(2, counter.count);
        }
    }

    [TestMethod]
    public void Eagerness()
    {
        var counter = new Counter();

        var foo = Trackable.Var(42);

        var bar = Trackable.Eval(() => counter.Get(42) + foo.Value);

        // Untracked evaluations are lazy.
        Assert.AreEqual(0, counter.count);

        Ignore(bar.Value);

        Assert.AreEqual(1, counter.count);

        foo.Value = 43;

        // Untracked evaluations are still lazy.
        Assert.AreEqual(1, counter.count);

        var reaction = Trackable.React(() => Ignore(bar.Value));

        Assert.AreEqual(2, counter.count);

        foo.Value = 43;

        Assert.AreEqual(3, counter.count);

        reaction.Dispose();

        foo.Value = 44;

        Assert.AreEqual(3, counter.count);
    }

    [TestMethod]
    public void Exceptions()
    {
        var shouldThrow = Trackable.Var(true);

        var throwing = Trackable.Eval(() => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

        AssertThrows(() => { Ignore(throwing.Value); }, typeof(InvalidOperationException));

        shouldThrow.Value = false;

        Assert.AreEqual(0, throwing.Value);

        shouldThrow.Value = true;

        // This subscription wakes up throwing.
        using (throwing.Subscribe(name: "keep alive"))
        {
            AssertThrows(() => { Ignore(throwing.Value); }, typeof(InvalidOperationException));

            shouldThrow.Value = false;

            Assert.AreEqual(0, throwing.Value);
        }
    }

    [TestMethod]
    public void ExceptionsIndirect()
    {
        var shouldThrow = Trackable.Var("shouldThrow", true);

        var throwing = Trackable.Eval("throwing", () => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

        var relay = Trackable.Eval("relay", () => throwing.Value);

        AssertThrows(() => { Ignore(relay.Value); }, typeof(InvalidOperationException));

        shouldThrow.Value = false;

        Assert.AreEqual(0, relay.Value);

        shouldThrow.Value = true;

        // This subscription wakes up the whole chain, first throwing, then relay.
        using (relay.Subscribe(name: "keep alive"))
        {
            AssertThrows(() => { Ignore(relay.Value); }, typeof(InvalidOperationException));

            shouldThrow.Value = false;

            Assert.AreEqual(0, relay.Value);
        }
    }

    struct Counter
    {
        public int count;

        public T Get<T>(T t)
        {
            ++count;
            return t;
        }
    }

    static void Ignore(Object dummy) { }

    static void AssertThrows(Action action, Type exceptionType)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Assert.IsInstanceOfType(ex, exceptionType);

            return;
        }

        Assert.Fail("Unexpectedly no exception.");
    }
}
