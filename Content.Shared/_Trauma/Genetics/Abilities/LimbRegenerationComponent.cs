// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Regrows missing limbs and organs of the mutated entity while it is unconscious and well fed.
/// The actual regrowth is handled by <c>LimbRegenerationSystem</c> on the server.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LimbRegenerationComponent : Component
{
    /// <summary>
    /// Chance to attempt regenerating something every <see cref="AttemptInterval"/>.
    /// /tg/ rolls this per second.
    /// </summary>
    [DataField]
    public float Chance = 0.05f;

    /// <summary>
    /// How often an attempt to regenerate something is made.
    /// </summary>
    [DataField]
    public TimeSpan AttemptInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Fraction of the entity's "okay" hunger threshold that is required to regenerate.
    /// Mirrors /tg/'s NUTRITION_LEVEL_FED * 0.85 check.
    /// </summary>
    [DataField]
    public float NutritionThresholdFraction = 0.85f;

    /// <summary>
    /// Fraction of the entity's "overfed" hunger value consumed by regrowing a limb.
    /// </summary>
    [DataField]
    public float LimbNutritionFraction = 0.5f;

    /// <summary>
    /// Fraction of the entity's "overfed" hunger value consumed by regrowing an important organ.
    /// </summary>
    [DataField]
    public float OrganNutritionFraction = 0.5f;

    /// <summary>
    /// Fraction of the entity's "overfed" hunger value consumed by regrowing a cosmetic organ.
    /// </summary>
    [DataField]
    public float CosmeticOrganNutritionFraction = 0.3f;

    /// <summary>
    /// Organ slots that are considered cosmetic. These are regenerated last and are cheaper.
    /// </summary>
    [DataField]
    public HashSet<string> CosmeticOrganSlots = new() { "eyes", "tongue", "butt" };

    /// <summary>
    /// Body prototype slots that are never regenerated.
    /// The head is excluded because its removal is used by the H.A.R.S. mutation.
    /// </summary>
    [DataField]
    public HashSet<string> IgnoredPartSlots = new() { "head" };

    /// <summary>
    /// Sound played when a limb or organ is regenerated.
    /// </summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg")
    {
        Params = AudioParams.Default.WithVariation(0.1f)
    };

    /// <summary>
    /// Runtime accumulator for <see cref="AttemptInterval"/>.
    /// </summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>
    /// Whether the entity has already been notified that it needs to eat or sleep first.
    /// </summary>
    [ViewVariables]
    public bool Notified;
}
