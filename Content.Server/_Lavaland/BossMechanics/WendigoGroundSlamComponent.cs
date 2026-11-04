// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Expanding ring of damage and throws around a wendigo ground slam.
/// The entity removes itself when the last ring is done.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoGroundSlamComponent : Component
{
    /// <summary>
    /// Radius of the slam, in tiles.
    /// </summary>
    [DataField]
    public int Range = 5;

    /// <summary>
    /// Seconds between two rings.
    /// </summary>
    [DataField]
    public float Delay = 0.3f;

    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    /// <summary>
    /// Speed the hit mobs are thrown away with. Zero disables the throw.
    /// </summary>
    [DataField]
    public float ThrowSpeed = 8f;

    /// <summary>
    /// Random spread of the throw direction, in degrees.
    /// </summary>
    [DataField]
    public float ThrowSpread = 20f;

    /// <summary>
    /// Roots the boss in place for the duration of the slam.
    /// </summary>
    [DataField]
    public bool AnchorUser;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/_EinsteinEngines/Effects/bamf.ogg");

    public EntityUid? User;

    public TimeSpan NextRing;

    public int Ring;

    public HashSet<EntityUid> Hit = new();
}
