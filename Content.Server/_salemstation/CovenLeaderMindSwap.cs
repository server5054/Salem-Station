using Content.Server.Antag;
using Content.Server.Mind;
using Content.Server.MindSwapTracker;
using Content.Shared.Actions;
using Content.Shared.CovenHexAbilityEvents;
using Content.Shared.CovenLeader;
using Content.Shared.CovenMember;
using Content.Shared.CovenMindSwappingWeapon;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.CovenLeaderMindSwap
{
    public sealed class CovenLeaderMindSwapSystem : EntitySystem
    {
        [Dependency] private readonly MindSystem _mindSystem = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!; // Added to manage actions
        [Dependency] private readonly SharedHandsSystem _hands = default!;

        private const string MindSwapActionId = "ActionAntagMindSwap";

        private const string CovenLeaderRoleId = "covenLeader";

        public override void Initialize()
        {
            base.Initialize();

            // Listen for the mind swap action click
            SubscribeLocalEvent<AntagMindSwapActionEvent>(OnMindSwapAction);

            // Listen for when a player gets assigned an antagonist role
            SubscribeLocalEvent<CovenLeaderComponent, ComponentInit>(OnCovenLeaderInit);

            SubscribeLocalEvent<CovenMindSwappingWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        }

        private void OnCovenLeaderInit(EntityUid uid, CovenLeaderComponent component, ComponentInit args)
        {
            // uid here is the player's physical body
            _actionsSystem.AddAction(uid, MindSwapActionId);
            Dirty(uid, component);
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

        private void OnMeleeHit(EntityUid uid, CovenMindSwappingWeaponComponent component, ref MeleeHitEvent args)
        {
            // The person swinging the weapon
            var attacker = args.User;

            // Melee swings can hit multiple entities at once, so loop through them
            foreach (var target in args.HitEntities)
            {
                // 1. Core Validation: Ensure target exists, is alive, and isn't the attacker themselves
                if (target == attacker || !Exists(target) || !_mobState.IsAlive(target))
                    continue;
                // 2. Mindshield Check: Read the attacker's coven status to see if they can pierce shields
                if (HasComp<MindShieldComponent>(target))
                {
                    if (!TryComp<CovenMemberComponent>(attacker, out var covenComp) || !covenComp.Has_Necro)
                    {
                        continue; // Blocked by mindshield, skip this target
                    }
                }

                // 3. Execution Phase
                if (ExecuteSwap(attacker, target))
                {
                    // Attach components to track the duration countdown
                    var perfSwap = AddComp<MindSwapTrackerComponent>(attacker);
                    perfSwap.Partner = target;
                    perfSwap.TimeRemaining = component.SwapDuration;
                    perfSwap.IsMaster = true;

                    var targetSwap = AddComp<MindSwapTrackerComponent>(target);
                    targetSwap.Partner = attacker;
                    targetSwap.TimeRemaining = component.SwapDuration;
                    targetSwap.IsMaster = false;

                    // We successfully swapped minds with the first valid entity we struck, so stop processing
                    break;
                }
            }
        }

        private void OnMindSwapAction(AntagMindSwapActionEvent args)
        {
            if (args.Handled)
                return;
            var caster = args.Performer;

            // Ensure they have active hands to hold the curse
            if (!_hands.TryGetEmptyHand(caster, out var emptyHand))
                return;
            // Spawn our bound hex item directly into existence
            var mindswapWeapon = Spawn("WeaponMindSwapper", Transform(caster).Coordinates);

            // Force it directly into their open active hand
            if (_hands.TryPickup(caster, mindswapWeapon, emptyHand, checkActionBlocker: false, animateUser: true))
            {
                args.Handled = true;
            }
            else
            {
                // Fallback safety deletion if pickup somehow fails
                QueueDel(mindswapWeapon);
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
