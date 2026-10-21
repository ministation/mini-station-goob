// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Network;

namespace Content.Server._Mini.VoiceChat;

/// <summary>
/// Mini: players whose client participates in voice chat. Voice chat is always on for new
/// clients, so this set normally contains every connected player. The server uses it to accept
/// microphone audio from a player. TTS is not affected: text speech is voiced independently.
/// Stored statically because Content.Server cannot reference Content.Goobstation.Server,
/// where the voice chat manager lives; the manager updates this set on connect and disconnect.
/// Several server instances (integration tests run them in parallel) share the static set,
/// so all access is locked.
/// </summary>
public static class VoiceChatOptIns
{
    private static readonly HashSet<NetUserId> OptedIn = new();
    private static readonly object Sync = new();

    public static bool Contains(NetUserId userId)
    {
        lock (Sync)
        {
            return OptedIn.Contains(userId);
        }
    }

    public static void Set(NetUserId userId, bool optedIn)
    {
        lock (Sync)
        {
            if (optedIn)
                OptedIn.Add(userId);
            else
                OptedIn.Remove(userId);
        }
    }
}
