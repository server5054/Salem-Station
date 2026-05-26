using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared.Roles;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.CovenRule;

[RegisterComponent]
public sealed partial class CovenRuleComponent : Component
{
    [DataField]
    public List<EntityUid> ActiveAntags = new();

    // This list will automatically populate from the YAML config above
    [DataField(required: true)]
    public List<AntagFactionRoleConfig> Roles = new();

    public bool WeWon = false;
}

[DataDefinition]
public sealed partial class AntagFactionRoleConfig
{
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
    public string Id = string.Empty;

    [DataField]
    public int Min = 1;

    [DataField]
    public int Max = 1;
}
