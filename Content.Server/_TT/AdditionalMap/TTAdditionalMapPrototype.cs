/*
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 */

using Content.Server.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server._TT.AdditionalMap;

[Prototype("additionalMap")]
public sealed class AdditionalMapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    [DataField("maps")]
    public List<ProtoId<GameMapPrototype>> MapProtoIds = new();
}
