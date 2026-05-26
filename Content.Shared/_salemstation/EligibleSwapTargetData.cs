using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Collections.Generic;

namespace Content.Shared.EligibleSwapTargetData
{

    public sealed class EligibleSwapTargetDataSystem : EntitySystem
    {
        // Dependency inject the core MobStateSystem to cleanly check health states
        [Dependency] private readonly MobStateSystem _mobState = default!;

        /// <summary>
        /// Fetches a list of all EntityUid references for mobs currently alive in the round.
        /// </summary>
        public List<EntityUid> GetAliveMobs()
        {
            var aliveMobs = new List<EntityUid>();

            // EntityQueryEnumerator is the most optimized way to loop through entities with specific components in Robust
            var query = EntityQueryEnumerator<MobStateComponent>();

            while (query.MoveNext(out var uid, out var mobState))
            {
                // Use the MobStateSystem to safely verify if the entity is alive 
                // (This filters out Dead or Critical states)
                if (_mobState.IsAlive(uid, mobState))
                {
                    aliveMobs.Add(uid);
                }
            }

            return aliveMobs;
        }
    }
}
