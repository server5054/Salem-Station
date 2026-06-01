using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robust.Shared.GameStates;

namespace Content.Shared.CovenMindSwappingWeapon;

[RegisterComponent, NetworkedComponent]
public sealed partial class CovenMindSwappingWeaponComponent : Component
{
    // How long the temporary swap should last when a hit connects
    [DataField] public float SwapDuration = 30f;
}
