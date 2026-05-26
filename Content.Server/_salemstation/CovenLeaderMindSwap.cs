using Content.Shared.Mobs.Systems;
using Content.Server.Mind;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Content.Server.MindSwapTracker;
using Content.Shared.Actions;
using Content.Shared.Mobs.Components;
using Content.Server.Antag;
using Content.Shared.CovenLeader;
using Content.Shared.CovenMember;
using Content.Shared.Mindshield.Components;

namespace Content.Server.CovenLeaderMindSwap
{
    public sealed partial class AntagMindSwapActionEvent : EntityTargetActionEvent { }
    public sealed class CovenLeaderMindSwapSystem : EntitySystem
    {
        [Dependency] private readonly MindSystem _mindSystem = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!; // Added to manage actions

        private const string MindSwapActionId = "ActionAntagMindSwap";

        private const string CovenLeaderRoleId = "covenLeader";

        public override void Initialize()
        {
            base.Initialize();

            // Listen for the mind swap action click
            SubscribeLocalEvent<AntagMindSwapActionEvent>(OnMindSwapAction);

            // Listen for when a player gets assigned an antagonist role
            SubscribeLocalEvent<CovenLeaderComponent, ComponentInit>(OnCovenLeaderInit);
        }

        private void OnCovenLeaderInit(EntityUid uid, CovenLeaderComponent component, ComponentInit args)
        {
            // uid here is the player's physical body
            _actionsSystem.AddAction(uid, MindSwapActionId);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<MindSwapTrackerComponent>();
            while (query.MoveNext(out var uid, out var swap))
            {
                // Only process from the master component to avoid double-ticking
                if (!swap.IsMaster)
                    continue;

                swap.TimeRemaining -= frameTime;

                if (swap.TimeRemaining <= 0f)
                {
                    ReverseMindSwap(uid, swap.Partner);
                }
            }
        }

        private void OnMindSwapAction(AntagMindSwapActionEvent args)
        {
            var performer = args.Performer;
            var target = args.Target;
            float duration = 30f; // 30 seconds duration

            if (!Exists(target) || !_mobState.IsAlive(target))
                return;

            // Check if the target is mindshielded
            if (HasComp<MindShieldComponent>(target))
            {
                // Read the performer's coven component
                if (!TryComp<CovenMemberComponent>(performer, out var covenComp) || !covenComp.Has_Necro)
                {
                    // If they aren't a coven member, or they haven't been granted the true boolean flag yet, block it!
                    return;
                }
            }

            // Execute the initial swap logic
            if (ExecuteSwap(performer, target))
            {
                // Attach components to track the temporary state
                var perfSwap = AddComp<MindSwapTrackerComponent>(performer);
                perfSwap.Partner = target;
                perfSwap.TimeRemaining = duration;
                perfSwap.IsMaster = true; // This entity will drive the countdown

                var targetSwap = AddComp<MindSwapTrackerComponent>(target);
                targetSwap.Partner = performer;
                targetSwap.TimeRemaining = duration;
                targetSwap.IsMaster = false;

                args.Handled = true;
            }
        }

        private bool ExecuteSwap(EntityUid alpha, EntityUid beta)
        {
            var alphaHasMind = _mindSystem.TryGetMind(alpha, out var alphaMindId, out var alphaMind);
            var betaHasMind = _mindSystem.TryGetMind(beta, out var betaMindId, out var betaMind);

            // If nobody has a mind, there's nothing to swap
            if (!alphaHasMind && !betaHasMind)
                return false;

            // SCENARIO 1: Both bodies have active players/minds
            if (alphaHasMind && betaHasMind)
            {
                // To safely swap without double-occupancy errors, we use WipeMind 
                // to break the link between the bodies and their current minds *first*.
                // This safely moves the players to a temporary "detached/ghost" state 
                // managed by the engine, clearing the bodies for a fresh transfer.
                _mindSystem.WipeMind(alpha);
                _mindSystem.WipeMind(beta);

                // Now that both bodies are officially vacant, assign the minds to their new bodies
                _mindSystem.TransferTo(alphaMindId, beta);
                _mindSystem.TransferTo(betaMindId, alpha);
            }
            // SCENARIO 2: Only Alpha (the antagonist) has a mind, Beta is a mindless AI/animal
            else if (alphaHasMind)
            {
                _mindSystem.WipeMind(alpha);
                _mindSystem.TransferTo(alphaMindId, beta);
            }
            // SCENARIO 3: Only Beta has a mind (unlikely edge case, but good to handle)
            else if (betaHasMind)
            {
                _mindSystem.WipeMind(beta);
                _mindSystem.TransferTo(betaMindId, alpha);
            }

            return true;
        }
        private void ReverseMindSwap(EntityUid entityA, EntityUid entityB)
        {
            // Remove the components first to prevent update loops
            RemCompDeferred<MindSwapTrackerComponent>(entityA);
            RemCompDeferred<MindSwapTrackerComponent>(entityB);

            // Double check that both entities still exist before attempting the swap back
            if (!Exists(entityA) || !Exists(entityB))
                return;

            ExecuteSwap(entityA, entityB);

            // Optional: Add visual or text feedback to the players
            // _popup.PopupEntity("Your consciousness snaps back into your own body!", entityA);
        }
    }
}
