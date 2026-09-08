namespace ObservableCollections.Internal
{
    // Which change was refused while a notification was being delivered, if any. A refusal is reported
    // only after the delivery completes, so it has to be carried from the guard out to the call site
    // that made the original change. An operation that notifies more than once carries one of these
    // across all of its notifications and reports it after the operation has been applied in full.
    internal struct Rejection
    {
        bool rejected;
        string? member;

        public Rejection(string? member)
        {
            this.rejected = true;
            this.member = member;
        }

        /// <summary>
        /// Records <paramref name="other"/> unless a refusal is already recorded. The first refusal is
        /// the one kept, because a handler that carries on after being refused would otherwise replace
        /// the member that caused the trouble with whichever one it called last.
        /// </summary>
        public void Accumulate(in Rejection other)
        {
            if (other.rejected && !rejected)
            {
                this = other;
            }
        }

        /// <summary>
        /// Reports the refusal, if there was one, to the call site whose change was being delivered.
        /// </summary>
        public void ThrowIfRejected(string typeName)
        {
            if (rejected)
            {
                ReentrancyGuard.ThrowRejectedChange(typeName, member);
            }
        }
    }
}
