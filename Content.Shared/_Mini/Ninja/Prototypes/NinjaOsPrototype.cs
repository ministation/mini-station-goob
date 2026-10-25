// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Ninja.Prototypes;

/// <summary>
/// Describes the set of modules available to a ninja suit.
/// When <see cref="Modules"/> is empty every registered <see cref="NinjaModulePrototype"/> is used.
/// </summary>
[Prototype("ninjaOs")]
public sealed partial class NinjaOsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<ProtoId<NinjaModulePrototype>> Modules = new();

    /// <summary>
    /// Highest module tier. Tiers are 1..MaxTier, one module per tier.
    /// </summary>
    [DataField]
    public int MaxTier = 5;
}

[Serializable, NetSerializable]
public enum NinjaOsBootCheck : byte
{
    SelfTest = 0,
    Authorized = 1,
    NotActivated = 2,
    ModulesReady = 3,
    VisorSecure = 4,
    HelmetSecure = 5,
    MaskSecure = 6,
    GlovesSecure = 7,
    BootsSecure = 8,
    SuitFasten = 9,
    BatteryReport = 10,
}

[DataRecord]
public partial record struct NinjaOsBootStep()
{
    [DataField]
    public float Delay = 1f;

    [DataField]
    public LocId Log = string.Empty;

    [DataField]
    public NinjaOsBootCheck? Check;
}

/// <summary>
/// Boot script of the suit, used by the client to show the activation log.
/// </summary>
[Prototype("ninjaOsBoot")]
public sealed partial class NinjaOsBootPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<NinjaOsBootStep> Steps = new();
}
