using Content.Shared._salemstation;
using Robust.Server.GameObjects;

namespace Content.Server._salemstation
{
    // UI half of TribunalPodiumSystem: vote messages in, window state out.
    // The window itself is opened by the ActivatableUI component on the podium.
    public sealed partial class TribunalPodiumSystem
    {
        [Dependency] private readonly UserInterfaceSystem _ui = default!;

        private void InitializeBui()
        {
            SubscribeLocalEvent<TribunalPodiumComponent, TribunalVoteMessage>(OnVoteMessage);
            SubscribeLocalEvent<TribunalPodiumComponent, BoundUIOpenedEvent>(OnBuiOpened);
        }

        // Push current state to a player the moment they open the window.
        private void OnBuiOpened(EntityUid uid, TribunalPodiumComponent component, BoundUIOpenedEvent args)
        {
            UpdateUserInterface(uid, component);
        }

        private void OnVoteMessage(EntityUid uid, TribunalPodiumComponent component, TribunalVoteMessage args)
        {
            if (component.CurrentState != TrialState.Voting)
                return;

            // One vote per crewmate; voting again changes their choice.
            component.Votes[args.Actor] = args.VoteGuilty;

            UpdateUserInterface(uid, component);
        }

        private void UpdateUserInterface(EntityUid uid, TribunalPodiumComponent component)
        {
            CountVotes(component, out var guilty, out var innocent);

            var prisonerName = component.Prisoner is { } prisoner && Exists(prisoner)
                ? Name(prisoner)
                : "None";

            var remaining = component.StateEndTime - _timing.CurTime;
            if (component.CurrentState == TrialState.Idle || remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            var state = new TribunalBoundUserInterfaceState(
                prisonerName,
                guilty,
                innocent,
                remaining,
                component.CurrentState.ToString());

            _ui.SetUiState(uid, TribunalUiKey.Key, state);
        }
    }
}
