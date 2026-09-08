using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections
{
    public partial class ObservableList<T> : IList<T>, IReadOnlyObservableList<T>
    {
        readonly List<T> list;
        ReentrancyGuard guard;
        public object SyncRoot { get; } = new();

        public ObservableList()
        {
            list = new List<T>();
        }

        public ObservableList(int capacity)
        {
            list = new List<T>(capacity);
        }

        public ObservableList(IEnumerable<T> collection)
        {
            list = collection.ToList();
        }

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return list[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    if (guard.RejectIfNotifying()) return;
                    var oldValue = list[index];
                    list[index] = value;
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Replace(value, oldValue, index, index));
                }
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return list.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        void NotifyCollectionChanged(in NotifyCollectionChangedEventArgs<T> args)
        {
            Rejection rejection;

            guard.BeginNotification();
            try
            {
                CollectionChanged?.Invoke(args);
            }
            finally
            {
                rejection = guard.EndNotification();
            }

            rejection.ThrowIfRejected(nameof(ObservableList<T>));
        }

        public void Add(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = list.Count;
                list.Add(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, index));
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = list.Count;
                using (var xs = new CloneCollection<T>(items))
                {
                    // to avoid iterate twice, require copy before insert.
                    list.AddRange(xs.AsEnumerable());
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void AddRange(T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = list.Count;
                list.AddRange(items);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void AddRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = list.Count; // starting index

#if NET8_0_OR_GREATER
                list.AddRange(items);
#else
                foreach (var item in items)
                {
                    list.Add(item);
                }
#endif

                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Clear();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return list.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                list.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in list)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void ForEach(Action<T> action)
        {
            lock (SyncRoot)
            {
                foreach (var item in list)
                {
                    action(item);
                }
            }
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return list.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Insert(index, item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, index));
            }
        }

        public void InsertRange(int index, T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.InsertRange(index, items);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void InsertRange(int index, IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                using (var xs = new CloneCollection<T>(items))
                {
                    list.InsertRange(index, xs.AsEnumerable());
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void InsertRange(int index, ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
#if NET8_0_OR_GREATER
                list.InsertRange(index, items);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
#else
                using (var xs = new CloneCollection<T>(items))
                {
                    list.InsertRange(index, xs.AsEnumerable());
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
#endif
            }
        }

        public bool Remove(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return false;
                var index = list.IndexOf(item);

                if (index >= 0)
                {
                    list.RemoveAt(index);
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(item, index));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void RemoveAt(int index)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var item = list[index];
                list.RemoveAt(index);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(item, index));
            }
        }

        public void RemoveRange(int index, int count)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
#pragma warning disable CS0436
                var range = CollectionsMarshal.AsSpan(list).Slice(index, count);
#pragma warning restore CS0436
                // require copy before remove
                using (var xs = new CloneCollection<T>(range))
                {
                    list.RemoveRange(index, count);
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(xs.Span, index));
                }
            }
        }

        public void Move(int oldIndex, int newIndex)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var removedItem = list[oldIndex];
                list.RemoveAt(oldIndex);
                list.Insert(newIndex, removedItem);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Move(removedItem, newIndex, oldIndex));
            }
        }

        public void Sort()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Sort();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Sort(0, list.Count, null));
            }
        }

        public void Sort(IComparer<T> comparer)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Sort(comparer);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Sort(0, list.Count, comparer));
            }
        }

        public void Sort(int index, int count, IComparer<T> comparer)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Sort(index, count, comparer);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Sort(index, count, comparer));
            }
        }

        public void Reverse()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Reverse();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reverse(0, list.Count));
            }
        }

        public void Reverse(int index, int count)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                list.Reverse(index, count);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reverse(index, count));
            }
        }
    }
}
