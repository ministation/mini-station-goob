// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Sprite;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Picks a random cosmetic form for a fish on spawn.
/// </summary>
public sealed class FishVariantSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishVariantComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FishVariantComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Variants.Count == 0)
            return;

        ent.Comp.Variant = _random.Next(ent.Comp.Variants.Count);
        var variant = ent.Comp.Variants[ent.Comp.Variant];

        if (variant.Name != null)
            _metaData.SetEntityName(ent, Loc.GetString(variant.Name));

        if (variant.Description != null)
            _metaData.SetEntityDescription(ent, Loc.GetString(variant.Description));

        if (variant.SpriteState == null)
            return;

        var randomSprite = EnsureComp<RandomSpriteComponent>(ent);
        randomSprite.Selected["0"] = (variant.SpriteState, null);
        Dirty(ent.Owner, randomSprite);
    }
}
