// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Server._Mini.VoiceChat;

/// <summary>
/// Mini: players who enabled voice chat client-side. They transmit microphone audio and are
/// excluded from TTS generation (voice chat or TTS — one or the other).
/// Stored statically because Content.Server cannot reference Content.Goobstation.Server,
/// where the voice chat manager lives; the manager updates this set on opt-in and disconnect.
/// </summary>
public static class VoiceChatOptIns
{
    private static readonly HashSet<NetUserId> OptedIn = new();

    public static bool Contains(NetUserId userId) => OptedIn.Contains(userId);

    public static void Set(NetUserId userId, bool optedIn)
    {
        if (optedIn)
            OptedIn.Add(userId);
        else
            OptedIn.Remove(userId);
    }
}
