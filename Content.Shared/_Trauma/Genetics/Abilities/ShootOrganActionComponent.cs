// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Polymorph;

namespace Content.Shared._Trauma.Genetics.Abilities;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShootOrganActionComponent : Component
{
    [DataField(required: true)]
    public string Organ = string.Empty;

    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Polymorph;
}

public sealed partial class ShootOrganActionEvent : WorldTargetActionEvent;
