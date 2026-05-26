using Robust.Shared.GameStates;

namespace Content.Shared.CovenMindSwap;

[RegisterComponent, NetworkedComponent]
public sealed partial class CovenMindSwapComponent : Component {
	
	
	[ViewVariables(VVAccess.ReadOnly)]
    public bool Unremoveable = true;
	
}
