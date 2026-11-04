// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MartialArts.Events;

// Mini - "Кулак Правосудия" (бокс ГСБ)

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HosBoxDoubleJabPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HosBoxLiverHookPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HosBoxCrossPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HosBoxBodyUppercutPerformedEvent : EntityEventArgs;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HosBoxKnockoutPerformedEvent : EntityEventArgs;
