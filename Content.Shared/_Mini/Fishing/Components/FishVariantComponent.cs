// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A fish that picks one of several cosmetic forms on spawn, like the fritterish variants.
/// The chosen form is applied through a <see cref="Content.Shared.Sprite.RandomSpriteComponent"/>,
/// so the sprite is replicated by the existing random sprite system.
/// </summary>
[RegisterComponent]
public sealed partial class FishVariantComponent : Component
{
    [DataField(required: true)]
    public List<FishVariantEntry> Variants = new();

    [ViewVariables]
    public int Variant = -1;
}

[DataDefinition]
public sealed partial class FishVariantEntry
{
    [DataField]
    public string? SpriteState;

    [DataField]
    public LocId? Name;

    [DataField]
    public LocId? Description;
}
