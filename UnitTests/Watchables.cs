﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IronStone.Moldinium.UnitTests
{
    [TestClass]
    public class WatchablesTests
    {
        [TestMethod]
        public void Fundamentals()
        {
            var foo = Watchable.Var(42);

            Assert.AreEqual(42, foo.Value);

            foo.Value = 43;

            Assert.AreEqual(43, foo.Value);

            var bar = Watchable.React(() => foo.Value);

            Assert.AreEqual(43, bar.Value);

            foo.Value = 44;

            Assert.AreEqual(44, foo.Value);
            Assert.AreEqual(44, bar.Value);
        }

        [TestMethod]
        public void Caching()
        {
            var counter = new Counter();

            var foo = Watchable.Eval(() => counter.Get(42));

            Assert.AreEqual(0, counter.count);

            // Only alert watchables can cache, as they're the only
            // ones who know when they become dirty.
            using (Watchable.React(() => foo.Value))
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

            var foo = Watchable.Var(42);

            var bar = Watchable.Eval(() => counter.Get(42) + foo.Value);

            // Unwatched evaluations are lazy.
            Assert.AreEqual(0, counter.count);

            Ignore(bar.Value);

            Assert.AreEqual(1, counter.count);

            foo.Value = 43;

            // Unwatched evaluations are still lazy.
            Assert.AreEqual(1, counter.count);

            var reaction = Watchable.React(() => Ignore(bar.Value));

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
            var shouldThrow = Watchable.Var(true);

            var throwing = Watchable.Eval(() => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

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
            var shouldThrow = Watchable.Var("shouldThrow", true);

            var throwing = Watchable.Eval("throwing", () => { if (shouldThrow.Value) throw new InvalidOperationException(); else return 0; });

            var relay = Watchable.Eval("relay", () => throwing.Value);

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
}
