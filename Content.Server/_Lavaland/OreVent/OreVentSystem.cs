// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.GPS.Components;
using Content.Shared._Lavaland.OreVent;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mining;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
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
    private const int MineralsPerVent = 4;
    private const int BoulderMinerals = 3;
    private const string DroneExplosionEffect = "ExplosionEffectGrenade";
    private const string DroneSmokeEffect = "ExplosionEffectGrenadeSmoke";

    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OreVentComponent, MapInitEvent>(OnVentMapInit);
        SubscribeLocalEvent<OreVentComponent, InteractUsingEvent>(OnVentInteractUsing);
        SubscribeLocalEvent<OreVentComponent, ExaminedEvent>(OnVentExamined);
        SubscribeLocalEvent<OreVentComponent, OreVentScanDoAfterEvent>(OnScanDoAfter);
        SubscribeLocalEvent<OreVentComponent, OreVentTapDoAfterEvent>(OnTapDoAfter);
        SubscribeLocalEvent<OreBoulderComponent, InteractUsingEvent>(OnBoulderInteractUsing);
        SubscribeLocalEvent<OreBoulderComponent, AttackedEvent>(OnBoulderAttacked);
        SubscribeLocalEvent<OreVentSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
    }

    private void OnVentMapInit(Entity<OreVentComponent> ent, ref MapInitEvent args)
    {
        var comp = ent.Comp;

        if (comp.BouldersRemaining < 0)
            comp.BouldersRemaining = comp.BoulderBudget;

        if (comp.Minerals.Count == 0 && comp.MineralPool.Count > 0)
            RollMinerals(comp);

        if (comp.Discovered)
            AddGps(ent);
    }

    /// <summary>
    /// Rolls the four distinct minerals this vent can produce, same idea as TG's generate_mineral_breakdown.
    /// </summary>
    private void RollMinerals(OreVentComponent comp)
    {
        var pool = new List<ProtoId<OrePrototype>>(comp.MineralPool);
        for (var i = 0; i < MineralsPerVent && pool.Count > 0; i++)
            comp.Minerals.Add(_random.PickAndTake(pool));
    }

    /// <summary>
    /// Discovery hands the vent a GPS signal, like TG's gps component on scan.
    /// </summary>
    private void AddGps(EntityUid uid)
    {
        var gps = EnsureComp<GPSComponent>(uid);
        gps.GpsName = Loc.GetString("ore-vent-gps-signal");
        gps.Enabled = true;
        Dirty(uid, gps);
    }

    /// <summary>
    /// Marks the vent as discovered. The first scan runs a longer do-after before this.
    /// </summary>
    public bool TryDiscover(Entity<OreVentComponent> ent)
    {
        if (ent.Comp.Discovered)
            return false;

        ent.Comp.Discovered = true;
        AddGps(ent);
        _popup.PopupCoordinates(Loc.GetString("ore-vent-discovered"), Transform(ent).Coordinates, PopupType.Medium);
        return true;
    }

    private void OnVentExamined(Entity<OreVentComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (!comp.Discovered)
        {
            args.PushMarkup(Loc.GetString("ore-vent-examine-undiscovered"));
            return;
        }

        var names = new List<string>(comp.Minerals.Count);
        foreach (var mineral in comp.Minerals)
            names.Add(Loc.GetString($"ore-vent-mineral-{mineral.Id}"));

        args.PushMarkup(Loc.GetString("ore-vent-examine-discovered",
            ("size", Loc.GetString(SizeLocKey(comp.Size))),
            ("minerals", string.Join(", ", names))));
    }

    private void OnScanDoAfter(Entity<OreVentComponent> ent, ref OreVentScanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryDiscover(ent))
            _popup.PopupEntity(Loc.GetString("ore-vent-cannot-tap"), ent, args.User);
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

        if (!ent.Comp.Discovered)
        {
            var scan = new DoAfterArgs(EntityManager, args.User, ent.Comp.DiscoveryDuration, new OreVentScanDoAfterEvent(), ent)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = true,
            };

            if (_doAfter.TryStartDoAfter(scan))
                _popup.PopupEntity(Loc.GetString("ore-vent-scanning"), ent, args.User);

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
        comp.WavesRemaining = Math.Max(1, comp.WaveCount > 0 ? comp.WaveCount : WavesForSize(comp.Size));
        comp.SpawnedMobs.Clear();

        var coords = Transform(ent).Coordinates;
        var drone = Spawn(comp.NodeDroneProto, coords.Offset(new Vector2(0, 1)));
        comp.NodeDrone = drone;

        // Bigger vents get a sturdier drone: 300/400/500 HP for small/medium/large.
        var health = comp.NodeDroneBaseHealth + comp.NodeDroneHealthPerSize * (int) comp.Size;
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

        // TG drones go out with a bang and a cloud of smoke.
        if (comp.NodeDrone is { } drone && !TerminatingOrDeleted(drone))
        {
            var droneCoords = Transform(drone).Coordinates;
            Spawn(DroneExplosionEffect, droneCoords);
            Spawn(DroneSmokeEffect, droneCoords);
        }

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

        var boulder = Spawn(boulderProto, coords.Offset(_random.NextVector2(1.5f, 2.5f)));
        comp.BouldersRemaining--;
        comp.NextBoulderAt = _timing.CurTime + comp.BoulderInterval;

        // Artifact boulders keep their own artifact table. Normal boulders drop the vent's minerals
        // and take 2 to boulder_size hits depending on the vent size.
        if (!artifact && TryComp<OreBoulderComponent>(boulder, out var boulderComp))
        {
            boulderComp.HitsRemaining = _random.Next(2, BoulderHitsForSize(comp.Size) + 1);
            boulderComp.OreLoot = RollBoulderOre(comp);
        }

        if (comp.BouldersRemaining > 0)
            return;

        if (comp.DepletionRewardTable is { } reward)
        {
            foreach (var lootProto in _entityTable.GetSpawns(_proto.Index(reward)))
                Spawn(lootProto, coords.Offset(_random.NextVector2(1.5f, 2.5f)));
        }

        _popup.PopupCoordinates(Loc.GetString("ore-vent-depleted"), coords, PopupType.Medium);
    }

    /// <summary>
    /// Picks up to three of the vent's minerals and turns them into ore entities for a boulder.
    /// </summary>
    private List<EntProtoId> RollBoulderOre(OreVentComponent comp)
    {
        var loot = new List<EntProtoId>(BoulderMinerals);
        var pool = new List<ProtoId<OrePrototype>>(comp.Minerals);

        for (var i = 0; i < BoulderMinerals && pool.Count > 0; i++)
        {
            if (_proto.Index(_random.PickAndTake(pool)).OreEntity is { } ore)
                loot.Add(ore);
        }

        return loot;
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
        // The deletion from the breaking hit is deferred to the end of the tick; without this
        // guard a second hit in the same tick would run the loot roll again.
        if (ent.Comp.HitsRemaining <= 0)
            return;

        ent.Comp.HitsRemaining--;
        if (ent.Comp.HitsRemaining > 0)
        {
            _popup.PopupEntity(Loc.GetString("ore-vent-boulder-hit"), ent, user);
            return;
        }

        var coords = Transform(ent).Coordinates;

        if (ent.Comp.OreLoot.Count > 0)
        {
            // Vent boulders drop their vent's minerals, 2 to 5 ore per mineral. TG uses a
            // log-scaled count, a flat roll is close enough and keeps the entity count low.
            foreach (var oreProto in ent.Comp.OreLoot)
            {
                var ore = Spawn(oreProto, coords.Offset(_random.NextVector2(0.3f)));
                if (TryComp<StackComponent>(ore, out var stack))
                    _stack.SetCount(ore, _random.Next(2, 6), stack);
            }
        }
        else
        {
            foreach (var lootProto in _entityTable.GetSpawns(_proto.Index(ent.Comp.LootTable)))
                Spawn(lootProto, coords.Offset(_random.NextVector2(0.3f)));
        }

        _popup.PopupEntity(Loc.GetString("ore-vent-boulder-broken"), ent, user);
        QueueDel(ent);
    }

    // TG sizes: small vents get 2 waves and 300 HP drones, medium 3 and 400, large 4 and 500.

    private static int WavesForSize(OreVentSize size) => size switch
    {
        OreVentSize.Small => 2,
        OreVentSize.Medium => 3,
        _ => 4,
    };

    /// <summary>
    /// Largest boulder durability a vent of this size produces. Boulders roll 2 to this value.
    /// </summary>
    private static int BoulderHitsForSize(OreVentSize size) => size switch
    {
        OreVentSize.Small => 3,
        OreVentSize.Medium => 4,
        _ => 5,
    };

    private static string SizeLocKey(OreVentSize size) => size switch
    {
        OreVentSize.Small => "ore-vent-size-small",
        OreVentSize.Medium => "ore-vent-size-medium",
        _ => "ore-vent-size-large",
    };

    /// <summary>
    /// Rolls a vent size with TG's weights: small 7, medium 5, large 3.
    /// </summary>
    private OreVentSize RollVentSize()
    {
        var roll = _random.Next(7 + 5 + 3);
        if (roll < 7)
            return OreVentSize.Small;

        return roll < 12 ? OreVentSize.Medium : OreVentSize.Large;
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
        ventComp.Size = RollVentSize();
    }
}

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
