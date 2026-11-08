// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarRespawnBoundUserInterface : BoundUserInterface
{
    private TypanWarRespawnWindow? _window;

    public TypanWarRespawnBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window ??= new TypanWarRespawnWindow(this);
        _window.OnClose -= Close;
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        _window ??= new TypanWarRespawnWindow(this);

        if (state is TypanWarRespawnBoundUserInterfaceState cast)
            _window.UpdateState(cast);
    }

    public void SendRespawnRequest(bool isBase, NetEntity zone)
    {
        SendMessage(new TypanWarRespawnRequestMessage(isBase, zone));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}
