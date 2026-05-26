using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared.CovenHexed;
using Content.Shared.Atmos.Components;
using Content.Shared.CovenHexAbilityEvents;
using Content.Shared.Actions;
using Content.Shared.Roles;
using Content.Server.Roles;
using Content.Shared.CovenHexMaster;
using Content.Shared.CovenMember;

namespace Content.Server.CovenHexAbilitySystem;

public sealed class CovenHexAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;

    private const string ActionHexPlayer = "ActionHexPlayer";
    private const string ActionTriggerHex = "ActionTriggerHex";

    public override void Initialize()
    {
        base.Initialize();

        // Bind the YAML actions to C# events
        SubscribeLocalEvent<CovenHexPlayerActionEvent>(OnMarkPlayer);
        SubscribeLocalEvent<CovenTriggerHexActionEvent>(OnMarkIgnite);
        SubscribeLocalEvent<CovenHexMasterComponent, ComponentInit>(OnCovenHexMasterInit);
    }


    private void OnCovenHexMasterInit(EntityUid uid , CovenHexMasterComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ActionHexPlayer);
        _actions.AddAction(uid, ActionTriggerHex);
    }
    private void OnMarkPlayer(CovenHexPlayerActionEvent args)
    {
        if (args.Handled)
            return;
        var target = args.Target.EntityId;

        // Ensure we actually targeted a player/valid entity, not empty space
        if (!EntityManager.EntityExists(target))
            return;
        // Don't mark someone who is already marked
        if (HasComp<CovenHexedComponent>(target))
            return;

        EnsureComp<CovenHexedComponent>(target);


    }

    private void OnMarkIgnite(CovenTriggerHexActionEvent args)
    {
        if (args.Handled)
            return;
        int delayInMilliseconds = 300000;
        //var delay = TimeSpan.FromSeconds(300);

        var performer = args.Performer;

        Timer.Spawn(delayInMilliseconds, () => IgniteHexes(performer));

        args.Handled = true;
    }

    private void IgniteHexes(EntityUid performer)
    {

        var damageSpec = new DamageSpecifier();
        if (_prototypeManager.TryIndex<DamageTypePrototype>("Heat", out var heatType))
        {
            damageSpec.DamageDict.Add(heatType.ID, 100);
        }

        // Find every entity across the map that has our marker component
        var query = EntityQueryEnumerator<CovenHexedComponent, FlammableComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var flammable, out _))
        {
            if (!TryComp<CovenMemberComponent>(performer, out var hexComp) || !hexComp.Has_Necro)
            {
                return;
            }

            else
            {
                // Parameters: (epicenter, explosionPrototypeId, totalIntensity, slope, maxRadius)
                // "Default" is the standard explosion type. Adjust numbers for flavor/balance!
                _explosion.QueueExplosion(
                    uid,
                    "Default",
                    totalIntensity: 30,
                    slope: 5,
                    maxTileIntensity: 10,
                    user: performer
                );
            }
            // 1. Ignite the player
            _flammable.AdjustFireStacks(uid, 5, flammable); // Adds fire intensity
            _flammable.Ignite(uid, uid);

            // 2. Deal 100 Heat Damage instantly
            _damageable.TryChangeDamage(uid, damageSpec, ignoreResistances: true);

            // 3. Clean up the marker component so it doesn't happen again
            RemCompDeferred<CovenHexedComponent>(uid);
        }
    }
}


