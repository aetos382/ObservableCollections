using ObservableCollections.Internal;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace ObservableCollections
{
    public partial class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        readonly RingBuffer<T> buffer;
        ReentrancyGuard guard;

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

            rejection.ThrowIfRejected(nameof(ObservableRingBuffer<T>));
        }

        public ObservableRingBuffer()
        {
            this.buffer = new RingBuffer<T>();
        }

        public ObservableRingBuffer(IEnumerable<T> collection)
        {
            this.buffer = new RingBuffer<T>(collection);
        }

        public bool IsReadOnly => false;

        public object SyncRoot { get; } = new object();

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return this.buffer[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    if (guard.RejectIfNotifying()) return;
                    var oldValue = buffer[index];
                    buffer[index] = value;
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
                    return buffer.Count;
                }
            }
        }

        public void AddFirst(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                buffer.AddFirst(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, 0));
            }
        }

        public void AddLast(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                buffer.AddLast(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, buffer.Count - 1));
            }
        }

        public T RemoveFirst()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableRingBuffer<T>));
                var item = buffer.RemoveFirst();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(item, 0));
                return item;
            }
        }

        public T RemoveLast()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableRingBuffer<T>));
                var index = buffer.Count - 1;
                var item = buffer.RemoveLast();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(item, index));
                return item;
            }
        }

        // AddFirstRange is not exists.

        public void AddLastRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = buffer.Count;
                using (var xs = new CloneCollection<T>(items))
                {
                    foreach (var item in xs.Span)
                    {
                        buffer.AddLast(item);
                    }
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void AddLastRange(T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = buffer.Count;
                foreach (var item in items)
                {
                    buffer.AddLast(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void AddLastRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = buffer.Count;
                foreach (var item in items)
                {
                    buffer.AddLast(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return buffer.IndexOf(item);
            }
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Add(T item)
        {
            AddLast(item);
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                buffer.Clear();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return buffer.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                buffer.CopyTo(array, arrayIndex);
            }
        }

        public T[] ToArray()
        {
            lock (SyncRoot)
            {
                return buffer.ToArray();
            }
        }

        public int BinarySearch(T item)
        {
            lock (SyncRoot)
            {
                return buffer.BinarySearch(item);
            }
        }

        public int BinarySearch(T item, IComparer<T> comparer)
        {
            lock (SyncRoot)
            {
                return buffer.BinarySearch(item, comparer);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in buffer)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
