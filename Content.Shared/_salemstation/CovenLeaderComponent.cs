using Content.Shared.Dataset;
using Content.Shared.Objectives.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Antag;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CovenLeader;

[RegisterComponent, NetworkedComponent]
public sealed partial class CovenLeaderComponent : Component {


    /// <summary>
    /// This is used for tagging a mob as a coven leader.
    /// </summary>

    [DataField]
	public bool Has_Necro = false;



	
	
	
}
