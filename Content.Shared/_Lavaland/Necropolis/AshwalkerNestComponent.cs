// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
/// Ashwalker nest: consumes corpses, hatches ashwalker ghost-role eggs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AshwalkerNestComponent : Component
{
    /// <summary>
    /// Radius in which corpses are consumed, in meters. TG scans the 3x3 tiles around the nest.
    /// </summary>
    [DataField]
    public float ConsumeRadius = 1.5f;

    /// <summary>
    /// Meat the nest starts with.
    /// shortcut: the round start clutch of three eggs is spawned by AshwalkerDenSystem, so this stays 0; at 6 the nest would lay three more eggs on the first tick.
    /// </summary>
    [DataField]
    public int InitialMeat;

    /// <summary>
    /// Meat per ordinary corpse eaten.
    /// </summary>
    [DataField]
    public int MeatPerCorpse = 1;

    /// <summary>
    /// Meat per megafauna corpse eaten.
    /// </summary>
    [DataField]
    public int MeatPerMegafauna = 20;

    /// <summary>
    /// Meat needed to hatch one egg.
    /// </summary>
    [DataField]
    public int MeatPerEgg = 2;

    /// <summary>
    /// How many eggs can exist at the same time. 0 means unlimited, as in TG.
    /// </summary>
    [DataField]
    public int MaxEggs;

    /// <summary>
    /// Fraction of the nest max integrity restored per corpse eaten.
    /// </summary>
    [DataField]
    public float HealPerCorpse = 0.05f;

    /// <summary>
    /// Seconds a dead ash walker with a live mind is kept for the soul salvage before being reborn at the nest.
    /// </summary>
    [DataField]
    public float SalvageDelay = 20f;

    [ViewVariables]
    public int Meat;

    [ViewVariables]
    public int Eggs;
}
