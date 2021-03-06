﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace IronStone.Moldinium.UnitTests
{
    class CollectionChangeTestListener<T>
    {
        NotifyCollectionChangedEventArgs lastEventArgs;

        public CollectionChangeTestListener(INotifyCollectionChanged obj)
        {
            obj.CollectionChanged += Handle;
        }

        private void Handle(Object sender, NotifyCollectionChangedEventArgs e)
        {
            this.lastEventArgs = e;
        }

        public void AssertChangedAndClear(NotifyCollectionChangedAction action, T[] newItems, T[] oldItems, Int32 newIndex, Int32 oldIndex)
        {
            if (null == lastEventArgs)
                Assert.Fail("List unexpectedly unchanged.");

            Assert.AreEqual(newIndex, lastEventArgs.NewStartingIndex);
            Assert.AreEqual(oldIndex, lastEventArgs.OldStartingIndex);
            CollectionAssert.AreEqual(newItems, lastEventArgs.NewItems);
            CollectionAssert.AreEqual(oldItems, lastEventArgs.OldItems);

            lastEventArgs = null;
        }

        public void AssertUnchanged()
        {
            if (null != lastEventArgs)
                Assert.Fail("List unexpectedly changed.");
        }
    }

    [TestClass]
    public class WatchableListTests
    {
        [TestMethod]
        public void CollectionChanged()
        {
            var list = new WatchableList<Int32>(new[] { 1, 2, 3 });

            var listener = new CollectionChangeTestListener<Int32>(list);

            listener.AssertUnchanged();

            list[1] = 42;

            listener.AssertChangedAndClear(NotifyCollectionChangedAction.Replace, new[] { 42 }, new[] { 2 }, 1, 1);

            list.Add(17);

            listener.AssertChangedAndClear(NotifyCollectionChangedAction.Add, new[] { 17 }, null, 3, -1);

            list.Remove(17);

            listener.AssertChangedAndClear(NotifyCollectionChangedAction.Remove, null, new[] { 17 }, -1, 3);

            list.Clear();

            listener.AssertChangedAndClear(NotifyCollectionChangedAction.Reset, null, null, -1, -1);
        }
    }
}
