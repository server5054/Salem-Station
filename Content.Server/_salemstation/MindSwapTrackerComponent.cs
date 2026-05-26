using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.MindSwapTracker
{

    [RegisterComponent]
    public sealed partial class MindSwapTrackerComponent : Component
    {

        // The entity this body swapped with
        [DataField] public EntityUid Partner;

        // Time left in seconds before the swap reverses
        [DataField] public float TimeRemaining;

        // Keeps track of whether this side of the component should handle the reversal 
        // (prevents double-reversing when updating both components)
        [DataField] public bool IsMaster = false;
    }
}
