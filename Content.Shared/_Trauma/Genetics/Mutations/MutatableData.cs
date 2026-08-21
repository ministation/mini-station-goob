// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Shared._Trauma.Genetics.Mutations;

/// <summary>
/// Complete mutation data for a mob which can easily be applied to a mob.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct MutatableData(List<EntProtoId> Dormant, List<EntProtoId> Mutations);
