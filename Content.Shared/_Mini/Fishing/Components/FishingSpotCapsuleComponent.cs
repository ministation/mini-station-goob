// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing/fishing_equipment.dm,
// code/modules/mining/shelters.dm, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A single-use capsule that deploys a fishing spot connected to some faraway place.
/// Activate in hand to deploy the selected spot, alt-interact (right click) to change it.
/// Unsafe spots (lava, plasma, deep ocean) stay locked until the capsule is emagged.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishingSpotCapsuleComponent : Component
{
    /// <summary>
    /// Fishing spots the capsule can deploy. Unsafe ones are hidden until emagged.
    /// </summary>
    [DataField(required: true)]
    public List<FishingSpotCapsuleOption> Spots = new();

    /// <summary>
    /// The spot that will be deployed, identified by its fishing source id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<FishingSourcePrototype> SelectedSource = default!;

    [DataField, AutoNetworkedField]
    public bool Emagged;

    [DataField]
    public SoundSpecifier? SelectSound = new SoundPathSpecifier("/Audio/Effects/Cargo/beep.ogg");
}

[DataDefinition]
public sealed partial class FishingSpotCapsuleOption
{
    /// <summary>
    /// The fishing source the deployed spot uses.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<FishingSourcePrototype> Source = default!;

    /// <summary>
    /// The spot entity that is spawned on deploy.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Spot = default!;

    /// <summary>
    /// A water overlay is spawned under the spot (lava and plasma pools skip it).
    /// </summary>
    [DataField]
    public bool WaterOverlay = true;

    /// <summary>
    /// Icon state in the fishing radial sheet.
    /// </summary>
    [DataField(required: true)]
    public string RadialState = default!;

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public LocId Description = string.Empty;

    /// <summary>
    /// Unsafe spots require an emagged capsule, like in tg.
    /// </summary>
    [DataField]
    public bool Safe = true;
}
