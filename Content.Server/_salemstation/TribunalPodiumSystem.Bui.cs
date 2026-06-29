using Content.Server.TribunalPodiumDef;
using Content.Shared.TribunalUIShared;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robust.Shared.Timing;

namespace Content.Server.TribunalPodiumSystemBuiDef
{
    public sealed partial class TribunalPodiumSystem : EntitySystem
    {
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private void InitializeBui()
        {
            // Subscribe to the event when a player interacts with the UI
            SubscribeLocalEvent<TribunalPodiumComponent, BoundUIClosedEvent>(OnBuiClosed);

        }

        // Triggered when a player clicks the podium without an item in hand
        private void OpenPodiumUi(EntityUid uid, TribunalPodiumComponent component, EntityUid user)
        {
            if (!_ui.HasUi(uid, TribunalUiKey.Key)) return;

            _ui.OpenUi(uid, TribunalUiKey.Key, user);
            UpdateUserInterface(uid, component);
        }

        private void OnBuiClosed(EntityUid uid, TribunalPodiumComponent component, BoundUIClosedEvent args)
        {
            // Left blank intentionally. No cleanup needed when a user closes the window.
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
    }
}
