namespace ObservableCollections.Internal
{
    // Changing the collection while CollectionChanged is being raised reorders the notifications
    // delivered to the remaining subscribers, which desynchronizes them from the collection.
    // The change is rejected, but it is reported only after every subscriber has received the
    // notification, so that a subscriber that observes the rules is not left behind by an aborted
    // delivery.
    internal struct ReentrancyGuard
    {
        bool notifying;
        bool rejected;

        /// <summary>
        /// Returns true when the caller must abandon the change because a notification is being
        /// delivered. The rejection is remembered until the delivery completes.
        /// </summary>
        public bool RejectIfNotifying()
        {
            if (notifying)
            {
                rejected = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Throws when a notification is being delivered. For the members that return a value and
        /// therefore cannot express a refusal. Unlike <see cref="RejectIfNotifying"/> this aborts the
        /// delivery, so it must not be used where a refusal is expressible.
        /// </summary>
        public void ThrowIfNotifying(string typeName)
        {
            if (notifying)
            {
                ThrowReentrancyNotAllowed(typeName);
            }
        }

        public void BeginNotification()
        {
            notifying = true;
        }

        /// <summary>Returns true when a change was rejected while the notification was delivered.</summary>
        public bool EndNotification()
        {
            notifying = false;

            var rejected = this.rejected;
            this.rejected = false;

            return rejected;
        }

        public static void ThrowReentrancyNotAllowed(string typeName)
        {
            throw new CollectionReentrancyException($"Cannot change {typeName} during a CollectionChanged event.");
        }
    }
}
