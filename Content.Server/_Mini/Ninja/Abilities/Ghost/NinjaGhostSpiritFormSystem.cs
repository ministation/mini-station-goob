// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Maps;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Physics;
using Content.Shared.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 5: the spirit form. The wearer loses the impassable collision and can walk through walls,
/// while the suit drains a part of the cell capacity every second. When the form ends inside a wall the
/// wearer is pushed out to the nearest free tile.
/// </summary>
public sealed class NinjaGhostSpiritFormSystem : EntitySystem
{
    private const float PushOutRadius = 5f;

    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    /// Wearer of every suit with an active form.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _active = new();

    /// <summary>
    /// Seconds accumulated since the last charge tick, per suit.
    /// </summary>
    private readonly Dictionary<EntityUid, float> _drainAccumulator = new();

    /// <summary>
    /// Original collision masks of every entity that is currently a ghost, per fixture.
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<string, int>> _originalMasks = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, NinjaGhostSpiritFormEvent>(OnAction);
        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, NinjaSuitPowerChangedEvent>(OnSuitPowerChanged);
        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaGhostSpiritFormComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SpaceNinjaComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<SpaceNinjaComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<SpaceNinjaComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    private void OnAction(Entity<NinjaGhostSpiritFormComponent> ent, ref NinjaGhostSpiritFormEvent args)
    {
        if (args.Handled)
            return;

        var suit = ent.Owner;
        var user = args.Performer;

        if (Transform(suit).ParentUid != user)
            return;

        if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            return;

        args.Handled = true;

        if (ent.Comp.Active)
        {
            Deactivate(ent, user);
            return;
        }

        if (!TryGetMaxCharge(suit, out var maxCharge))
        {
            _suitPower.PopupNoPower(user);
            return;
        }

        var cost = maxCharge * ent.Comp.EnergyDrainPercent;

        if (!_suitPower.HasCharge(user, cost))
        {
            _suitPower.PopupNoPower(user);
            return;
        }

        if (!_suitPower.TryUseCharge(user, cost))
            return;

        Activate(ent, user);
    }

    private void Activate(Entity<NinjaGhostSpiritFormComponent> ent, EntityUid wearer)
    {
        ent.Comp.Active = true;
        Dirty(ent);

        _active[ent.Owner] = wearer;
        _drainAccumulator[ent.Owner] = 0f;

        SetSpiritCollision(wearer, true);
        _audio.PlayPvs(ent.Comp.ActivateSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("ninja-ghost-spiritform-on"), wearer, wearer);
    }

    private void Deactivate(Entity<NinjaGhostSpiritFormComponent> ent, EntityUid wearer)
    {
        var wasActive = ent.Comp.Active;

        ent.Comp.Active = false;
        _active.Remove(ent.Owner);
        _drainAccumulator.Remove(ent.Owner);
        Dirty(ent);

        if (wasActive)
        {
            _audio.PlayPvs(ent.Comp.DeactivateSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("ninja-ghost-spiritform-off"), wearer, wearer);
        }

        if (!wearer.IsValid())
            return;

        SetSpiritCollision(wearer, false);
        TryPushOutOfWall(wearer);
    }

