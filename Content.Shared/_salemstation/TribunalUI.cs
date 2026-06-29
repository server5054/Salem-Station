using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared.TribunalUIShared
{
    // The unique key to identify this UI
    [Serializable, NetSerializable]
    public enum TribunalUiKey : byte
    {
        Key
    }

    // Sent from Client -> Server when a player clicks a button
    [Serializable, NetSerializable]
    public sealed class TribunalVoteMessage : BoundUserInterfaceMessage
    {
        public readonly bool VoteGuilty;
        public TribunalVoteMessage(bool voteGuilty)
        {
            VoteGuilty = voteGuilty;
        }
    }

    // Sent from Server -> Client to update what the window displays
    [Serializable, NetSerializable]
    public sealed class TribunalBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string PrisonerName;
        public readonly int GuiltyVotes;
        public readonly int InnocentVotes;
        public readonly TimeSpan TimeRemaining;
        public readonly string CurrentPhase;

        public TribunalBoundUserInterfaceState(string prisonerName, int guilty, int innocent, TimeSpan remaining, string phase)
        {
            PrisonerName = prisonerName;
            GuiltyVotes = guilty;
            InnocentVotes = innocent;
            TimeRemaining = remaining;
            CurrentPhase = phase;
        }
    }
}
