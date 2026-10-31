// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared.EntityTable;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A treasure chest fished out of the ocean. Clicking it presents a random prize above the lid,
/// clicking again takes it. Every player can pull a few items before the chest runs dry.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishingTreasureChestComponent : Component
{
    /// <summary>
    /// Total pulls the chest can give before it falls apart.
    /// </summary>
    [DataField]
    public int UsesLeft = 18;

    /// <summary>
    /// How many items a single player can pull out of the same chest.
    /// </summary>
    [DataField]
    public int PullsPerPlayer = 3;

    /// <summary>
    /// Weighted loot table rolled on every pull.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Table = default!;

    /// <summary>
    /// Decorative entity that shows the rolling prize above the chest.
    /// </summary>
    [DataField]
    public EntProtoId PrizePrototype = "FishingTreasureChestPrize";

    /// <summary>
    /// How long the prize flickers before it settles.
    /// </summary>
    [DataField]
    public float ChoosingTime = 1.6f;

    /// <summary>
    /// How long a settled prize waits to be taken before the chest closes.
    /// </summary>
    [DataField]
    public float PresentTime = 6f;

    /// <summary>
    /// Delay after closing before the chest can be opened again.
    /// </summary>
    [DataField]
    public float Cooldown = 2f;

    /// <summary>
    /// Current presentational state, read by the client to swap the lid and show the prize.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FishingTreasureChestState State = FishingTreasureChestState.Closed;

    /// <summary>
    /// Loot prototype currently shown above the chest, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Prize;
}

public enum FishingTreasureChestState : byte
{
    Closed,
    Choosing,
    Open,
}

/// <summary>
/// Decorative prize sprite floating above a treasure chest. The client resolves
/// <see cref="Prize"/> to a sprite so the chest itself does not need to touch client resources.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FishingTreasureChestPrizeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Prize;
}
