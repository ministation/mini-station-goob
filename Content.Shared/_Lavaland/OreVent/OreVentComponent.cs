// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Lavaland.OreVent;

/// <summary>
/// An ore vent: a lavaland rock formation that is scanned with a mining scanner.
/// Scanning taps it and starts a defense event. If the NODE drone survives the
/// waves, the vent produces a limited number of boulders and finally a reward.
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
    /// Table used to pick the mobs of a defense wave.
    /// </summary>
    [DataField]
    public ProtoId<EntityTablePrototype>? WaveMobTable;

    /// <summary>
    /// How many waves defend the vent before the drone is considered safe.
    /// </summary>
    [DataField]
    public int WaveCount = 3;

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
    /// How long the player scans the vent before the defense starts.
    /// </summary>
    [DataField]
    public TimeSpan TapDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public EntProtoId NodeDroneProto = "LavalandNodeDrone";

    /// <summary>
    /// Base health of the NODE drone, before the per-boulder bonus.
    /// </summary>
    [DataField]
    public int NodeDroneBaseHealth = 300;

    /// <summary>
    /// Extra NODE drone health per boulder in the vent's budget, so bigger vents are harder.
    /// </summary>
    [DataField]
    public int NodeDroneHealthPerBoulder = 25;

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
    public bool Tapped;
    public int WavesRemaining;
    public TimeSpan NextWaveAt;
    public TimeSpan NextBoulderAt;
    public TimeSpan CooldownEnd;
    public EntityUid? NodeDrone;
    public readonly HashSet<EntityUid> SpawnedMobs = new();
}

/// <summary>
/// Finishes the scan after a do-after so the vent cannot be tapped by a single click.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class OreVentTapDoAfterEvent : SimpleDoAfterEvent;
