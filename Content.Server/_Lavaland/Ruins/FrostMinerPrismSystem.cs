// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.BossMechanics;
using Content.Shared.Light;
using Content.Shared.Sprite;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// TG parity for <see cref="FrostMinerPrismComponent"/>: the arena prisms follow the
/// state of their demonic master.
/// </summary>
public sealed class FrostMinerPrismSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FrostMinerEnragedEvent>(OnEnraged);
        SubscribeLocalEvent<FrostMinerDiedEvent>(OnDied);
    }

    private void OnEnraged(ref FrostMinerEnragedEvent args)
    {
        SetAllPrisms(prism => (prism.EnragedColor, prism.EnragedLightColor));
    }

    private void OnDied(ref FrostMinerDiedEvent args)
    {
        SetAllPrisms(prism => (prism.DeadColor, prism.DeadLightColor));
    }

    private void SetAllPrisms(Func<FrostMinerPrismComponent, (Color Sprite, Color? Light)> colors)
    {
        var query = EntityQueryEnumerator<FrostMinerPrismComponent>();
        while (query.MoveNext(out var uid, out var prism))
        {
            var (spriteColor, lightColor) = colors(prism);

            // Same server-side sprite shortcut the miner's enrage uses: tint through RandomSprite.
            var sprite = EnsureComp<RandomSpriteComponent>(uid);
            sprite.Selected[prism.Layer] = (prism.State, spriteColor);
            Dirty(uid, sprite);

            if (lightColor != null)
                _light.SetColor(uid, lightColor.Value);
        }
    }
}
