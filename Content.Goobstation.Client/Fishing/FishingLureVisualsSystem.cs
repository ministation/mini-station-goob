// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Goobstation.Shared.Fishing.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Fishing;

/// <summary>
/// Bobs the float while waiting and shakes it while a fish is biting, same as the tg float animation.
/// </summary>
public sealed partial class FishingLureVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = (float) _timing.RealTime.TotalSeconds;

        var query = EntityQueryEnumerator<FishingLureComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var lure, out var sprite))
        {
            if (lure.Biting)
            {
                // Fast nibble shake: a small horizontal jitter plus a dip into the water.
                var offset = new Vector2(
                    MathF.Sin(time * 42f) * 0.12f,
                    MathF.Abs(MathF.Sin(time * 26f)) * 0.3f - 0.16f);
                _sprite.SetOffset((uid, sprite), offset);
            }
            else
            {
                _sprite.SetOffset((uid, sprite), new Vector2(0f, MathF.Sin(time * 2.2f) * 0.06f));
            }
        }
    }
}
