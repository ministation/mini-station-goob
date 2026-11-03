// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Lavaland.Weapons.CainAbel;

/// <summary>Dagger swing ability: a short flurry of slashes around the user.</summary>
public sealed partial class CainAbelSwingActionEvent : InstantActionEvent
{
}

/// <summary>Release the stored blood wisps at a world position.</summary>
public sealed partial class CainAbelReleaseWispsActionEvent : WorldTargetActionEvent
{
}
