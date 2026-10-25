// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Behaviour of a single holographic clone: it splits itself on melee attacks and disappears on death.
/// </summary>
public sealed class NinjaGhostCloneSystem : EntitySystem
{
    [Dependency] private readonly NinjaGhostClonesSystem _clones = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostCloneComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<NinjaGhostCloneComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMeleeAttack(Entity<NinjaGhostCloneComponent> ent, ref MeleeAttackEvent args)
    {
        var comp = ent.Comp;

        if (!_random.Prob(comp.CloningChance))
            return;

        var origin = Transform(ent).Coordinates;
        if (TryFindFreeCoords(origin, comp.MinSpawnRadius, comp.MaxSpawnRadius) is not { } coords)
            return;

        _clones.SpawnVisualClone(ent.Owner, coords, comp.CloneProto, comp.Owner);
    }

    private void OnMobStateChanged(Entity<NinjaGhostCloneComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            QueueDel(ent.Owner);
    }

    private EntityCoordinates? TryFindFreeCoords(EntityCoordinates origin, float minRadius, float maxRadius)
    {
        for (var i = 0; i < 20; i++)
        {
            var offset = _random.NextVector2(minRadius, maxRadius);
            var coords = origin.Offset(offset);
            if (!_lookup.AnyEntitiesIntersecting(coords, LookupFlags.Static))
                return coords;
        }

        return null;
    }
}