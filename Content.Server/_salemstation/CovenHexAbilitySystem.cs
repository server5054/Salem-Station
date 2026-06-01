using Content.Server.Atmos.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.CovenHexAbilityEvents;
using Content.Shared.CovenHexed;
using Content.Shared.CovenHexMarkingWeapon;
using Content.Shared.CovenHexMaster;
using Content.Shared.CovenMember;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CovenHexAbilitySystem;

public sealed class CovenHexAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    private const string ActionHexPlayer = "ActionHexPlayer";
    private const string ActionTriggerHex = "ActionTriggerHex";


    public override void Initialize()
    {
        base.Initialize();

        // Bind the YAML actions to C# events
        SubscribeLocalEvent<CovenHexPlayerActionEvent>(OnMarkPlayer);
        SubscribeLocalEvent<CovenTriggerHexActionEvent>(OnMarkIgnite);
        SubscribeLocalEvent<CovenHexMasterComponent, ComponentInit>(OnCovenHexMasterInit);

        SubscribeLocalEvent<HexMarkingWeaponComponent, MeleeHitEvent>(OnMeleeHit);
    }


    private void OnCovenHexMasterInit(EntityUid uid, CovenHexMasterComponent component, ref ComponentInit args)
    {
        // Safety check: Don't double-grant actions if they already exist
        if (component.ActionHexPlayerEntity != null || component.ActionTriggerHexEntity != null)
            return;

        _actions.AddAction(uid, ref component.ActionHexPlayerEntity, ActionHexPlayer);
        _actions.AddAction(uid, ref component.ActionTriggerHexEntity, ActionTriggerHex);
        Dirty(uid, component);
    }
    private void OnMarkPlayer(CovenHexPlayerActionEvent args)
    {
        if (args.Handled)
            return;
        var caster = args.Performer;

        // Ensure they have active hands to hold the curse
        if (!_hands.TryGetEmptyHand(caster, out var emptyHand))
            return;
        // Spawn our bound hex item directly into existence
        var hexWeapon = Spawn("WeaponHexMarker", Transform(caster).Coordinates);

        // Force it directly into their open active hand
        if (_hands.TryPickup(caster, hexWeapon, emptyHand, checkActionBlocker: false, animateUser: true))
        {
            args.Handled = true;
        }
        else
        {
            // Fallback safety deletion if pickup somehow fails
            QueueDel(hexWeapon);
        }


    }

    private void OnMeleeHit(EntityUid weaponUid, HexMarkingWeaponComponent component, MeleeHitEvent args)
    {
        // We only want to process the strike if it connected with valid targets
        if (args.HitEntities.Count == 0)
            return;

        bool markedAnyone = false;

        foreach (var victim in args.HitEntities)
        {
            // Skip hitting yourself, walls, or non-living items
            if (victim == args.User || !HasComp<DamageableComponent>(victim))
                continue;

            // Apply your custom coven marker component permanently
            EnsureComp<CovenHexedComponent>(victim);
            markedAnyone = true;
        }

        // If we successfully hit and marked a valid player, consume the curse weapon
        if (markedAnyone)
        {
            QueueDel(weaponUid);
        }
    }

    private void OnMarkIgnite(CovenTriggerHexActionEvent args)
    {
        if (args.Handled)
            return;

        var caster = args.Performer;
        var activatedAny = false;

        // Find all victims currently marked, but whose timers haven't started yet
        var query = EntityQueryEnumerator<CovenHexedComponent>();
        while (query.MoveNext(out var victimUid, out var markComp))
        {
            if (markComp.TimerStarted)
                continue;

            // Start the 5-minute clock right now
            markComp.Caster = caster;
            markComp.DetonationTime = _gameTiming.CurTime + TimeSpan.FromMinutes(2);
            markComp.TimerStarted = true;

            activatedAny = true;
        }
        if (!activatedAny)
            return; // Don't trigger action cooldown if nobody was marked

        // Sound effect for the Caster confirming the ritual has begun

        args.Handled = true;

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Get the current persistent server time
        var currentTime = _gameTiming.CurTime;

        var query = EntityQueryEnumerator<CovenHexedComponent, DamageableComponent>();
        while (query.MoveNext(out var victimUid, out var markComp, out var damageableComp))
        {

            // IGNORE if the detonation ability hasn't been pressed yet!
            if (!markComp.TimerStarted)
                continue;

            // If the 5 minutes haven't fully elapsed yet, skip them for this tick
            if (currentTime < markComp.DetonationTime)
                continue;

            // --- THE FUSE HAS EXPIRED! DETONATE! ---

            DamageSpecifier damage = new();
            damage.DamageDict.Add("Heat", 100);

            // Apply the damage payload, attributing the source back to the original Hex Master
            _damageable.TryChangeDamage(victimUid, damage, ignoreResistances: true, origin: markComp.Caster);


            // Safely strip the component so they don't get double-hit on the next tick
            RemCompDeferred<CovenHexedComponent>(victimUid);
        }
    }

}


