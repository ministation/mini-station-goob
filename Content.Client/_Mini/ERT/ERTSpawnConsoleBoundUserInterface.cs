// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.ERT;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.ERT;

public sealed class ERTSpawnConsoleBoundUserInterface : BoundUserInterface
{
    private ERTSpawnConsoleWindow? _window;

    public ERTSpawnConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ERTSpawnConsoleWindow>();
        _window.OnCallPressed += OnCallPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ERTSpawnConsoleState cast)
        {
            _window?.UpdateOptions(
                cast.Options,
                cast.IsOnCooldown,
                cast.ActiveOptionIndex,
                cast.Balance,
                cast.HasAdminAccess,
                cast.CanCall,
                cast.TitleName,
                cast.BalanceName);
        }
    }

    private void OnCallPressed(int optionIndex)
    {
        SendMessage(new ERTSpawnConsoleCallMessage(optionIndex));
    }
}