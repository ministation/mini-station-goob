// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.OreVent;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.OreVent;

/// <summary>
/// TG ore vents adapted to the procedural lavaland planet: scan the vent, defend the
/// NODE drone from waves of lavaland fauna, then break the boulders it produces.
/// Boulders are dug with a pickaxe or a crusher.
/// </summary>
public sealed class OreVentSystem : EntitySystem
{
    private const string StoneTileBurnt = "LavalandStoneTileBurnt";
    private const string PickaxeTag = "Pickaxe";
    private const string ScannerTag = "MineralScanner";

    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OreVentComponent, MapInitEvent>(OnVentMapInit);
        SubscribeLocalEvent<OreVentComponent, InteractUsingEvent>(OnVentInteractUsing);
        SubscribeLocalEvent<OreVentComponent, OreVentTapDoAfterEvent>(OnTapDoAfter);
        SubscribeLocalEvent<OreBoulderComponent, InteractUsingEvent>(OnBoulderInteractUsing);
        SubscribeLocalEvent<OreBoulderComponent, AttackedEvent>(OnBoulderAttacked);
        SubscribeLocalEvent<OreVentSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnVentMapInit(Entity<OreVentComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.BouldersRemaining < 0)
            ent.Comp.BouldersRemaining = ent.Comp.BoulderBudget;
    }

    private void OnVentInteractUsing(Entity<OreVentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tags.HasTag(args.Used, ScannerTag))
            return;

        args.Handled = true;

        if (ent.Comp.Tapped)
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-already-tapped"), ent, args.User);
            return;
        }

        if (ent.Comp.Defending)
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-defending"), ent, args.User);
            return;
        }

        if (_timing.CurTime < ent.Comp.CooldownEnd)
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-recovering"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.TapDuration, new OreVentTapDoAfterEvent(), ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("ore-vent-scanning"), ent, args.User);
    }

    private void OnTapDoAfter(Entity<OreVentComponent> ent, ref OreVentTapDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryStartDefense(ent))
            _popup.PopupEntity(Loc.GetString("ore-vent-cannot-tap"), ent, args.User);
    }

    /// <summary>
    /// Starts the defense event: spawns the NODE drone and the first wave.
    /// </summary>
    public bool TryStartDefense(Entity<OreVentComponent> ent)
    {
        var comp = ent.Comp;
        var now = _timing.CurTime;

        if (comp.Tapped || comp.Defending || now < comp.CooldownEnd)
            return false;

        comp.Defending = true;
        comp.WavesRemaining = Math.Max(1, comp.WaveCount);
        comp.SpawnedMobs.Clear();

        var coords = Transform(ent).Coordinates;
        var drone = Spawn(comp.NodeDroneProto, coords.Offset(new Vector2(0, 1)));
        comp.NodeDrone = drone;

        // Bigger vents get a sturdier drone, same idea as TG scaling max health by boulder size.
        var health = comp.NodeDroneBaseHealth + comp.NodeDroneHealthPerBoulder * Math.Max(1, comp.BoulderBudget);
        _mobThreshold.SetMobStateThreshold(drone, FixedPoint2.New(health), MobState.Dead);

        // A short windup so players can see the drone before the first wave.
        comp.NextWaveAt = now + TimeSpan.FromSeconds(1);

        _popup.PopupCoordinates(Loc.GetString("ore-vent-defense-started"), coords, PopupType.MediumCaution);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OreVentComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = new Entity<OreVentComponent>(uid, comp);

            if (comp.Defending)
            {
                UpdateDefense(ent, now);
                continue;
            }

            if (!comp.Tapped || comp.BouldersRemaining <= 0 || now < comp.NextBoulderAt)
                continue;

            ProduceBoulder(ent);
        }
    }

    private void UpdateDefense(Entity<OreVentComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;

        if (comp.NodeDrone is not { } drone || TerminatingOrDeleted(drone) || _mobState.IsDead(drone))
        {
            FailDefense(ent);
            return;
        }

        if (comp.WavesRemaining > 0)
        {
            if (now < comp.NextWaveAt)
                return;

            SpawnWave(ent);
            comp.WavesRemaining--;
            comp.NextWaveAt = now + comp.WaveInterval;
            return;
        }

        if (HasLiveWaveMobs(comp))
            return;

        WinDefense(ent);
    }

    private void SpawnWave(Entity<OreVentComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.WaveMobTable is not { } table)
            return;

        var coords = Transform(ent).Coordinates;
        var tableProto = _proto.Index(table);

        for (var i = 0; i < comp.MobsPerWave; i++)
        {
            foreach (var mobProto in _entityTable.GetSpawns(tableProto))
            {
                var mob = Spawn(mobProto, coords.Offset(_random.NextVector2(2f, 4f)));
                comp.SpawnedMobs.Add(mob);
            }
        }

        _popup.PopupCoordinates(Loc.GetString("ore-vent-wave"), coords, PopupType.MediumCaution);
    }

    private bool HasLiveWaveMobs(OreVentComponent comp)
    {
        comp.SpawnedMobs.RemoveWhere(mob => TerminatingOrDeleted(mob));

        foreach (var mob in comp.SpawnedMobs)
        {
            if (_mobState.IsAlive(mob))
                return true;
        }

        return false;
    }

    private void WinDefense(Entity<OreVentComponent> ent)
    {
        var comp = ent.Comp;
        comp.Defending = false;
        comp.Tapped = true;
        comp.SpawnedMobs.Clear();
        comp.NextBoulderAt = _timing.CurTime + comp.BoulderInterval;
        DeleteDrone(comp);

        _popup.PopupCoordinates(Loc.GetString("ore-vent-tapped"), Transform(ent).Coordinates, PopupType.Medium);
    }

    private void FailDefense(Entity<OreVentComponent> ent)
    {
        var comp = ent.Comp;
        comp.Defending = false;
        comp.WavesRemaining = 0;
        comp.CooldownEnd = _timing.CurTime + comp.RetryCooldown;
        comp.SpawnedMobs.Clear();
        DeleteDrone(comp);

        _popup.PopupCoordinates(Loc.GetString("ore-vent-failed"), Transform(ent).Coordinates, PopupType.MediumCaution);
    }

    private void DeleteDrone(OreVentComponent comp)
    {
        if (comp.NodeDrone is not { } drone)
            return;

        if (!TerminatingOrDeleted(drone))
            QueueDel(drone);

        comp.NodeDrone = null;
    }

    private void ProduceBoulder(Entity<OreVentComponent> ent)
    {
        var comp = ent.Comp;
        var coords = Transform(ent).Coordinates;
        var artifact = comp.ArtifactBoulderChance > 0 && _random.Prob(comp.ArtifactBoulderChance);

        var boulderProto = artifact || comp.BoulderProtos.Count == 0
            ? comp.ArtifactBoulderProto
            : _random.Pick(comp.BoulderProtos);

        Spawn(boulderProto, coords.Offset(_random.NextVector2(1.5f, 2.5f)));
        comp.BouldersRemaining--;
        comp.NextBoulderAt = _timing.CurTime + comp.BoulderInterval;

        if (comp.BouldersRemaining > 0)
            return;

        if (comp.DepletionRewardTable is { } reward)
        {
            foreach (var lootProto in _entityTable.GetSpawns(_proto.Index(reward)))
                Spawn(lootProto, coords.Offset(_random.NextVector2(1.5f, 2.5f)));
        }

        _popup.PopupCoordinates(Loc.GetString("ore-vent-depleted"), coords, PopupType.Medium);
    }

    private void OnBoulderInteractUsing(Entity<OreBoulderComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tags.HasTag(args.Used, PickaxeTag))
            return;

        args.Handled = true;
        DigBoulder(ent, args.User);
    }

    private void OnBoulderAttacked(Entity<OreBoulderComponent> ent, ref AttackedEvent args)
    {
        if (!_tags.HasTag(args.Used, PickaxeTag))
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-boulder-tool"), ent, args.User);
            return;
        }

        DigBoulder(ent, args.User);
    }

    private void DigBoulder(Entity<OreBoulderComponent> ent, EntityUid user)
    {
        ent.Comp.HitsRemaining--;
        if (ent.Comp.HitsRemaining > 0)
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-boulder-hit"), ent, user);
            return;
        }

        var coords = Transform(ent).Coordinates;
        foreach (var lootProto in _entityTable.GetSpawns(_proto.Index(ent.Comp.LootTable)))
            Spawn(lootProto, coords.Offset(_random.NextVector2(0.3f)));

        _popup.PopupEntity(Loc.GetString("ore-vent-boulder-broken"), ent, user);
        QueueDel(ent);
    }

    private void OnSpawnerMapInit(Entity<OreVentSpawnerComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;
        var baseCoords = Transform(ent).Coordinates;

        // The landmark can land on lava or inside basalt: lay a small stone patch first,
        // same as the other lavaland marker ruins.
        for (var x = -comp.Radius; x <= comp.Radius; x++)
        {
            for (var y = -comp.Radius; y <= comp.Radius; y++)
                Spawn(StoneTileBurnt, baseCoords.Offset(new Vector2(x, y)));
        }

        var vent = Spawn(comp.VentProto, baseCoords);
        if (!TryComp<OreVentComponent>(vent, out var ventComp))
            return;

        var budget = _random.Next(comp.MinBoulders, comp.MaxBoulders + 1);
        ventComp.BoulderBudget = budget;
        ventComp.BouldersRemaining = budget;
    }
}

/// <summary>
/// Finishes the scan after a do-after so the vent cannot be tapped by a single click.
/// </summary>
public sealed partial class OreVentTapDoAfterEvent : SimpleDoAfterEvent;

[RegisterComponent]
public sealed partial class OreVentSpawnerComponent : Component
{
    /// <summary>
    /// Vent prototype placed at the marker.
    /// </summary>
    [DataField]
    public EntProtoId VentProto = "LavalandOreVent";

    /// <summary>
    /// Half-size of the stone patch laid around the vent, in tiles.
    /// </summary>
    [DataField]
    public int Radius = 3;

    [DataField]
    public int MinBoulders = 6;

    [DataField]
    public int MaxBoulders = 12;
}
