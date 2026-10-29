// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Fishing.Sources;

[Serializable, NetSerializable]
public enum FishCatalogUiKey : byte
{
    Key,
}
