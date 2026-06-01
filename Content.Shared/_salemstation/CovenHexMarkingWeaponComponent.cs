using Robust.Shared.GameStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared.CovenHexMarkingWeapon;

[RegisterComponent, NetworkedComponent]
/// <summary>
/// Placed on the spawned weapon so the system knows to apply the mark on melee hits.
/// </summary>
public sealed partial class HexMarkingWeaponComponent : Component { }
