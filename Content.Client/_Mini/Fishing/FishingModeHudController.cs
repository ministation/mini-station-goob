// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Mini.Fishing;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Mini.Fishing;

[UsedImplicitly]
public sealed class FishingModeHudController : UIController,
    IOnStateEntered<GameplayState>,
    IOnSystemChanged<FishingModeUiSystem>
{
    [UISystemDependency] private readonly FishingModeUiSystem _fishing = default!;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
    }

    public void OnStateEntered(GameplayState state)
    {
        _fishing.RequestStatus();
        Refresh();
    }

    public void OnSystemLoaded(FishingModeUiSystem system)
    {
        system.LeadersUpdated += Refresh;
        Refresh();
    }

    public void OnSystemUnloaded(FishingModeUiSystem system)
    {
        system.LeadersUpdated -= Refresh;
    }

    private void OnScreenLoad()
    {
        _fishing.RequestStatus();
        Refresh();
    }

    private void Refresh()
    {
        if (UIManager.ActiveScreen == null)
            return;

        var nameScope = UIManager.ActiveScreen.FindNameScope();
        if (nameScope?.Find("FishingModeHud") is not FishingModeHudControl hud)
            return;

        hud.Update(_fishing.Active, _fishing.Leaders);
    }
}
