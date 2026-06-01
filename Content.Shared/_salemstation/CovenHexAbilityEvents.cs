using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Actions;

namespace Content.Shared.CovenHexAbilityEvents;
// Needs to inherit from WorldTargetActionEvent because it targets a coordinate
public sealed partial class CovenHexPlayerActionEvent : InstantActionEvent
{
    public CovenHexPlayerActionEvent()
    {

    }
}

// Needs to inherit from InstantActionEvent because it fires immediately on click
public sealed partial class CovenTriggerHexActionEvent : InstantActionEvent
{
    public CovenTriggerHexActionEvent()
    {

    }
}
