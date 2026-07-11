// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Hides ghost role / antag / Thunderdome controls during station war.
/// </summary>
public sealed class TypanWarGhostUiSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly TypanWarUiSystem _war = default!;

    public override void Initialize()
    {
        base.Initialize();
        _war.StatusUpdated += UpdateGhostGui;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _war.StatusUpdated -= UpdateGhostGui;
    }

    private void UpdateGhostGui()
    {
        var gui = _ui.GetActiveUIWidgetOrNull<GhostGui>();
        if (gui == null)
            return;

        var warActive = _war.Phase is TypanWarPhase.Pending or TypanWarPhase.Active;
        gui.SetWarModeRestrictions(warActive);
    }
}
