// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Megafauna.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Wendigo boss state: the arena center it returns to and the stomp it does while moving.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoBossComponent : Component
{
    /// <summary>
    /// Where the boss spawned, the center of its arena. The shockwave attacks return here.
    /// </summary>
    public EntityCoordinates Origin;

    /// <summary>
    /// Damage and throw burst spawned every <see cref="StompDistance"/> tiles of movement.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StompBurst;

    [DataField]
    public float StompDistance = 1f;

    [DataField]
    public SoundSpecifier? ScreamSound = new SoundPathSpecifier("/Audio/Effects/demon_dies.ogg");

    [DataField]
    public float ScreamRange = 7f;

    [DataField]
    public TimeSpan ScreamDizzyTime = TimeSpan.FromSeconds(12);

    public bool HasLastStompPosition;

    public Vector2 LastStompPosition;
}

/// <summary>
/// Running shockwave pattern on a wendigo. The boss is rooted in place until the pattern ends.
/// </summary>
[RegisterComponent]
public sealed partial class WendigoShockwaveComponent : Component
{
    public EntProtoId Projectile = default!;

    public WendigoShockwavePattern Pattern;

    public int Count = 1;

    public int Repeats = 1;

    public float Interval = 0.5f;

    public float Speed = 10f;

    public float Step;

    public float Growth;

    public Angle BaseAngle;

    public int Direction = 1;

    public int Repeat;

    public TimeSpan Next;

    public TimeSpan End;
}
