// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Inventory;

public sealed partial class SlotDefinition
{
    [DataField] public bool StrictWhitelist { get; private set; }
}
