// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

// Oasis — atmospheric lobby backgrounds
public sealed partial class GameTicker
{
    private static readonly string[] MiniLobbyBackgroundPaths =
    [
        "/Textures/_OS/Lobby/evening_sun7.rsi",
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
