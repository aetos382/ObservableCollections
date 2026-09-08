using ObservableCollections.Internal;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ObservableCollections
{
    public partial class ObservableStack<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        readonly Stack<T> stack;
        ReentrancyGuard guard;
        public object SyncRoot { get; } = new object();

        public ObservableStack()
        {
            this.stack = new Stack<T>();
        }

        public ObservableStack(int capacity)
        {
            this.stack = new Stack<T>(capacity);
        }

        public ObservableStack(IEnumerable<T> collection)
        {
            this.stack = new Stack<T>(collection);
        }

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

            rejection.ThrowIfRejected(nameof(ObservableStack<T>));
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return stack.Count;
                }
            }
        }

        public void Push(T item)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                stack.Push(item);
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(item, 0));
            }
        }

        public void PushRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                using (var xs = new CloneCollection<T>(items))
                {
                    foreach (var item in xs.Span)
                    {
                        stack.Push(item);
                    }
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, 0));
                }
            }
        }

        public void PushRange(T[] items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                foreach (var item in items)
                {
                    stack.Push(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, 0));
            }
        }

        public void PushRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                foreach (var item in items)
                {
                    stack.Push(item);
                }
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Add(items, 0));
            }
        }

        public T Pop()
        {
            lock (SyncRoot)
            {
                guard.ThrowIfNotifying(nameof(ObservableStack<T>));
                var v = stack.Pop();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(v, 0));
                return v;
            }
        }

        public bool TryPop([MaybeNullWhen(false)] out T result)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying())
                {
                    result = default;
                    return false;
                }

                if (stack.Count != 0)
                {
                    result = stack.Pop();
                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(result, 0));
                    return true;
                }

                result = default;
                return false;
            }
        }

        public void PopRange(int count)
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                var dest = ArrayPool<T>.Shared.Rent(count);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        dest[i] = stack.Pop();
                    }

                    NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(dest.AsSpan(0, count), 0));
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(dest, RuntimeHelpersEx.IsReferenceOrContainsReferences<T>());
                }
            }
        }

        public void PopRange(Span<T> dest)
        {
            lock (SyncRoot)
            {
                // Refusing would leave dest unwritten, which the caller cannot tell from a success.
                guard.ThrowIfNotifying(nameof(ObservableStack<T>));
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[i] = stack.Pop();
                }

                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Remove(dest, 0));
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                if (guard.RejectIfNotifying()) return;
                stack.Clear();
                NotifyCollectionChanged(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

        public T Peek()
        {
            lock (SyncRoot)
            {
                return stack.Peek();
            }
        }

        public bool TryPeek([MaybeNullWhen(false)] out T result)
        {
            lock (SyncRoot)
            {
                if (stack.Count != 0)
                {
                    result = stack.Peek();
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
                return stack.ToArray();
            }
        }

        public void TrimExcess()
        {
            lock (SyncRoot)
            {
                stack.TrimExcess();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (SyncRoot)
            {
                foreach (var item in stack)
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
