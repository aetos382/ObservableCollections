using System;

namespace ObservableCollections;

/// <summary>
/// Thrown when a collection is changed from a handler of its own
/// <see cref="IObservableCollection{T}.CollectionChanged"/> event.
/// </summary>
/// <remarks>
/// The nested change is refused rather than applied, because it would invalidate the notification
/// that the handlers which have not been called yet are about to receive. This exception surfaces at
/// the call site that requested the change, after the notification has been delivered to every
/// handler. It derives from <see cref="InvalidOperationException"/>, which is what earlier versions
/// threw, so existing handling keeps working. Catch this type instead to tell the violation apart
/// from the <see cref="InvalidOperationException"/> that <c>Dequeue</c>, <c>Pop</c>,
/// <c>RemoveFirst</c> and <c>RemoveLast</c> throw for an empty collection.
/// </remarks>
public sealed class CollectionReentrancyException : InvalidOperationException
{
    public CollectionReentrancyException()
    {
    }

    public CollectionReentrancyException(string message)
        : base(message)
    {
    }

    public CollectionReentrancyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
