using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.TribunalPodiumDef
{
    [RegisterComponent]
    public sealed partial class TribunalPodiumComponent : Component
    {
        // Tracks the current entity buckled/cuffed to the podium
        public EntityUid? Prisoner = null;

        // Trial states: Idle, Discussion, Voting, Execution
        public TrialState CurrentState = TrialState.Idle;

        // Timers
        public TimeSpan StateEndTime;
        public readonly TimeSpan DiscussionDuration = TimeSpan.FromMinutes(5);
        public readonly TimeSpan LastWordsDuration = TimeSpan.FromSeconds(10);

        // Vote tracking (Player NetID -> Vote Choice)
        public Dictionary<string, bool> Votes = new();
    }

    public enum TrialState
    {
        Idle,
        Discussion,
        Voting,
        Execution
    }
}
