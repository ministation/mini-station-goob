/*
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 */

using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._TT.AdditionalMap;

[Prototype("additionalMap")]
public sealed partial class AdditionalMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField("maps")]
    public List<ProtoId<GameMapPrototype>> MapProtoIds = new();
}
