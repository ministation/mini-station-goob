// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.DecaCode;

/// <summary>
/// A crate protected by a four digit "Deca-Code" lock, in the style of the TG code lock.
/// The secret code lives only on the server and is never part of a networked field.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DecaCodeLockComponent : Component
{
    /// <summary>
    /// Amount of digits in the code. Codes consist of unique digits.
    /// </summary>
    public const int CodeLength = 4;

    /// <summary>
    /// The correct code. Server only: deliberately not an <see cref="AutoNetworkedFieldAttribute"/>.
    /// </summary>
    [DataField]
    public string SecretCode = string.Empty;

    /// <summary>
    /// How many failed attempts are allowed before the anti-tamper bomb goes off.
    /// </summary>
    [DataField]
    public int MaxAttempts = 10;

    /// <summary>
    /// Amount of wrong codes submitted so far.
    /// </summary>
    [DataField]
    public int FailedAttempts;

    /// <summary>
    /// Whether the crate is still locked. Set to false once the correct code is entered.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsLocked = true;

    /// <summary>
    /// Whether the anti-tamper bomb is live. Disarm it if the crate should only refuse input.
    /// </summary>
    [DataField]
    public bool BombArmed = true;

    /// <summary>
    /// History of submitted codes. It is not an <see cref="AutoNetworkedFieldAttribute"/>:
    /// only the sanitized <see cref="DecaCodeLockState"/> is ever sent to a client.
    /// </summary>
    public List<DecaCodeAttemptRecord> Attempts = new();
}

/// <summary>
/// A single submitted attempt. Shared with the client so the UI can render the history table.
/// </summary>
[Serializable, NetSerializable]
public sealed class DecaCodeAttemptRecord
{
    public string Input;
    public int ExactMatches;
    public int PartialMatches;

    public DecaCodeAttemptRecord(string input, int exactMatches, int partialMatches)
    {
        Input = input;
        ExactMatches = exactMatches;
        PartialMatches = partialMatches;
    }
}

/// <summary>
/// State of the lock UI. Contains no secret data, only the attempt history and remaining attempts.
/// </summary>
[Serializable, NetSerializable]
public sealed class DecaCodeLockState : BoundUserInterfaceState
{
    public List<DecaCodeAttemptRecord> Attempts;
    public int RemainingAttempts;

    public DecaCodeLockState(List<DecaCodeAttemptRecord> attempts, int remainingAttempts)
    {
        Attempts = attempts;
        RemainingAttempts = remainingAttempts;
    }
}

/// <summary>
/// Sent by the client when the player submits a code.
/// </summary>
[Serializable, NetSerializable]
public sealed class DecaCodeSubmitMessage : BoundUserInterfaceMessage
{
    public string Code;

    public DecaCodeSubmitMessage(string code)
    {
        Code = code;
    }
}

[Serializable, NetSerializable]
public enum DecaCodeLockUiKey : byte
{
    Key,
}