    private void OnSuitPowerChanged(Entity<NinjaGhostSpiritFormComponent> ent, ref NinjaSuitPowerChangedEvent args)
    {
        if (!args.Activated)
            Deactivate(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<NinjaGhostSpiritFormComponent> ent, ref GotUnequippedEvent args)
    {
        if (_active.TryGetValue(ent.Owner, out var wearer))
            Deactivate(ent, wearer);
    }

    private void OnShutdown(Entity<NinjaGhostSpiritFormComponent> ent, ref ComponentShutdown args)
    {
        if (_active.TryGetValue(ent.Owner, out var wearer))
            Deactivate(ent, wearer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaGhostSpiritFormComponent>();
        while (query.MoveNext(out var suit, out var comp))
        {
            if (!comp.Active)
                continue;

            _active.TryGetValue(suit, out var wearer);

            if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated || !wearer.IsValid())
            {
                Deactivate((suit, comp), wearer);
                continue;
            }

            _drainAccumulator[suit] = _drainAccumulator.GetValueOrDefault(suit) + frameTime;

            while (_drainAccumulator[suit] >= 1f)
            {
                _drainAccumulator[suit] -= 1f;

                if (!TryGetMaxCharge(suit, out var maxCharge))
                {
                    Deactivate((suit, comp), wearer);
                    break;
                }

                var cost = maxCharge * comp.EnergyDrainPercent;

                if (!_suitPower.HasCharge(wearer, cost) || !_suitPower.TryUseCharge(wearer, cost))
                {
                    Deactivate((suit, comp), wearer);
                    break;
                }
            }
        }
    }

    private bool TryGetMaxCharge(EntityUid suit, out float maxCharge)
    {
        maxCharge = 0f;

        if (!_powerCell.TryGetBatteryFromSlot(suit, out var battery))
            return false;

        maxCharge = battery.Value.Comp.MaxCharge;
        return maxCharge > 0f;
    }

    /// <summary>
    /// Removes or restores the impassable bits of every fixture of the entity, keeping the layers untouched.
    /// </summary>
    private void SetSpiritCollision(EntityUid uid, bool enabled)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) || !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        if (enabled)
        {
            if (_originalMasks.ContainsKey(uid))
                return;

            var masks = new Dictionary<string, int>(fixtures.Fixtures.Count);

            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                masks[id] = fixture.CollisionMask;

                var mask = fixture.CollisionMask
                           & ~(int) CollisionGroup.Impassable
                           & ~(int) CollisionGroup.MidImpassable
                           & ~(int) CollisionGroup.HighImpassable
                           & ~(int) CollisionGroup.LowImpassable;

                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures, physics);
            }

            _originalMasks[uid] = masks;
            return;
        }

        if (!_originalMasks.Remove(uid, out var original))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (original.TryGetValue(id, out var mask))
                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures, physics);
        }
    }

    /// <summary>
    /// Moves the entity to the nearest free tile if the form ends inside a wall.
    /// </summary>
    private void TryPushOutOfWall(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_turf.TryGetTileRef(xform.Coordinates, out var tile) ||
            !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
        {
            return;
        }

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var maxRadius = (int) PushOutRadius;

        for (var radius = 1; radius <= maxRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    var candidate = _map.GridTileToLocal(gridUid, grid, indices + new Vector2i(dx, dy));
                    if (!_turf.TryGetTileRef(candidate, out var candidateTile) ||
                        _turf.IsSpace(candidateTile.Value) ||
                        _turf.IsTileBlocked(candidateTile.Value, CollisionGroup.Impassable))
                    {
                        continue;
                    }

                    _xform.SetCoordinates(uid, candidate);
                    _popup.PopupEntity(Loc.GetString("ninja-ghost-spiritform-pushed"), uid, uid);
                    return;
                }
            }
        }
    }

    private void OnAttackAttempt(Entity<SpaceNinjaComponent> ent, ref AttackAttemptEvent args)
    {
        if (IsSpiritFormActive(ent.Owner))
            args.Cancel();
    }

    private void OnUseAttempt(Entity<SpaceNinjaComponent> ent, ref UseAttemptEvent args)
    {
        if (IsSpiritFormActive(ent.Owner))
            args.Cancel();
    }

    private void OnInteractionAttempt(Entity<SpaceNinjaComponent> ent, ref InteractionAttemptEvent args)
    {
        if (IsSpiritFormActive(ent.Owner))
            args.Cancelled = true;
    }

    private bool IsSpiritFormActive(EntityUid wearer)
    {
        if (!TryComp<SpaceNinjaComponent>(wearer, out var ninja) || ninja.Suit is not { } suit)
            return false;

        return TryComp<NinjaGhostSpiritFormComponent>(suit, out var form) && form.Active;
    }
}