// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Megafauna.Systems;

/// <summary>
/// Makes megafauna bosses sidestep hostile projectiles: the hit is cancelled,
/// the boss blinks a short distance to a clear tile and leaves a visual effect.
/// </summary>
public sealed class MegafaunaDodgeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    // Reused teleport shimmer, so the dodge needs no extra prototype.
    private static readonly EntProtoId DodgeEffect = "EffectDesynchronizer";
    private static readonly SoundSpecifier DodgeSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // Every staged megafauna is a boss fight, so the dodge comes with the stage
        // component instead of being repeated in every boss prototype. BossHudSystem already
        // owns the MapInit subscription for the stage component, so use ComponentStartup here.
        SubscribeLocalEvent<MegafaunaStageComponent, ComponentStartup>(OnBossStartup);
        SubscribeLocalEvent<MegafaunaDodgeComponent, ProjectileReflectAttemptEvent>(OnProjectileHit);
    }

    private void OnBossStartup(Entity<MegafaunaStageComponent> ent, ref ComponentStartup args)
        => EnsureComp<MegafaunaDodgeComponent>(ent);

    private void OnProjectileHit(Entity<MegafaunaDodgeComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled
            || !ent.Comp.Enabled
            || HasComp<MegafaunaAnchorComponent>(ent)
            || !_mobState.IsAlive(ent)
            || _timing.CurTime < ent.Comp.NextDodge)
            return;

        // Only dodge hostile fire: never the boss's own shots or those of its allies.
        if (args.Component.Shooter is not { } shooter
            || shooter == ent.Owner
            || _faction.IsEntityFriendly(ent.Owner, shooter))
            return;

        if (!_random.Prob(ent.Comp.DodgeChance))
            return;

        if (!TryFindDodgeSpot(ent, ent.Comp.Distance, out var spot))
            return;

        args.Cancelled = true;
        ent.Comp.NextDodge = _timing.CurTime + ent.Comp.Cooldown;

        Spawn(DodgeEffect, Transform(ent).Coordinates);
        _xform.SetCoordinates(ent, spot);
        _audio.PlayPvs(DodgeSound, ent);
    }

    private bool TryFindDodgeSpot(EntityUid uid, float distance, out EntityCoordinates spot)
    {
        spot = default;
        var origin = Transform(uid).Coordinates;

        // A few tries because a random direction can point into a wall.
        for (var i = 0; i < 6; i++)
        {
            var candidate = origin.Offset(_random.NextAngle().ToVec() * distance);
            if (!_turf.TryGetTileRef(candidate, out var tileRef)
                || _turf.IsTileBlocked(tileRef.Value, CollisionGroup.MobMask))
                continue;

            spot = candidate.SnapToGrid(EntityManager);
            return true;
        }

        return false;
    }
}
