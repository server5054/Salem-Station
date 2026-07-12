using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._salemstation
{
    public sealed partial class TribunalPodiumSystem : EntitySystem
    {
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly AccessReaderSystem _access = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;

        // Push a fresh UI state (vote counts + countdown) to viewers once per second.
        private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(1);
        private TimeSpan _nextUiUpdate;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<TribunalPodiumComponent, InteractHandEvent>(OnInteract);
            InitializeBui();
        }

        private void OnInteract(EntityUid uid, TribunalPodiumComponent component, InteractHandEvent args)
        {
            if (args.Handled || component.CurrentState != TrialState.Idle)
                return;

            // Only someone carrying Captain-level access may convene a trial.
            if (!_access.FindAccessTags(args.User).Contains("Captain"))
            {
                _popup.PopupEntity("You need the Captain's authority to convene a tribunal!", uid, args.User);
                return;
            }

            if (!TryComp<StrapComponent>(uid, out var strap) || strap.BuckledEntities.Count == 0)
            {
                _popup.PopupEntity("There is no prisoner secured to the podium!", uid, args.User);
                return;
            }

            component.Prisoner = strap.BuckledEntities.First();
            component.Votes.Clear();
            StartDiscussionPhase(uid, component);
            args.Handled = true;
        }

        private void StartDiscussionPhase(EntityUid uid, TribunalPodiumComponent component)
        {
            component.CurrentState = TrialState.Discussion;
            component.StateEndTime = _timing.CurTime + component.DiscussionDuration;

            _chat.DispatchGlobalAnnouncement(
                $"A formal Tribunal has been called by the Captain! {Name(component.Prisoner!.Value)} stands accused. The {(int) component.DiscussionDuration.TotalMinutes}-minute evidence and discussion period begins now.",
                "Tribunal AI",
                colorOverride: Color.DeepSkyBlue);

            UpdateUserInterface(uid, component);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var curTime = _timing.CurTime;

            var pushUiUpdate = curTime >= _nextUiUpdate;
            if (pushUiUpdate)
                _nextUiUpdate = curTime + UiUpdateInterval;

            var query = EntityQueryEnumerator<TribunalPodiumComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.CurrentState == TrialState.Idle)
                    continue;

                if (curTime >= comp.StateEndTime)
                    AdvanceTrialState(uid, comp);
                else if (pushUiUpdate)
                    UpdateUserInterface(uid, comp);
            }
        }

        private void AdvanceTrialState(EntityUid uid, TribunalPodiumComponent comp)
        {
            switch (comp.CurrentState)
            {
                case TrialState.Discussion:
                    comp.CurrentState = TrialState.Voting;
                    comp.StateEndTime = _timing.CurTime + comp.VotingDuration;
                    comp.Votes.Clear();
                    _chat.DispatchGlobalAnnouncement(
                        $"Discussion time is over. Crewmates have {(int) comp.VotingDuration.TotalSeconds} seconds to cast their votes at the podium!",
                        "Tribunal AI");
                    break;

                case TrialState.Voting:
                    CountVotes(comp, out var guilty, out var innocent);

                    if (guilty > innocent)
                    {
                        comp.CurrentState = TrialState.Execution;
                        comp.StateEndTime = _timing.CurTime + comp.LastWordsDuration;
                        _chat.DispatchGlobalAnnouncement(
                            $"The verdict is GUILTY ({guilty} to {innocent}). The prisoner has {(int) comp.LastWordsDuration.TotalSeconds} seconds for last words.",
                            "Tribunal AI",
                            colorOverride: Color.Red);
                    }
                    else
                    {
                        _chat.DispatchGlobalAnnouncement(
                            $"The verdict is INNOCENT ({innocent} to {guilty}). The accused walks free.",
                            "Tribunal AI",
                            colorOverride: Color.LimeGreen);
                        EndTrial(comp);
                    }
                    break;

                case TrialState.Execution:
                    ExecutePrisoner(comp.Prisoner);
                    EndTrial(comp);
                    break;
            }

            UpdateUserInterface(uid, comp);
        }

        private static void EndTrial(TribunalPodiumComponent comp)
        {
            comp.CurrentState = TrialState.Idle;
            comp.Prisoner = null;
            comp.Votes.Clear();
        }

        private static void CountVotes(TribunalPodiumComponent comp, out int guilty, out int innocent)
        {
            guilty = 0;
            innocent = 0;

            foreach (var vote in comp.Votes.Values)
            {
                if (vote)
                    guilty++;
                else
                    innocent++;
            }
        }

        private void ExecutePrisoner(EntityUid? prisoner)
        {
            if (prisoner == null || !Exists(prisoner))
                return;

            // SS14 uses specific string IDs for damage types. "Asphyxiation" is standard.
            var damageSpec = new DamageSpecifier();
            damageSpec.DamageDict.Add("Asphyxiation", 200);

            // 'ignoreResistances: true' ensures armor or internals don't accidentally block the execution.
            _damageable.TryChangeDamage(prisoner.Value, damageSpec, ignoreResistances: true);

            _chat.DispatchGlobalAnnouncement(
                $"{Name(prisoner.Value)} has been suffocated by order of the Tribunal.",
                "Tribunal AI",
                colorOverride: Color.Tomato);
        }
    }
}
