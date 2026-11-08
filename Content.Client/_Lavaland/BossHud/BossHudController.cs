// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Lavaland.BossHud;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Localization;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._Lavaland.BossHud;

/// <summary>
/// Shows the boss HUD for the closest active boss of the local map and keeps it
/// updated every frame.
/// </summary>
[UsedImplicitly]
public sealed class BossHudController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private Control? _screen;
    private BossHudControl? _cachedHud;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
    }

    public void OnStateEntered(GameplayState state)
    {
        _screen = null;
        _cachedHud = null;
    }

    private void OnScreenLoad()
    {
        _screen = null;
        _cachedHud = null;
        Update();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        Update();
    }

    private void Update()
    {
        var hud = TryFindHud();
        if (hud == null)
            return;

        if (_player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<TransformComponent>(player, out var playerXform))
        {
            hud.HideHud();
            return;
        }

        var playerMap = playerXform.MapID;
        var playerPos = playerXform.WorldPosition;

        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;

        var query = EntityManager.AllEntityQueryEnumerator<BossHudComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boss, out var xform))
        {
            if (!boss.Active || xform.MapID != playerMap)
                continue;

            var distance = (xform.WorldPosition - playerPos).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearest = uid;
            nearestDistance = distance;
        }

        if (nearest is not { } bossUid
            || !EntityManager.TryGetComponent<BossHudComponent>(bossUid, out var bossComp))
        {
            hud.HideHud();
            return;
        }

        var stage = 1;
        var maxStage = 1;
        if (EntityManager.TryGetComponent<MegafaunaStageComponent>(bossUid, out var stageComp))
        {
            stage = stageComp.Stage;
            maxStage = stageComp.MaxStage;
        }

        var name = EntityManager.GetComponent<MetaDataComponent>(bossUid).EntityName;
        hud.ShowHud(LocExtensions.LocalizeOrRaw(name), bossComp.Health, bossComp.MaxHealth,
            bossComp.Armor, bossComp.MaxArmor, bossComp.ArmorBroken, stage, maxStage);
    }

    private BossHudControl? TryFindHud()
    {
        if (UIManager.ActiveScreen == null)
        {
            _screen = null;
            _cachedHud = null;
            return null;
        }

        if (_screen == UIManager.ActiveScreen && _cachedHud != null)
            return _cachedHud;

        _screen = UIManager.ActiveScreen;
        _cachedHud = UIManager.ActiveScreen.FindNameScope()?.Find("BossHud") as BossHudControl;
        return _cachedHud;
    }
}
