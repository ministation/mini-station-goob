// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Random.Helpers;
using Content.Shared.Throwing;
using Content.Shared._Trauma.Genetics.EntityEffects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Trauma.Genetics.EntityEffects;

/// <summary>
/// Throws the target entity in a random direction, with a fixed speed.
/// </summary>
public sealed partial class ThrowRandomly : BaseThrowEntityEffect<ThrowRandomly>;

public sealed partial class ThrowRandomlyEffectSystem : EntityEffectSystem<MetaDataComponent, ThrowRandomly>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    protected override void Effect(Entity<MetaDataComponent> ent, ref EntityEffectEvent<ThrowRandomly> args)
    {
        var rand = IoCManager.Resolve<IRobustRandom>();
        var angle = rand.NextAngle();
        var direction = angle.ToVec();

        var effect = args.Effect;
        _throwing.TryThrow(ent,
            direction,
            baseThrowSpeed: effect.Speed,
            user: args.User);
    }
}
