// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

// Mini Station - alternating lobby backgrounds (Mars / space hole)
public sealed partial class GameTicker
{
    private static readonly string[] MiniLobbyBackgroundPaths =
    [
        // Mini: на время хэллоуинского события лобби одно - анимированное кладбище.
        // После события вернуть ротацию mars/space-hole.
        "/Textures/_Mini/Lobby/halloween.rsi",
        // "/Textures/_Mini/Lobby/mars.rsi",
        // "/Textures/_OS/Lobby/space-hole.rsi",
    ];

    private int _lobbyBackgroundIndex;

    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    private void InitializeLobbyBackground()
    {
        _lobbyBackgroundIndex = 0;
        LobbyBackground = MiniLobbyBackgroundPaths[_lobbyBackgroundIndex];
    }

    private void RandomizeLobbyBackground()
    {
        _lobbyBackgroundIndex = (_lobbyBackgroundIndex + 1) % MiniLobbyBackgroundPaths.Length;
        LobbyBackground = MiniLobbyBackgroundPaths[_lobbyBackgroundIndex];
    }
}
