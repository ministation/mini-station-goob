// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Trauma.Genetics.Interaction;

/// <summary>
/// Allows interaction at any range with this entity by players with
/// <see cref="YamlCompat.TelekinesisComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TelekineticInteractableComponent : Component;
