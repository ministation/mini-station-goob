// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Gatherable;
using Content.Server.Gatherable.Components;
using Content.Shared._Lavaland.Weapons.Resonator;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons.Resonator;

/// <summary>
/// Handles the resonator mining tool and its resonance fields.
/// </summary>
public sealed class ResonatorSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GatherableSystem _gatherable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string FieldPrototype = "LavalandResonanceField";
    private const string BurstEffect = "BulletImpactEffectKinetic";

    /// <summary>
    /// How long a field must exist before clicking it detonates it early, so rapid clicks
    /// cannot turn the resonator into a burst cannon against fauna.
    /// </summary>
    private const float QuickBurstMinAge = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResonatorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ResonatorComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnUseInHand(Entity<ResonatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var comp = ent.Comp;

        comp.Mode = comp.Mode switch
        {
            ResonatorMode.Auto => ResonatorMode.Manual,
            ResonatorMode.Manual => comp.MatrixMode ? ResonatorMode.Matrix : ResonatorMode.Auto,
            _ => ResonatorMode.Auto,
        };

        var key = comp.Mode switch
        {
            ResonatorMode.Auto => "resonator-mode-auto",
            ResonatorMode.Manual => "resonator-mode-manual",
            _ => "resonator-mode-matrix",
        };

        _popup.PopupEntity(Loc.GetString(key), ent.Owner, args.User);
    }

    private void OnAfterInteract(Entity<ResonatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        args.Handled = true;
        // Aim at the clicked rock tile instead of the raw click point so the field
        // always covers the rock that was clicked.
        var coords = args.Target is { } target && HasComp<GatherableComponent>(target)
            ? Transform(target).Coordinates
            : args.ClickLocation;

        foreach (var (fieldUid, field) in _lookup.GetEntitiesInRange<ResonanceFieldComponent>(coords, 0.6f))
        {
            if (field.Bursting)
                continue;

            // A field that was just placed cannot be detonated on demand yet, otherwise
            // alternating create/burst clicks deal burst damage once per click.
            if (_timing.CurTime - field.CreatedAt < TimeSpan.FromSeconds(QuickBurstMinAge))
                return;

            field.DamageMultiplier = ent.Comp.QuickBurstMod;
            Burst((fieldUid, field));
            return;
        }

        if (CountFields(ent.Owner) >= ent.Comp.FieldLimit)
        {
            _popup.PopupEntity(Loc.GetString("resonator-field-limit"), ent.Owner, args.User);
            return;
        }

        CreateField(coords, args.User, ent.Owner, ent.Comp.Mode, 1f, 0);
    }

    private int CountFields(EntityUid resonator)
    {
        var count = 0;
        var query = EntityQueryEnumerator<ResonanceFieldComponent>();
        while (query.MoveNext(out _, out var field))
        {
            if (field.Resonator == resonator)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Spawns a resonance field. Returns null if the field could not be created.
    /// </summary>
    public EntityUid? CreateField(
        EntityCoordinates coords,
        EntityUid? creator,
        EntityUid? resonator,
        ResonatorMode mode,
        float damageMultiplier,
        int failure)
    {
        var field = Spawn(FieldPrototype, coords);
        var comp = Comp<ResonanceFieldComponent>(field);

        comp.Creator = creator;
        comp.Resonator = resonator;
        comp.CreatedAt = _timing.CurTime;
        comp.DamageMultiplier = damageMultiplier;
        comp.FailureProb = failure;
        comp.Matrix = mode == ResonatorMode.Matrix;

        var autoDelay = 2f;
        var matrixLifetime = 60f;
        var manualLifetime = 60f; // shortcut: hardcoded TG 60 s, add a ResonatorComponent field if it needs tuning.

        if (resonator is { } resonatorUid && TryComp<ResonatorComponent>(resonatorUid, out var resonatorComp))
        {
            comp.AddingFailure = resonatorComp.AddingFailure;
            autoDelay = resonatorComp.AutoBurstDelay;
            matrixLifetime = resonatorComp.MatrixLifetime;
            _audio.PlayPvs(resonatorComp.FireSound, coords);
        }

        comp.BurstAt = mode switch
        {
            ResonatorMode.Auto => _timing.CurTime + TimeSpan.FromSeconds(autoDelay),
            ResonatorMode.Matrix => _timing.CurTime + TimeSpan.FromSeconds(matrixLifetime),
            _ => _timing.CurTime + TimeSpan.FromSeconds(manualLifetime),
        };

        return field;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        List<Entity<ResonanceFieldComponent>>? due = null;
        var query = EntityQueryEnumerator<ResonanceFieldComponent>();
        while (query.MoveNext(out var uid, out var field))
        {
            if (field.Bursting)
                continue;

            if (field.Matrix
                && HasAliveMob(Transform(uid).Coordinates, 0.5f))
            {
                (due ??= new()).Add((uid, field));
                continue;
            }

            // Fields spawned directly (not by a resonator) have no burst timer.
            if (field.BurstAt != TimeSpan.Zero && field.BurstAt != TimeSpan.MaxValue && _timing.CurTime >= field.BurstAt)
                (due ??= new()).Add((uid, field));
        }

        if (due == null)
            return;

        foreach (var field in due)
        {
            if (TerminatingOrDeleted(field))
                continue;

            Burst(field);
        }
    }

    private bool HasAliveMob(EntityCoordinates coords, float range)
    {
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, range))
        {
            if (_mobState.IsAlive(mob))
                return true;
        }

        return false;
    }

    public void Burst(Entity<ResonanceFieldComponent> ent)
    {
        if (ent.Comp.Bursting || TerminatingOrDeleted(ent))
            return;

        ent.Comp.Bursting = true;

        var coords = Transform(ent).Coordinates;
        var creator = ent.Comp.Creator;
        var resonator = ent.Comp.Resonator;
        var failure = ent.Comp.FailureProb;
        var addingFailure = ent.Comp.AddingFailure;
        var sound = ent.Comp.BurstSound;

        var damage = ent.Comp.Damage * ent.Comp.DamageMultiplier;
        var pressure = _atmos.GetTileMixture((ent.Owner, Transform(ent)))?.Pressure ?? 0f;
        if (pressure <= ent.Comp.PressureUpperBound * Atmospherics.OneAtmosphere)
            damage *= ent.Comp.PressureMultiplier;

        QueueDel(ent);
        Spawn(BurstEffect, coords);
        _audio.PlayPvs(sound, coords);

        // Crush the rock on the field tile.
        var onTile = _lookup.GetEntitiesInRange(coords, 0.5f);
        foreach (var uid in onTile)
        {
            if (uid != ent.Owner && HasComp<GatherableComponent>(uid))
                _gatherable.Gather(uid, creator);
        }

        // Matrix traps are the only fields that hurt mobs: regular auto/manual fields
        // are purely a mining tool, otherwise fauna near rock gets shredded in seconds.
        if (ent.Comp.Matrix)
        {
            var mobs = _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.5f);
            foreach (var (mob, _) in mobs)
            {
                if (mob == ent.Owner || TerminatingOrDeleted(mob) || !_mobState.IsAlive(mob))
                    continue;

                _damage.TryChangeDamage(mob, damage, origin: creator);
            }
        }

        // Chain into other fields.
        var neighbours = _lookup.GetEntitiesInRange<ResonanceFieldComponent>(coords, 1.5f);
        foreach (var (otherUid, other) in neighbours)
        {
            if (otherUid == ent.Owner)
                continue;

            Burst((otherUid, other));
        }

        // With a partial chance, spread the field onto adjacent rock.
        if (resonator is not { } resonatorUid
            || !TryComp<ResonatorComponent>(resonatorUid, out var resonatorComp)
            || !_random.Prob(1f - failure / 100f))
            return;

        var adjacent = _lookup.GetEntitiesInRange(coords, 1.5f);
        foreach (var rock in adjacent)
        {
            if (!HasComp<GatherableComponent>(rock) || TerminatingOrDeleted(rock))
                continue;

            var rockCoords = Transform(rock).Coordinates;
            if (_lookup.GetEntitiesInRange<ResonanceFieldComponent>(rockCoords, 0.3f).Count > 0)
                continue;

            CreateField(rockCoords, creator, resonatorUid, resonatorComp.Mode, 1f,
                Math.Min(100, (int) failure + addingFailure));
        }
    }
}
