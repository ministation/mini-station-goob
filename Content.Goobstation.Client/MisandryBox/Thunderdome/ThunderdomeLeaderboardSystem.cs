using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.MisandryBox.Thunderdome;

/// <summary>
/// System that manages the Thunderdome leaderboard overlay.
/// </summary>
public sealed partial class ThunderdomeLeaderboardSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private ThunderdomeLeaderboardOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ThunderdomeLeaderboardOverlay();

        // Подписываемся на события attach/detach локального игрока
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);

        // Подписываемся на добавление/удаление ThunderdomePlayerComponent
        SubscribeLocalEvent<ThunderdomePlayerComponent, ComponentStartup>(OnThunderdomePlayerStartup);
        SubscribeLocalEvent<ThunderdomePlayerComponent, ComponentShutdown>(OnThunderdomePlayerShutdown);
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        UpdateOverlayVisibility();
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        // Убираем overlay при отключении от энтити
        if (_overlayMan.HasOverlay<ThunderdomeLeaderboardOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnThunderdomePlayerStartup(EntityUid uid, ThunderdomePlayerComponent component, ComponentStartup args)
    {
        // Когда игрок получает ThunderdomePlayerComponent, показываем overlay
        if (_playerManager.LocalEntity == uid)
            UpdateOverlayVisibility();
    }

    private void OnThunderdomePlayerShutdown(EntityUid uid, ThunderdomePlayerComponent component, ComponentShutdown args)
    {
        // Когда игрок теряет ThunderdomePlayerComponent, обновляем overlay
        if (_playerManager.LocalEntity == uid)
            UpdateOverlayVisibility();
    }

    private void UpdateOverlayVisibility()
    {
        var localPlayer = _playerManager.LocalEntity;
        if (localPlayer == null)
            return;

        // Проверяем, в Thunderdome ли игрок (активный игрок) или призрак на карте Thunderdome
        var shouldShow = HasComp<ThunderdomePlayerComponent>(localPlayer.Value) ||
                         (HasComp<GhostComponent>(localPlayer.Value) && IsOnThunderdomeMap(localPlayer.Value));

        if (shouldShow)
        {
            if (!_overlayMan.HasOverlay<ThunderdomeLeaderboardOverlay>())
                _overlayMan.AddOverlay(_overlay);
        }
        else
        {
            if (_overlayMan.HasOverlay<ThunderdomeLeaderboardOverlay>())
                _overlayMan.RemoveOverlay(_overlay);
        }
    }

    private bool IsOnThunderdomeMap(EntityUid entity)
    {
        // Проверяем, есть ли на карте игрока хотя бы один ThunderdomeLeaderboardComponent
        if (!TryComp<TransformComponent>(entity, out var xform))
            return false;

        var playerMapId = xform.MapID;

        var query = EntityQueryEnumerator<ThunderdomeLeaderboardComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var leaderboardXform))
        {
            if (leaderboardXform.MapID == playerMapId)
                return true;
        }

        return false;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        // Cleanup на случай, если система выключается
        if (_overlayMan.HasOverlay<ThunderdomeLeaderboardOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }
}
