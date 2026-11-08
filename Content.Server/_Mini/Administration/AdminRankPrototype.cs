// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

[Prototype("adminRankIcon")]
public sealed partial class AdminRankIconPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;
    
    [DataField(required: true)] public string Rank { get; set; } = string.Empty;
    [DataField(required: true)] public string Icon { get; set; } = string.Empty;
}