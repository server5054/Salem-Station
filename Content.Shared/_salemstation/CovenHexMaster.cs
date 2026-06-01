using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

namespace Content.Shared.CovenHexMaster;

[RegisterComponent, NetworkedComponent]
public sealed partial class CovenHexMasterComponent : Component
{
    [DataField] public EntityUid? ActionHexPlayerEntity;
    [DataField] public EntityUid? ActionTriggerHexEntity;
}
