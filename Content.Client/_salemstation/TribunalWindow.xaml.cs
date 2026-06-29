using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Content.Shared.TribunalUIShared;

namespace Content.Client.TribunalWindowDef
{
    // REMOVED: [GenerateTypedNameReferences] attribute
    public sealed partial class TribunalWindow : DefaultWindow
    {
        public event Action<bool>? OnVotePressed;

        // Manually bind the controls by looking up their XAML names at runtime
        private Label PrisonerLabel => this.FindControl<Label>("PrisonerLabel");
        private Label PhaseLabel => this.FindControl<Label>("PhaseLabel");
        private Label TimerLabel => this.FindControl<Label>("TimerLabel");
        private Button GuiltyButton => this.FindControl<Button>("GuiltyButton");
        private Button InnocentButton => this.FindControl<Button>("InnocentButton");

        public TribunalWindow()
        {
            RobustXamlLoader.Load(this);

            GuiltyButton.OnPressed += _ => OnVotePressed?.Invoke(true);
            InnocentButton.OnPressed += _ => OnVotePressed?.Invoke(false);
        }

        public void UpdateState(TribunalBoundUserInterfaceState state)
        {
            PrisonerLabel.Text = $"Accused: {state.PrisonerName}";
            PhaseLabel.Text = $"Current Phase: {state.CurrentPhase}";
            TimerLabel.Text = $"Time Remaining: {state.TimeRemaining.Minutes}:{state.TimeRemaining.Seconds:D2}";

            GuiltyButton.Text = $"Guilty ({state.GuiltyVotes})";
            InnocentButton.Text = $"Innocent ({state.InnocentVotes})";

            var isVotingPhase = state.CurrentPhase == "Voting";
            GuiltyButton.Disabled = !isVotingPhase;
            InnocentButton.Disabled = !isVotingPhase;
        }
    }
}
