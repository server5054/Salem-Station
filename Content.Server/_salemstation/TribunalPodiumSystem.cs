using Content.Server.Chat.Systems;
using Content.Server.TribunalPodiumDef;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.TribunalUIShared;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.TribunalPodiumSystemDef
{
    public sealed class TribunalPodiumSystem : EntitySystem
    {
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly EntityManager _entManager = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TribunalPodiumComponent, InteractHandEvent>(OnInteract);
        }

        private void InitializeBui()
        {
            // Listen for the server's master BUI message event
            SubscribeLocalEvent<TribunalPodiumComponent, ServerBoundUserInterfaceMessage>(OnBuiMessageReceived);
        }

        private void OnBuiMessageReceived(EntityUid uid, TribunalPodiumComponent component, ServerBoundUserInterfaceMessage args)
        {
            // Cast the message safely. If it's not a vote, ignore it.
            if (args.Message is not TribunalVoteMessage voteMessage)
                return;

            if (component.CurrentState != TrialState.Voting)
                return;

            if (args.Session.AttachedEntity == null)
                return;

            // Securely read the NetID provided by the server wrapper, NOT the client
            var playerNetId = args.Session.UserId.ToString();

            // Read the boolean choice from our custom shared message
            component.Votes[playerNetId] = voteMessage.VoteGuilty;

            UpdateUserInterface(uid, component);
        }

        private void UpdateUserInterface(EntityUid uid, TribunalPodiumComponent component)
        {
            int guilty = 0;
            int innocent = 0;

            foreach (var vote in component.Votes.Values)
            {
                if (vote) guilty++; else innocent++;
            }

            var prisonerName = component.Prisoner != null ? Name(component.Prisoner.Value) : "None";
            var remaining = component.StateEndTime - _timing.CurTime; //Check

            var state = new TribunalBoundUserInterfaceState(
                prisonerName,
                guilty,
                innocent,
                remaining,
                component.CurrentState.ToString()
            );

            _ui.SetUiState(uid, TribunalUiKey.Key, state);
        }

        private void OnInteract(EntityUid uid, TribunalPodiumComponent component, InteractHandEvent args)
        {
            // 1. Check if a trial is already running
            if (component.CurrentState != TrialState.Idle)
                return;

            // 2. Verify the user is the Captain (has Captain ID)
            if (!_inventory.TryGetSlotEntity(args.User, "id", out var idUid)) return;
            // (Simplification: Check if the ID card has Captain access)
            // if (!HasCaptainAccess(idUid)) return; 

            // 3. Check if someone is buckled/cuffed to the podium
            if (!TryComp<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0)
            {
                _popup.PopupEntity("There is no prisoner secured to the podium!", uid, args.User);
                return;
            }

            // Start the trial
            component.Prisoner = strap.BuckledEntities.First();
            StartDiscussionPhase(uid, component);
        }

        private void StartDiscussionPhase(EntityUid uid, TribunalPodiumComponent component)
        {
            component.CurrentState = TrialState.Discussion;
            component.StateEndTime = _timing.CurTime + component.DiscussionDuration;

            _chat.DispatchGlobalAnnouncement(
                "A formal Tribunal has been called by the Captain! The 5-minute evidence and discussion period begins now.",
                "Tribunal AI",
                colorOverride: Color.DeepSkyBlue
            );
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var curTime = _timing.CurTime;

            foreach (var comp in EntityQuery<TribunalPodiumComponent>())
            {
                if (comp.CurrentState == TrialState.Idle) continue;

                if (curTime >= comp.StateEndTime)
                {
                    AdvanceTrialState(comp);
                }
            }
        }

        private void AdvanceTrialState(TribunalPodiumComponent comp)
        {
            switch (comp.CurrentState)
            {
                case TrialState.Discussion:
                    comp.CurrentState = TrialState.Voting;
                    // Set up voting timer, open UI for players, etc.
                    _chat.DispatchGlobalAnnouncement("Discussion time is over. Cast your votes at the podium interfaces!", "Tribunal AI");
                    break;

                case TrialState.Voting:
                    // Count votes. If Majority = Guilty:
                    comp.CurrentState = TrialState.Execution;
                    comp.StateEndTime = _timing.CurTime + comp.LastWordsDuration;
                    _chat.DispatchGlobalAnnouncement("The verdict is GUILTY. The prisoner has 10 seconds for last words.", "Tribunal AI", colorOverride: Color.Red);
                    break;

                case TrialState.Execution:
                    ExecutePrisoner(comp.Prisoner);
                    comp.CurrentState = TrialState.Idle;
                    comp.Prisoner = null;
                    break;
            }
        }

        private void ExecutePrisoner(EntityUid? prisoner)
        {
            if (prisoner == null || !Exists(prisoner)) return;

            // 1. Create the asphyxiation damage specifier
            var damageSpec = new DamageSpecifier();

            // SS14 uses specific string IDs for damage types. "Asphyxiation" is standard.
            damageSpec.DamageDict.Add("Asphyxiation", 200);

            // 2. Apply the damage to the prisoner. 
            // 'ignoreModifiers: true' ensures armor or internals don't accidentally block the execution.
            _damageable.TryChangeDamage(prisoner.Value, damageSpec, ignoreResistances: true);

            // 3. Optional: Visual flair text to show they are suffocating
            _chat.DispatchGlobalAnnouncement(
                $"{Name(prisoner.Value)} has been suffocated by order of the Tribunal.",
                "Tribunal AI",
                colorOverride: Color.Tomato);
        }
    }
}
