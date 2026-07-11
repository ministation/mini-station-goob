using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarCaptureZoneOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly TypanWarUiSystem _war = default!;

    private TypanWarCaptureZoneOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _war.StatusUpdated += OnStatusUpdated;
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _war.StatusUpdated -= OnStatusUpdated;
        RemoveOverlay();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) => RemoveOverlay();

    private void OnStatusUpdated()
    {
        if (_war.Phase == TypanWarPhase.Active)
            EnsureOverlay();
        else
            RemoveOverlay();
    }

    private void EnsureOverlay()
    {
        _overlay ??= new TypanWarCaptureZoneOverlay();
        if (!_overlayMan.HasOverlay<TypanWarCaptureZoneOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay != null && _overlayMan.HasOverlay<TypanWarCaptureZoneOverlay>())
            _overlayMan.RemoveOverlay(_overlay);
    }
}
