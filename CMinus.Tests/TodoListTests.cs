using CMinus.Injection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace CMinus.Tests
{
    public interface ITodoList
    {
        Func<ITodoList, ITodoItem> CreateItem { get; init; }

        List<ITodoItem> Items { get; set; } // FIXME

        void Init()
        {
            Items = new List<ITodoItem>(); // FIXME
        }

        ITodoItem AddItem(String text)
        {
            var item = CreateItem(this);

            item.Text = text;

            Items.Add(item);

            return item;
        }

        void RemoveItem(ITodoItem item) => Items.Remove(item);
    }

    public interface ITodoItem
    {
        ITodoList Owner { get; init; }

        String Text { get; set; }

        void Remove() => Owner.RemoveItem(this);
    }

    [TestClass]
    public class TodoListTests
    {
        IDependencyProvider provider;

        public TodoListTests()
        {
            provider = new CombinedDependencyProvider(
                new AcceptingDefaultConstructiblesDependencyProvider(), // We really should only allow "baked" types to be blindly constructed
                new BakeryDependencyProvider(new Bakery("TestBakery")),
                new FactoryDependencyProvider(),
                new ActivatorDependencyProvider(),
                new InitSetterDependencyProvider()
            );
        }

        [TestMethod]
        public void TestTodoList()
        {
            var todoList = provider.CreateInstance<ITodoList>();

            todoList.Init();

            Assert.AreEqual(0, todoList.Items.Count);

            var dishesItem = todoList.AddItem("do the dishes");

            Assert.AreEqual(1, todoList.Items.Count);

            dishesItem.Remove();

            Assert.AreEqual(0, todoList.Items.Count);
        }
    }
}
