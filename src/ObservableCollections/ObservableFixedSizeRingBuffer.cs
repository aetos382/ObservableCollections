using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ObservableCollections
{
    public partial class ObservableFixedSizeRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        readonly RingBuffer<T> buffer;
        readonly int capacity;
        ReentrancyGuard guard;

        /// <inheritdoc />
        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        void NotifyCollectionChanged(in NotifyCollectionChangedEventArgs<T> args)
        {
            bool rejected;
            string? rejectedMember;

            guard.BeginNotification();
            try
            {
                CollectionChanged?.Invoke(args);
            }
            finally
            {
                rejected = guard.EndNotification(out rejectedMember);
            }

            if (rejected)
            {
                ReentrancyGuard.ThrowRejectedChange(nameof(ObservableFixedSizeRingBuffer<T>), rejectedMember);
            }
        }

        public ObservableFixedSizeRingBuffer(int capacity)
        {
            this.capacity = capacity;
            this.buffer = new RingBuffer<T>(capacity);
        }

        public ObservableFixedSizeRingBuffer(int capacity, IEnumerable<T> collection)
        {
            this.capacity = capacity;
            this.buffer = new RingBuffer<T>(capacity);
            foreach (var item in collection)
            {
                if (capacity == buffer.Count)
                {
                    buffer.RemoveFirst();
                }
                buffer.AddLast(item);
            }
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

        public int Capacity => capacity;

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
                if (capacity == buffer.Count)
                {
                    var remItem = buffer.RemoveLast();
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(remItem, capacity - 1));
                }

                buffer.AddFirst(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, 0));
            }
        }

        public void AddLast(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                if (capacity == buffer.Count)
                {
                    var remItem = buffer.RemoveFirst();
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(remItem, 0));
                }

                buffer.AddLast(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, buffer.Count - 1));
            }
        }

        public T RemoveFirst()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableFixedSizeRingBuffer<T>));
                var item = buffer.RemoveFirst();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(item, 0));
                return item;
            }
        }

        public T RemoveLast()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableFixedSizeRingBuffer<T>));
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
                using (var xs = new CloneCollection<T>(items))
                {
                    if (capacity <= buffer.Count + xs.Span.Length)
                    {
                        // calc remove count
                        var remCount = Math.Min(buffer.Count, buffer.Count + xs.Span.Length - capacity);
                        using (var ys = new ResizableArray<T>(remCount))
                        {
                            for (int i = 0; i < remCount; i++)
                            {
                                ys.Add(buffer.RemoveFirst());
                            }

                            NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(ys.Span, 0));
                        }
                    }

                    var index = buffer.Count;
                    var span = xs.Span;
                    if (span.Length > capacity)
                    {
                        span = span.Slice(span.Length - capacity);
                    }

                    foreach (var item in span)
                    {
                        buffer.AddLast(item);
                    }
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(span, index));
                }
            }
        }

        public void AddLastRange(T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                if (capacity <= buffer.Count + items.Length)
                {
                    // calc remove count
                    var remCount = Math.Min(buffer.Count, buffer.Count + items.Length - capacity);
                    using (var ys = new ResizableArray<T>(remCount))
                    {
                        for (int i = 0; i < remCount; i++)
                        {
                            ys.Add(buffer.RemoveFirst());
                        }

                        NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(ys.Span, 0));
                    }
                }

                var index = buffer.Count;
                var span = items.AsSpan();
                if (span.Length > capacity)
                {
                    span = span.Slice(span.Length - capacity);
                }

                foreach (var item in span)
                {
                    buffer.AddLast(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(span, index));
            }
        }

        public void AddLastRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                if (capacity <= buffer.Count + items.Length)
                {
                    // calc remove count
                    var remCount = Math.Min(buffer.Count, buffer.Count + items.Length - capacity);
                    using (var ys = new ResizableArray<T>(remCount))
                    {
                        for (int i = 0; i < remCount; i++)
                        {
                            ys.Add(buffer.RemoveFirst());
                        }

                        NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(ys.Span, 0));
                    }
                }

                var index = buffer.Count;
                var span = items;
                if (span.Length > capacity)
                {
                    span = span.Slice(span.Length - capacity);
                }

                foreach (var item in span)
                {
                    buffer.AddLast(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(span, index));
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

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform)
        {
            return new ObservableRingBuffer<T>.View<TView>(this, transform);
        }
    }
}
