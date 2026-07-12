namespace Content.Server._salemstation
{
    [RegisterComponent]
    public sealed partial class TribunalPodiumComponent : Component
    {
        // Tracks the current entity buckled/cuffed to the podium
        public EntityUid? Prisoner;

        // Trial states: Idle, Discussion, Voting, Execution
        public TrialState CurrentState = TrialState.Idle;

        // When the current phase ends (compared against IGameTiming.CurTime)
        public TimeSpan StateEndTime;

        [DataField]
        public TimeSpan DiscussionDuration = TimeSpan.FromMinutes(5);

        [DataField]
        public TimeSpan VotingDuration = TimeSpan.FromMinutes(1);

        [DataField]
        public TimeSpan LastWordsDuration = TimeSpan.FromSeconds(10);

        // Vote tracking (voter entity -> guilty?). Re-voting overwrites the previous choice.
        public readonly Dictionary<EntityUid, bool> Votes = new();
    }

    public enum TrialState : byte
    {
        Idle,
        Discussion,
        Voting,
        Execution
    }
}
