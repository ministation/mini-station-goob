// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Mining;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.OreVent;

/// <summary>
/// An ore vent: a lavaland rock formation that is scanned with a mining scanner.
/// The first scan discovers it and gives it a GPS signal, the next one taps it and
/// starts a defense event. If the NODE drone survives the waves, the vent produces
/// a limited number of boulders and finally a reward.
/// </summary>
[RegisterComponent]
public sealed partial class OreVentComponent : Component
{
    /// <summary>
    /// How many boulders this vent can produce in total.
    /// </summary>
    [DataField]
    public int BoulderBudget = 8;

    /// <summary>
    /// Boulders still left in the vent. Set from <see cref="BoulderBudget"/> on map init.
    /// </summary>
    public int BouldersRemaining = -1;

    /// <summary>
    /// Vent size. Drives wave count, NODE drone health and boulder durability.
    /// The marker spawner rolls it for planet vents, fixed prototypes set it in yml.
    /// </summary>
    [DataField]
    public OreVentSize Size = OreVentSize.Medium;

    /// <summary>
    /// Ores the four vent minerals are rolled from when <see cref="Minerals"/> is empty.
    /// </summary>
    [DataField]
    public List<ProtoId<OrePrototype>> MineralPool = new();

    /// <summary>
    /// Minerals this vent can produce. Empty means roll four distinct ones from <see cref="MineralPool"/> on map init.
    /// </summary>
    [DataField]
    public List<ProtoId<OrePrototype>> Minerals = new();

    /// <summary>
    /// Has the vent been scanned yet. Undiscovered vents only show a scan hint on examine.
    /// </summary>
    [DataField]
    public bool Discovered;

    /// <summary>
    /// Has the vent been tapped to produce boulders.
    /// </summary>
    [DataField]
    public bool Tapped;

    /// <summary>
    /// Table used to pick the mobs of a defense wave.
    /// </summary>
    [DataField]
    public ProtoId<EntityTablePrototype>? WaveMobTable;

    /// <summary>
    /// How many waves defend the vent before the drone is considered safe.
    /// Negative values derive the count from <see cref="Size"/>.
    /// </summary>
    [DataField]
    public int WaveCount = -1;

    /// <summary>
    /// How many mobs spawn per wave.
    /// </summary>
    [DataField]
    public int MobsPerWave = 2;

    /// <summary>
    /// Delay between waves.
    /// </summary>
    [DataField]
    public TimeSpan WaveInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long the first scan takes before the vent is discovered and gets a GPS signal.
    /// </summary>
    [DataField]
    public TimeSpan DiscoveryDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// How long the player scans the vent before the defense starts.
    /// </summary>
    [DataField]
    public TimeSpan TapDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public EntProtoId NodeDroneProto = "LavalandNodeDrone";

    /// <summary>
    /// Health of the NODE drone for a small vent. Medium and large vents add <see cref="NodeDroneHealthPerSize"/> per step.
    /// </summary>
    [DataField]
    public int NodeDroneBaseHealth = 300;

    /// <summary>
    /// Extra NODE drone health per size step above small, so the drone has 300, 400 or 500 HP.
    /// </summary>
    [DataField]
    public int NodeDroneHealthPerSize = 100;

    /// <summary>
    /// Delay between produced boulders once the vent is tapped.
    /// </summary>
    [DataField]
    public TimeSpan BoulderInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Boulder prototypes spawned while the vent is active. Picked at random for visual variety.
    /// </summary>
    [DataField]
    public List<EntProtoId> BoulderProtos = new() { "LavalandOreBoulder" };

    /// <summary>
    /// Chance that a produced boulder is an artifact boulder instead of a normal one.
    /// </summary>
    [DataField]
    public float ArtifactBoulderChance = 0.1f;

    [DataField]
    public EntProtoId ArtifactBoulderProto = "LavalandOreBoulderArtifact";

    /// <summary>
    /// Loot spawned once the vent has no boulders left.
    /// </summary>
    [DataField]
    public ProtoId<EntityTablePrototype>? DepletionRewardTable;

    /// <summary>
    /// Cooldown after a failed defense before the vent can be tapped again.
    /// </summary>
    [DataField]
    public TimeSpan RetryCooldown = TimeSpan.FromSeconds(30);

    // Round state, not serialized.

    public bool Defending;
    public int WavesRemaining;
    public TimeSpan NextWaveAt;
    public TimeSpan NextBoulderAt;
    public TimeSpan CooldownEnd;
    public EntityUid? NodeDrone;
    public readonly HashSet<EntityUid> SpawnedMobs = new();
}

/// <summary>
/// Finishes the discovery scan. The vent becomes discovered and gets a GPS signal.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class OreVentScanDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Finishes the tap scan after a do-after so the vent cannot be tapped by a single click.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class OreVentTapDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Vent size: small, medium or large. TG's 7/5/3 weights are rolled by the marker spawner.
/// </summary>
public enum OreVentSize : byte
{
    Small,
    Medium,
    Large,
}
