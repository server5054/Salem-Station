using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared.CovenHexed;

[RegisterComponent, NetworkedComponent]
public sealed partial class CovenHexedComponent : Component
{
    /// <summary>
    /// The exact timestamp when this mark will automatically detonate.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan DetonationTime;

    /// <summary>
    /// Tracks who cast the mark so they get credited for the damage/kill.
    /// </summary>
    [DataField]
    public EntityUid? Caster;

    /// <summary>
    /// Is the 5-minute countdown actively ticking?
    /// </summary>
    [DataField]
    public bool TimerStarted = false;
}
