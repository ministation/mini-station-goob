// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 4: the energy net emitter. It lives on the retractable emitter item and fires a net
/// that straps the target in place until the net is broken or expires.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaEnergyNetGunComponent : Component
{
    /// <summary>
    /// Entity prototype of the net spawned on the target.
    /// </summary>
    [DataField]
    public EntProtoId NetProto = "NinjaNet";

    /// <summary>
    /// How long the emitter recharges after a successful shot.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum application range, in tiles.
    /// </summary>
    [DataField]
    public float Range = 10f;

    /// <summary>
    /// Suit battery charge spent by a single application.
    /// </summary>
    [DataField]
    public float Charge = 100f;

    /// <summary>
    /// Sound played when the net is launched.
    /// </summary>
    [DataField]
    public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/Effects/PowerSink/electric.ogg");

    /// <summary>
    /// Sprite of the beam between the net and the ninja.
    /// </summary>
    [DataField]
    public SpriteSpecifier NetBeamSprite = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Mini/Ninja/Objects/Weapons/ninja.rsi"),
        "n_beam");

    /// <summary>
    /// When the emitter is ready to fire again.
    /// </summary>
    [DataField]
    public TimeSpan RechargeTime;
}

/// <summary>
/// The energy net itself. It is a static strap entity that cannot be removed by the caught mob.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaEnergyNetComponent : Component
{
    /// <summary>
    /// When the visual beam between the net and the ninja is removed.
    /// </summary>
    [DataField]
    public TimeSpan BeamDeleteTime;

    /// <summary>
    /// How long the visual beam lives after the net is launched.
    /// </summary>
    [DataField]
    public TimeSpan BeamDuration = TimeSpan.FromSeconds(2);
}
