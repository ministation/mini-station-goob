// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Port of tg's venomous_strikes mutation: the mutated mob's unarmed attacks inject a small dose of
/// <see cref="Venom"/> into whatever they hit, and the mob slowly filters the same reagent out of its own bloodstream.
/// </summary>
/// <remarks>
/// tg implements this with /datum/element/venomous, which hooks the mob's on-hit effect.
/// In ss14 unarmed attacks are melee attacks where the mob itself is the melee weapon,
/// so subscribing to <see cref="Content.Shared.Weapons.Melee.Events.MeleeHitEvent"/> on this component's owner
/// is the equivalent of tg's on-hit hook.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class VenomousStrikesComponent : Component
{
    /// <summary>
    /// Reagent injected into every entity hit by an unarmed attack.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Venom = "Toxin";

    /// <summary>
    /// How much <see cref="Venom"/> is injected per unarmed hit.
    /// tg's venom_amount (3) multiplied by mutation power, which the port does not have.
    /// </summary>
    [DataField]
    public FixedPoint2 AmountPerHit = FixedPoint2.New(3);

    /// <summary>
    /// How much of <see cref="Venom"/> is filtered out of the mob's own bloodstream per second.
    /// tg removes round(venom_amount * 0.33, CHEMICAL_VOLUME_ROUNDING) per second, i.e. 0.99.
    /// </summary>
    [DataField]
    public FixedPoint2 SelfFilterRate = FixedPoint2.New(0.99);
}
