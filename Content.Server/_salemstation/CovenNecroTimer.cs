using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;
using Content.Shared.CovenMember;

namespace Content.Server.CovenNecroTimer
{

    public sealed class CovenNecroTimer : EntitySystem
    {
        [Dependency] private readonly GameTicker _gameTicker = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private readonly TimeSpan _selectionTime = TimeSpan.FromMinutes(30);
        private bool _leaderSelected = false;


        public override void Update(float frameTime)
        {



            base.Update(frameTime);

            // If we already picked a leader this round, stop checking
            if (_leaderSelected)
                return;

            // Wait until 30 minutes have ticked by in the round
            if (_gameTicker.RoundDuration() < _selectionTime)
                return;

            GiveNecronomicon();
        }

        private void GiveNecronomicon()
        {
            var candidates = new List<EntityUid>();
            var query = EntityQueryEnumerator<CovenMemberComponent>();

            // 1. Gather all living/active coven members currently in the round
            while (query.MoveNext(out var uid, out var covenComp))
            {
                candidates.Add(uid);
            }

            // Safety check: If no coven members are left alive/in-game, retry next tick
            if (candidates.Count == 0)
                return;

            // 2. Pick exactly one random player from the list
            var chosenEmpoweredUid = _random.Pick(candidates);

            // 3. Flip their boolean flag to true
            if (TryComp<CovenMemberComponent>(chosenEmpoweredUid, out var leaderComp))
            {
                leaderComp.Has_Necro = true;
                _leaderSelected = true; // Lock system so it doesn't pick again

                // Optional: Notify the chosen player via text popup or audio
                Logger.Info($"The Necronomicon has chosen entity {chosenEmpoweredUid}!");
            }

        }
    }

}
