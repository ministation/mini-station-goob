// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Mini.ERT;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.ERT;

public sealed class ERTCryostorageBoundUserInterface : BoundUserInterface
{
    private ERTCryostorageWindow? _window;

    public ERTCryostorageBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ERTCryostorageWindow>();
        _window.OnSpawnPressed += OnSpawnPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ERTCryostorageState cast)
        {
            _window?.UpdateOptions(
                cast.Options,
                cast.TitleName,
                cast.HasAdminAccess,
                cast.Blocked);
        }
    }

    private void OnSpawnPressed(int optionIndex)
    {
        SendMessage(new ERTCryostorageSpawnMessage(optionIndex));
    }
}