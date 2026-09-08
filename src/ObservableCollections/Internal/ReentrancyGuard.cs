using System.Runtime.CompilerServices;

namespace ObservableCollections.Internal
{
    // Changing the collection while CollectionChanged is being raised reorders the notifications
    // delivered to the remaining subscribers, which desynchronizes them from the collection.
    // The change is rejected, but it is reported only after every subscriber has received the
    // notification, so that a subscriber that observes the rules is not left behind by an aborted
    // delivery. Because the report happens after the refused call has already returned, the guard
    // remembers which member was refused; otherwise the exception would surface at an innocent call
    // site with nothing to identify the handler that caused it.
    internal struct ReentrancyGuard
    {
        bool notifying;
        Rejection rejection;

        /// <summary>
        /// Returns true when the caller must abandon the change because a notification is being
        /// delivered. The refusal is remembered until the delivery completes.
        /// </summary>
        public bool RejectIfNotifying([CallerMemberName] string? member = null)
        {
            if (notifying)
            {
                rejection.Accumulate(new Rejection(member));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Throws when a notification is being delivered. For the members that hand the removed
        /// elements back to the caller, either as the return value or by writing into a buffer the
        /// caller supplied, and therefore have no way to express a refusal. Unlike
        /// <see cref="RejectIfNotifying"/> this aborts the delivery, so it must not be used where a
        /// refusal is expressible. The refusal is recorded before throwing, so that a handler that
        /// swallows the exception does not hide the violation from the outer call site.
        /// </summary>
        public void ThrowIfNotifying(string typeName, [CallerMemberName] string? member = null)
        {
            if (notifying)
            {
                rejection.Accumulate(new Rejection(member));
                ThrowReentrancyNotAllowed(typeName, member);
            }
        }

        public void BeginNotification()
        {
            notifying = true;
        }

        /// <summary>
        /// Ends the notification and hands out the refusal it collected, which is cleared here. A second
        /// call reports no refusal.
        /// </summary>
        public Rejection EndNotification()
        {
            notifying = false;

            var rejection = this.rejection;
            this.rejection = default;

            return rejection;
        }

        /// <summary>
        /// Reports the refusal to the handler that caused it, which aborts the delivery. Only for the
        /// members that cannot express a refusal any other way.
        /// </summary>
        public static void ThrowReentrancyNotAllowed(string typeName, string? member)
        {
            throw new CollectionReentrancyException(
                $"Cannot change {typeName} from a CollectionChanged handler{Describe(member)}.");
        }

        /// <summary>
        /// Reports the refusal to the call site whose change was being delivered. That call site did
        /// nothing wrong; it is the only place left to report to, because the refused call has already
        /// returned to the handler without a value that could carry the refusal.
        /// </summary>
        public static void ThrowRejectedChange(string typeName, string? member)
        {
            throw new CollectionReentrancyException(
                $"A CollectionChanged handler tried to change {typeName}{Describe(member)}, and the change " +
                "was refused. This is reported at the call site whose change was being delivered, because " +
                "the handler was given no value that could carry the refusal.");
        }

        static string Describe(string? member)
        {
            return member == null ? "" : $" ({member} was called)";
        }
    }
}
