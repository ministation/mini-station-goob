// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Shared._Goobstation.Weapons.SmartGun;
using Content.Shared.CombatMode;
using Content.Shared.Hands.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Client.Weapons.LaserPointer;

public sealed class LaserPointerSystem : SharedLaserPointerSystem
{
    private const float UpdateInterval = 0.05f;

    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private float _updateAccumulator;
    private EntityUid? _lastHovered;
    private Vector2? _lastDir;
    private NetEntity? _lastPointer;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new LaserPointerOverlay(EntityManager, _prototype));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<LaserPointerOverlay>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        if (!TryComp(_player.LocalEntity, out HandsComponent? hands) ||
            !TryComp(_player.LocalEntity.Value, out TransformComponent? xform))
            return;

        var player = _player.LocalEntity.Value;

        EntityUid? hovered = null;
        MapCoordinates? mousePos = null;
        if (_state.CurrentState is GameplayStateBase screen && TryComp(player, out CombatModeComponent? combat) &&
            combat.IsInCombatMode)
        {
            mousePos = _eye.PixelToMap(_input.MouseScreenPosition);

            if (mousePos.Value.MapId == MapId.Nullspace)
                mousePos = null;
            else
                hovered = screen.GetDamageableClickedEntity(mousePos.Value);
        }

        Vector2? dir = mousePos == null ? null : mousePos.Value.Position - _transform.GetWorldPosition(xform);

        var sent = false;
        foreach (var held in _hands.EnumerateHeld((player, hands)))
        {
            if (!HasComp<LaserPointerComponent>(held))
                continue;

            var pointer = GetNetEntity(held);
            EntityUid? target = null;
            if (hovered != null && TryComp(held, out WieldableComponent? wieldable) && wieldable.Wielded)
                target = hovered;

            var changed = target != _lastHovered
                || !Approximately(dir, _lastDir)
                || pointer != _lastPointer;

            if (!changed && _updateAccumulator < UpdateInterval)
                continue;

            RaisePredictiveEvent(new LaserPointerEntityHoveredEvent(
                target == null ? null : GetNetEntity(target.Value),
                dir,
                pointer));

            _lastHovered = target;
            _lastDir = dir;
            _lastPointer = pointer;
            sent = true;
        }

        if (sent)
            _updateAccumulator = 0f;
        else
            _updateAccumulator += frameTime;
    }

    private static bool Approximately(Vector2? a, Vector2? b)
    {
        if (a == null || b == null)
            return a == b;

        return a.Value.EqualsApprox(b.Value, 0.01f);
    }
}
