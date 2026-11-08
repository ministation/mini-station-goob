// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Base.Refill;

/// <summary>
/// Lets the ninja reload limited charge actions of the suit by feeding it materials.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaSuitRefillComponent : Component
{
    [DataField]
    public Dictionary<EntProtoId, NinjaActionMaterialCost> ActionMaterials = new();
}

[DataDefinition]
public sealed partial class NinjaActionMaterialCost
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Stack;

    [DataField(required: true)]
    public int Amount;
}
