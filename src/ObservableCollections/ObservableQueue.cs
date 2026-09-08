using ObservableCollections.Internal;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public partial class ObservableQueue<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        readonly Queue<T> queue;
        ReentrancyGuard guard;
        public object SyncRoot { get; } = new object();

        public ObservableQueue()
        {
            this.queue = new Queue<T>();
        }

        public ObservableQueue(int capacity)
        {
            this.queue = new Queue<T>(capacity);
        }

        public ObservableQueue(IEnumerable<T> collection)
        {
            this.queue = new Queue<T>(collection);
        }

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
                ReentrancyGuard.ThrowRejectedChange(nameof(ObservableQueue<T>), rejectedMember);
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return queue.Count;
                }
            }
        }

        public void Enqueue(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = queue.Count;
                queue.Enqueue(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, index));
            }
        }

        public void EnqueueRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = queue.Count;
                using (var xs = new CloneCollection<T>(items))
                {
                    foreach (var item in xs.Span)
                    {
                        queue.Enqueue(item);
                    }
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void EnqueueRange(T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = queue.Count;
                foreach (var item in items)
                {
                    queue.Enqueue(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void EnqueueRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var index = queue.Count;
                foreach (var item in items)
                {
                    queue.Enqueue(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public T Dequeue()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableQueue<T>));
                var v = queue.Dequeue();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(v, 0));
                return v;
            }
        }

        public bool TryDequeue([MaybeNullWhen(false)] out T result)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying())
                {
                    result = default;
                    return false;
                }

                if (queue.Count != 0)
                {
                    result = queue.Dequeue();
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(result, 0));
                    return true;
                }
                result = default;
                return false;
            }
        }

        public void DequeueRange(int count)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var dest = ArrayPool<T>.Shared.Rent(count);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        dest[i] = queue.Dequeue();
                    }

                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(dest.AsSpan(0, count), 0));
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(dest, RuntimeHelpersEx.IsReferenceOrContainsReferences<T>());
                }
            }
        }

        public void DequeueRange(Span<T> dest)
        {
            lock (SyncRoot)
            {
                // Refusing would leave dest unwritten, which the caller cannot tell from a success.
                guard.ThrowIfNotifying(nameof(ObservableQueue<T>));
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = queue.Dequeue();
                }

                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(dest, 0));
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                queue.Clear();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

        public T Peek()
        {
            lock (SyncRoot)
            {
                return queue.Peek();
            }
        }

        public bool TryPeek([MaybeNullWhen(false)] out T result)
        {
            lock (SyncRoot)
            {
                if (queue.Count != 0)
                {
                    result = queue.Peek();
                    return true;
                }
                result = default;
                return false;
            }
        }

        public T[] ToArray()
        {
            lock (SyncRoot)
            {
                return queue.ToArray();
            }
        }

        public void TrimExcess()
        {
            lock (SyncRoot)
            {
                queue.TrimExcess();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in queue)
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
