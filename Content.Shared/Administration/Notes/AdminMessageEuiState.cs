// SPDX-License-Identifier: MIT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Notes;

[Serializable, NetSerializable]
public sealed class AdminMessageEuiState(TimeSpan time, AdminMessageEuiState.Message[] messages) : EuiStateBase
{
    public TimeSpan Time { get; private set; } = time;
    public Message[] Messages { get; private set; } = messages;

    [Serializable]
    public sealed class Message(string text, string adminName, DateTime addedOn)
    {
        public string Text = text;
        public string AdminName = adminName;
        public DateTime AddedOn = addedOn;
    }
}

public static class AdminMessageEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Dismiss(bool permanent) : EuiMessageBase
    {
        public bool Permanent { get; private set; } = permanent;
    }
}