using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Moldinium;

public class WatchableList<T> : INotifyCollectionChanged, IList<T>, ICollection<T>, ICollection, IReadOnlyList<T>, IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
{
    List<T> items;
    ConcreteWatchable watchable = new ConcreteWatchable();

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchableList{T}"/> class that
    /// is empty and has the default initial capacity.
    /// </summary>
    public WatchableList()
    {
        items = new List<T>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchableList{T}"/> class that
    /// is empty and has the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The number of elements that the new list can initially store.</param>
    public WatchableList(Int32 capacity)
    {
        items = new List<T>(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchableList{T}"/> class that
    /// contains elements copied from the specified collection and has sufficient capacity
    /// to accommodate the number of elements copied.
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the new list.</param>
    public WatchableList(IEnumerable<T> collection)
    {
        items = new List<T>(collection.Count());
        InsertRange(0, collection);
    }

    /// <summary>
    /// Gets or sets the total number of elements the internal data structure can hold
    /// without resizing.
    /// </summary>
    public int Capacity { get { return items.Capacity; } set { items.Capacity = value; } }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="System.Collections.Generic.ICollection{T}" />.
    /// </summary>
    public Int32 Count { get { OnEvaluated(); return items.Count; } }

    /// <summary>
    /// Adds an object to the end of the <see cref="System.Collections.Generic.ICollection{T}" />.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="System.Collections.Generic.ICollection{T}" />.
    /// The value must not be null or equal to any item already in the list.</param>
    public void Add(T item)
    {
        Insert(items.Count, item);
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the <see cref="WatchableList{T}"/>.
    /// </summary>
    /// <param name="collection">The collection whose elements should be added to the end of the
    /// <see cref="WatchableList{T}"/>. The collection must not be null and must
    /// not contain null elements, duplicates, or elements that are already in the list.</param>
    public void AddRange(IEnumerable<T> collection)
    {
        InsertRange(items.Count, collection);
    }

    /// <summary>
    /// Returns a read-only <see cref="System.Collections.Generic.IList{T}" /> wrapper for the current collection.
    /// </summary>
    /// <returns>A <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}" /> that acts as a read-only wrapper. </returns>
    public ReadOnlyCollection<T> AsReadOnly()
    {
        return items.AsReadOnly();
    }

    /// <summary>
    /// Searches the entire sorted <see cref="WatchableList{T}" /> for an element using
    /// the default comparer and returns the zero-based index of the element.
    /// </summary>
    /// <param name="item">The object to locate.</param>
    /// <returns>The zero-based index of item in the sorted <see cref="WatchableList{T}" />,
    /// if item is found; otherwise, a negative number that is the bitwise complement
    /// of the index of the next element that is larger than item or, if there is no
    /// larger element, the bitwise complement of <see cref="WatchableList{T}.Count"/>.</returns>
    public int BinarySearch(T item)
    {
        OnEvaluated();
        return items.BinarySearch(item);
    }

    /// <summary>
    /// Searches the entire sorted <see cref="WatchableList{T}" /> for an element using
    /// the specified comparer and returns the zero-based index of the element.
    /// </summary>
    /// <param name="item">The object to locate.</param>
    /// <param name="comparer">The <see cref="System.Collections.Generic.IComparer{T}" /> implementation to use when comparing
    /// elements, or null to use the default comparer <see cref="System.Collections.Generic.Comparer{T}" />.Default.</param>
    /// <returns>
    /// The zero-based index of item in the sorted <see cref="WatchableList{T}" />,
    /// if item is found; otherwise, a negative number that is the bitwise complement
    /// of the index of the next element that is larger than item or, if there is no
    /// larger element, the bitwise complement of <see cref="WatchableList{T}.Count" />.
    /// </returns>
    public int BinarySearch(T item, IComparer<T> comparer)
    {
        OnEvaluated();
        return items.BinarySearch(item, comparer);
    }

    /// <summary>
    /// Searches the entire sorted <see cref="WatchableList{T}" /> for an element using
    /// the specified comparer and returns the zero-based index of the element.
    /// </summary>
    /// <param name="index">The zero-based starting index of the range to search.</param>
    /// <param name="count">The length of the range to search.</param>
    /// <param name="item">The object to locate.</param>
    /// <param name="comparer">The <see cref="System.Collections.Generic.IComparer{T}" /> implementation to use when comparing
    /// elements, or null to use the default comparer <see cref="System.Collections.Generic.Comparer{T}" />.Default.</param>
    /// <returns>
    /// The zero-based index of item in the sorted <see cref="WatchableList{T}" />,
    /// if item is found; otherwise, a negative number that is the bitwise complement
    /// of the index of the next element that is larger than item or, if there is no
    /// larger element, the bitwise complement of <see cref="WatchableList{T}.Count" />.
    /// </returns>
    public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
    {
        OnEvaluated();
        return items.BinarySearch(index, count, item, comparer);
    }

    // ** Core method **        
    /// <summary>
    /// Removes all items from the <see cref="System.Collections.Generic.ICollection{T}" />.
    /// </summary>
    public void Clear()
    {
        items.Clear();
        watchable.Notify();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Determines whether the <see cref="System.Collections.Generic.ICollection{T}" /> contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="System.Collections.Generic.ICollection{T}" />.</param>
    /// <returns>
    /// true if <paramref name="item" /> is found in the <see cref="System.Collections.Generic.ICollection{T}" />; otherwise, false.
    /// </returns>
    public bool Contains(T item) { OnEvaluated(); return items.Contains(item); }

    /// <summary>
    /// Copies the entire <see cref="WatchableList{T}"/> to a compatible one-dimensional
    /// array, starting at the beginning of the target array.
    /// </summary>
    /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
    /// from <see cref="WatchableList{T}"/>. The System.Array must have zero-based indexing.</param>
    public void CopyTo(T[] array) { OnEvaluated(); items.CopyTo(array); }

    /// <summary>
    /// Copies the entire <see cref="WatchableList{T}" /> to a compatible one-dimensional
    /// array, starting at the specified index of the target array.
    /// </summary>
    /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
    /// from <see cref="WatchableList{T}" />. The System.Array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex) { OnEvaluated(); items.CopyTo(array, arrayIndex); }

    /// <summary>
    /// Copies the entire <see cref="WatchableList{T}" /> to a compatible one-dimensional
    /// array, starting at the specified index of the target array.
    /// </summary>
    /// <param name="index">The zero-based index in the source <see cref="WatchableList{T}" /> at which
    /// copying begins.</param>
    /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied
    /// from <see cref="WatchableList{T}" />. The System.Array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
    /// <param name="count">The number of elements to copy.</param>
    public void CopyTo(int index, T[] array, int arrayIndex, int count) { OnEvaluated(); items.CopyTo(index, array, arrayIndex, count); }

    /// <summary>
    /// Determines whether the <see cref="WatchableList{T}" /> contains elements that
    /// match the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the elements to
    /// search for.</param>
    /// <returns>true if the <see cref="WatchableList{T}" /> contains one or more elements that
    /// match the conditions defined by the specified predicate; otherwise, false.</returns>
    public bool Exists(Predicate<T> match) { OnEvaluated(); return items.Exists(match); }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified
    /// predicate, and returns the first occurrence within the entire <see cref="WatchableList{T}" />.
    /// </summary>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
    /// search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate,
    ///  if found; otherwise, the default value for type T.</returns>
    public T? Find(Predicate<T> match) { OnEvaluated(); return items.Find(match); }

    /// <summary>
    /// Retrieves all the elements that match the conditions defined by the specified
    /// predicate.
    /// </summary>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
    /// search for.</param>
    /// <returns>A <see cref="System.Collections.Generic.List{T}" /> containing all the elements that match the
    /// conditions defined by the specified predicate, if found; otherwise, an empty
    /// <see cref="System.Collections.Generic.List{T}" />.</returns>
    public List<T> FindAll(Predicate<T> match) { OnEvaluated(); return items.FindAll(match); }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified
    /// predicate, and returns the zero-based index of the first occurrence within the
    /// entire <see cref="WatchableList{T}" />.
    /// </summary>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
    /// search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions
    /// defined by match, if found; otherwise, –1.</returns>
    public int FindIndex(Predicate<T> match) { OnEvaluated(); return items.FindIndex(match); }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified
    /// predicate, and returns the zero-based index of the first occurrence within the
    /// range of elements in the <see cref="WatchableList{T}" /> that extends from
    /// the specified index to the last element.
    /// </summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
    /// search for.</param>
    /// <returns>
    /// The zero-based index of the first occurrence of an element that matches the conditions
    /// defined by match, if found; otherwise, –1.
    /// </returns>
    public int FindIndex(int startIndex, Predicate<T> match) { OnEvaluated(); return items.FindIndex(startIndex, match); }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified
    /// predicate, and returns the zero-based index of the first occurrence within the
    /// range of elements in the <see cref="WatchableList{T}" /> that extends from
    /// the specified index and contains the specified number of elements.
    /// </summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="match">The <see cref="System.Predicate{T}" /> delegate that defines the conditions of the element to
    /// search for.</param>
    /// <returns>
    /// The zero-based index of the first occurrence of an element that matches the conditions
    /// defined by match, if found; otherwise, –1.
    /// </returns>
    public int FindIndex(int startIndex, int count, Predicate<T> match) { OnEvaluated(); return items.FindIndex(startIndex, count, match); }

    /// <summary>
    /// Removes the first occurrence of a specific object from the <see cref="System.Collections.Generic.ICollection{T}" />.
    /// </summary>
    /// <param name="item">The object to remove from the <see cref="System.Collections.Generic.ICollection{T}" />.</param>
    /// <returns>
    /// true if <paramref name="item" /> was successfully removed from the <see <see cref="cref="System.Collections.Generic.ICollection{T}" />" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection{T}" />.
    /// </returns>
    public bool Remove(T item)
    {
        OnEvaluated();
        var index = items.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Determines the index of a specific item in the <see cref="System.Collections.Generic.IList{T}" />.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="System.Collections.Generic.IList{T}" />.</param>
    /// <returns>
    /// The index of <paramref name="item" /> if found in the list; otherwise, -1.
    /// </returns>
    public int IndexOf(T item) { OnEvaluated(); return items.IndexOf(item); }

    // ** Core method **
    /// <summary>
    /// Removes the <see cref="System.Collections.Generic.IList{T}" /> item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    public void RemoveAt(int index)
    {
        var removed = items[index];
        items.RemoveAt(index);
        watchable.Notify();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
    }

    /// <summary>
    /// Removes the last item in the list.
    /// </summary>
    public void RemoveLast()
    {
        RemoveAt(items.Count - 1);
    }

    /// <summary>
    /// Gets or sets the <see cref="T"/> at the specified index.
    /// </summary>
    /// <value>
    /// The <see cref="T"/>.
    /// </value>
    /// <param name="index">The index.</param>
    public T this[int index]
    {
        get { OnEvaluated(); return items[index]; }
        // ** Core method **
        set
        {
            var removed = items[index];
            items[index] = value;
            watchable.Notify();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, removed, index));
        }
    }

    // ** Core method **        
    /// <summary>
    /// Inserts an item to the <see cref="System.Collections.Generic.IList{T}" /> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
    /// <param name="item">The object to insert into the <see cref="System.Collections.Generic.IList{T}" />.</param>
    public void Insert(int index, T item)
    {
        items.Insert(index, item);
        watchable.Notify();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    /// <summary>
    /// Inserts the elements of a collection into the <see cref="WatchableList{T}" />
    /// </summary>
    /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
    /// <param name="collection">The collection whose elements should be inserted into the <see cref="WatchableList{T}" />.
    /// The collection itself cannot be null, but it can contain elements that are null,
    /// if type T is a reference type.</param>
    public void InsertRange(int index, IEnumerable<T> collection)
    {
        var i = index;
        foreach (var item in collection)
            Insert(i++, item);
    }

    /// <summary>
    /// Occurs when the collection changes.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    protected virtual void OnEvaluated() {
        Repository.Instance.NoteEvaluation(watchable);
    }

    Boolean ICollection<T>.IsReadOnly { get { return true; } }

    Int32 ICollection.Count { get { return Count; } }

    Object ICollection.SyncRoot { get { return this; } }

    bool ICollection.IsSynchronized { get { return false; } }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() { OnEvaluated(); return items.GetEnumerator(); }

    IEnumerator IEnumerable.GetEnumerator() { OnEvaluated(); return items.GetEnumerator(); }

    void ICollection.CopyTo(Array array, int index)
    {
        OnEvaluated();
        (items as ICollection).CopyTo(array, index);
    }

    // Missing List<T> methods:

    // ConvertAll
    // FindLast
    // FindLastIndex (3x)
    // ForEach
    // GetRange
    // RemoveAll
    // RemoveRange
    // Reverse (2x)
    // Sort (4x)
    // TrimExcess
    // TrueForAll

    // LastIndexOf: makes no sense here

    // To correct ghost-doc-screwups: Replace ...
    // To correct copy-pasted documentation: Replace \b([^ ]*?)`1\b with <see cref="$1{T}" />
}
