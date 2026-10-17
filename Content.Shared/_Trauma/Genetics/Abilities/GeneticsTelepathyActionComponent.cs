// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Action component for use with <see cref="TelepathyActionEvent"/>.
/// PDA messaging but with your mind...
/// </summary>
/// <remarks>
/// Registered as <c>GeneticsTelepathyAction</c> instead of <c>TelepathyAction</c> to avoid colliding with
/// <see cref="Content.Goobstation.Shared.Telepathy.TelepathyActionComponent"/>, which upstream also registers
/// under that name.
/// </remarks>
[RegisterComponent, NetworkedComponent, Access(typeof(TelepathyActionSystem))]
public sealed partial class GeneticsTelepathyActionComponent : Component
{
    [DataField]
    public int MaxLength = 30; // no essays

    [ViewVariables]
    public EntityUid? Target;
}

public sealed partial class TelepathyActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public enum TelepathyUiKey : byte
{
    Key
}

/// <summary>
/// Message sent by the BUI with the chosen text to send to the target.
/// </summary>
[Serializable, NetSerializable]
public sealed class TelepathyChosenMessage(string message) : BoundUserInterfaceMessage
{
    public readonly string Message = message;
}
