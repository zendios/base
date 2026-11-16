namespace Base.Ads
{
    /// <summary>
    /// Turns the states of one rewarded attempt into exactly one result for the caller. Networks differ on the order of
    /// "reward earned" and "closed" (and both reach Unity through the main-thread dispatcher), so a close is only a skip
    /// when no reward follows it within AfterCloseSeconds.
    /// </summary>
    internal class RewardResult
    {
        internal const float AfterCloseSeconds = 1f;
        internal const float TimeoutSeconds = 120f;

        private bool rewarded;
        private bool closed;
        private AdState failure = AdState.None;
        private float waited;
        private float closedFor;

        public AdType Type { get; private set; } = AdType.Reward;

        /// <summary>The result is known (reward earned, closed and waited, failed, or timed out).</summary>
        public bool IsDone => rewarded
            || (closed && closedFor >= AfterCloseSeconds)
            || (!closed && failure != AdState.None)
            || waited >= TimeoutSeconds;

        /// <summary>ShowSuccess (reward earned), Close (closed without a reward), else why no ad showed.</summary>
        public AdState State => rewarded ? AdState.ShowSuccess
            : closed ? AdState.Close
            : failure != AdState.None ? failure : AdState.ShowNotAvailable;

        public void Collect(AdType type, AdState state)
        {
            Type = type;
            if (state == AdState.ShowSuccess)
                rewarded = true;
            else if (state == AdState.Close)
                closed = true;
            else if (state == AdState.LoadAvailable)
                failure = AdState.None;   // an earlier waterfall step failed, a later one filled
            else
                failure = state;
        }

        /// <summary>Time since the attempt ended. The caller caps it per frame: time paused behind the ad does not count.</summary>
        public void Wait(float seconds)
        {
            waited += seconds;
            if (closed)
                closedFor += seconds;
        }

        public override string ToString() =>
            $"{State} (reward {rewarded}, closed {closed}, failure {failure}, waited {waited:0.0}s)";
    }
}
