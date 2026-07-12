using Content.Shared._salemstation;
using Robust.Client.UserInterface;

namespace Content.Client._salemstation
{
    /// <summary>
    /// Client-side controller for the tribunal podium UI. The entity prototype's
    /// UserInterface component points at this class; it owns the window, forwards
    /// button presses to the server and applies incoming state updates.
    /// </summary>
    public sealed class TribunalBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private TribunalWindow? _window;

        public TribunalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<TribunalWindow>();
            _window.OnVotePressed += voteGuilty => SendMessage(new TribunalVoteMessage(voteGuilty));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not TribunalBoundUserInterfaceState cast)
                return;

            _window?.UpdateState(cast);
        }
    }
}
