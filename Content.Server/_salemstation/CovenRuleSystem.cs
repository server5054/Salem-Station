using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.CovenRule;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared.Chat;
using Content.Shared.CovenMember;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Robust.Shared.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.CovenInGameRules;

public sealed class CovenRuleSystem : GameRuleSystem<CovenRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly Content.Server.Chat.Systems.ChatSystem _chatSystem = default!;
    [Dependency] private readonly Content.Server.Chat.Managers.IChatManager _chatManager = default!;



    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<CovenRuleComponent, GameRuleStartedEvent>(OnRuleStarted);
        //SubscribeLocalEvent<CovenRuleComponent, GameRuleEndedEvent>(OnRuleEnded);

        // Listen for entity death
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void Started(EntityUid uid, CovenRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        // 1. Gather all candidates who want to play ANY role in this faction
        // We pool them together so players who selected multiple roles aren't left out
        var allSessions = _playerManager.Sessions.ToList();
        var eligiblePlayers = new List<ICommonSession>();

        if (eligiblePlayers.Count == 0)
            return;

        // Shuffle the master player pool to ensure random distribution across roles
        var random = new Random();
        var playerPool = eligiblePlayers.OrderBy(_ => random.Next()).ToList();

        // 2. Loop through each configured role and assign players
        foreach (var roleConfig in component.Roles)
        {
            // Determine how many we actually want to spawn based on available players
            int targetCount = Math.Min(roleConfig.Max, playerPool.Count);

            // If we don't have enough players to meet the minimum required for a vital role,
            // you could add a fallback or logging here.
            if (targetCount < roleConfig.Min && playerPool.Count > 0)
            {
                targetCount = playerPool.Count;
            }

            for (int i = 0; i < targetCount; i++)
            {
                // Pull a player out of our pool
                var session = playerPool[0];
                playerPool.RemoveAt(0);

                if (session.AttachedEntity is not { } mob)
                    continue;

                // 3. Assign the specific role ID to this player COMMENTED OUT
                //_role.MindAddRole(session, new AntagRole(session, roleConfig.Id), silent: false);

                // Mark them globally as our faction member
                component.ActiveAntags.Add(mob);
                EnsureComp<CovenMemberComponent>(mob);
            }

            SendCovenBriefing(component);
        }
    }
    protected override void Ended(EntityUid uid, CovenRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        
        // Clean up or log round-end stats here
        component.ActiveAntags.Clear();
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        // We only care if an entity has actually died
        if (ev.NewMobState != MobState.Dead)
            return;

        // Get all active instances of your game rule running
        var query = EntityQueryEnumerator<CovenRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            // If the rule isn't actively running, don't check win conditions
            if (!GameTicker.IsGameRuleActive(uid))
                continue;

            CheckWinCondition(uid, rule);
        }
    }

    private void SendCovenBriefing(CovenRuleComponent component)
    {
        // Build your faction's secret message text
        // Using color tags makes it pop out visually in the chat box!
        var message = "[color=purple][bold]You are part of the Coven!:[/bold]\n" +
                      "Your circle sent you to wipe out this station's crew with deception and witchcraft.\n" +
                      "Avenge your fallen brothers and sisters![/color]";

        foreach (var mob in component.ActiveAntags)
        {
            // We need to find the network/player session currently controlling this mob
            if (!TryComp<ActorComponent>(mob, out var actor))
                continue;

            var session = actor.PlayerSession;

            // Dispatch a local chat message strictly visible to this single player
            _chatManager.ChatMessageToOne(
                ChatChannel.Server,              // Internal server log channel
                message,                         // The briefing text
                string.Empty,                    // Plain text wrapped formatting (leave empty)
                EntityUid.Invalid,               // No physical "speaker" entity in the room
                false,                           // Do not show a floating chat bubble above their head
                session.Channel                          // The specific recipient target
            );

            // OPTIONAL: If you want to play a spooky/alert sound effect just for them:
            // _audioSystem.PlayToSession(session, "/Audio/Ambience/Antag/traitor_start.ogg");
        }
    }

    private void CheckWinCondition(EntityUid ruleUid, CovenRuleComponent rule)
    {
        // Keep track of whether any innocent crew are still breathing
        bool crewAlive = false;

        // Loop through all minds tracking active players
        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindUid, out var mind))
        {
            // Skip if they don't have a physical body spawned in the round
            if (mind.OwnedEntity == null)
                continue;

            var playerMob = mind.OwnedEntity.Value;

            // Skip if this specific body is already dead
            if (_mobState.IsDead(playerMob))
                continue;

            // Check if this living player is part of your antag faction.
            // Option A: Check if they have your custom antag component
            if (HasComp<CovenMemberComponent>(playerMob))
                continue;

            // Option B: (Alternative) Check if their mind possesses your Antag Role
            // if (_role.MindHasRole(mindUid, rule.AntagRoleId)) continue;

            // If we reached this point, we found a living player who is NOT your antag
            crewAlive = true;
            break;
        }

        if (!crewAlive)
        {
            TriggerFactionVictory(ruleUid, rule);
        }
    }

    private void TriggerFactionVictory(EntityUid ruleUid, CovenRuleComponent rule)
    {
        // 1. Send a round-end text announcement to the chat box
        _chatSystem.DispatchStationAnnouncement(
            ruleUid,
            "The station has fallen for scorcery and witchcraft. The Coven wins!",
            sender: "Central Command Warning"
        );

        // 2. Tell the GameTicker to wrap up the round
        // This will bring up the round-end summary screen
        _gameTicker.EndRound();
    }
}
