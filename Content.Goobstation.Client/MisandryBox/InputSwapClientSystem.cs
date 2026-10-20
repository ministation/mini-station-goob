// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MisandryBox;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameStates;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Goobstation.Client.MisandryBox;

/// <summary>
/// Re-applies movement speed modifiers after game state when input-swap is active on the local player.
/// Only runs while the smite is present, unlike the previous global resync on every state.
/// </summary>
public sealed partial class InputSwapClientSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _move = default!;
    [Dependency] private IClientGameStateManager _stateMan = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        _stateMan.GameStateApplied += OnGameStateApplied;
    }

    public override void Shutdown()
    {
        _stateMan.GameStateApplied -= OnGameStateApplied;
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (_player.LocalEntity is not { } local || !HasComp<InputSwapComponent>(local))
            return;

        if (!HasComp<MovementSpeedModifierComponent>(local))
            return;

        _move.RefreshMovementSpeedModifiers(local);
    }
}
