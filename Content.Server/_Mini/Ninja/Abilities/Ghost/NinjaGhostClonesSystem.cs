// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 3: holographic clones. Spawns several copies of the ninja around the wearer and swaps
/// places with one of them, so the enemies lose track of the real ninja.
/// </summary>
public sealed class NinjaGhostClonesSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly NinjaGhostSmokeSystem _smoke = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostClonesComponent, NinjaGhostClonesEvent>(OnUse);
    }

    private void OnUse(Entity<NinjaGhostClonesComponent> ent, ref NinjaGhostClonesEvent args)
    {
        if (args.Handled)
            return;

        var suit = ent.Owner;
        var user = args.Performer;
        var comp = ent.Comp;

        if (Transform(suit).ParentUid != user)
            return;

        if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            return;

        if (comp.CloneAmount < 1)
            return;

        var origin = Transform(user).Coordinates;
        var spawnCoords = new List<EntityCoordinates>();

        for (var i = 0; i < comp.CloneAmount; i++)
        {
            if (TryFindFreeCoords(origin, comp.MinSpawnRadius, comp.MaxSpawnRadius, comp.MaxSpawnAttempts) is { } coords)
                spawnCoords.Add(coords);
        }

        if (spawnCoords.Count == 0)
            return;

        if (!_suitPower.HasCharge(user, comp.EnergyCost))
        {
            _suitPower.PopupNoPower(user);
            return;
        }

        if (!_suitPower.TryUseCharge(user, comp.EnergyCost))
            return;

        _smoke.TryAutoSmoke(suit);

        var clones = new List<EntityUid>();
        foreach (var coords in spawnCoords)
        {
            clones.Add(SpawnVisualClone(user, coords, comp.CloneProto, user));
        }

        // The trick itself: the ninja appears where a clone stood, and the clone takes the ninja's place.
        var userCoords = Transform(user).Coordinates;
        var selected = _random.Pick(clones);
        var cloneCoords = Transform(selected).Coordinates;

        _xform.SetCoordinates(user, cloneCoords);
        _xform.SetCoordinates(selected, userCoords);

        args.Handled = true;
    }

    /// <summary>
    /// Spawns a holographic clone of the source entity and makes it look like the source.
    /// </summary>
    public EntityUid SpawnVisualClone(EntityUid source, EntityCoordinates coords, EntProtoId cloneProto, EntityUid owner)
    {
        var clone = Spawn(cloneProto, coords);

        EnsureComp<NinjaGhostCloneComponent>(clone).Owner = owner;

        if (TryComp<HumanoidAppearanceComponent>(source, out var sourceAppearance))
        {
            var cloneAppearance = EnsureComp<HumanoidAppearanceComponent>(clone);
            _humanoid.SetSpecies(clone, sourceAppearance.Species.Id, sync: false);
            _humanoid.SetSex(clone, sourceAppearance.Sex, sync: false);
            _humanoid.CloneAppearance(source, clone, sourceAppearance, cloneAppearance);
        }

        _metaData.SetEntityName(clone, MetaData(source).EntityName);

        var visual = EnsureComp<NinjaGhostCloneVisualComponent>(clone);
        visual.Source = GetNetEntity(source);
        Dirty(clone, visual);

        if (TryComp<TimedDespawnComponent>(source, out var despawn))
            EnsureComp<TimedDespawnComponent>(clone).Lifetime = despawn.Lifetime;

        return clone;
    }

    private EntityCoordinates? TryFindFreeCoords(EntityCoordinates origin, float minRadius, float maxRadius, int attempts)
    {
        for (var i = 0; i < attempts; i++)
        {
            var offset = _random.NextVector2(minRadius, maxRadius);
            var coords = origin.Offset(offset);
            if (!_lookup.AnyEntitiesIntersecting(coords, LookupFlags.Static))
                return coords;
        }

        return null;
    }
}