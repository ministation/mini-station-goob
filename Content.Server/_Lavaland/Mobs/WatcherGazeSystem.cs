// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity for the watcher gaze (tgstation watcher_gaze.dm). The action warns everyone first, then after
/// <see cref="WarningDelay"/> affects every living entity in range that is still looking at the watcher.
/// </summary>
public sealed class WatcherGazeSystem : EntitySystem
{
    // Numbers from tgstation watcher_gaze.dm.
    private const float GazeRadius = 7f;
    private const float FireStacks = 10f;
    private const float IceThrowSpeed = 10f;

    private static readonly TimeSpan WarningDelay = TimeSpan.FromSeconds(1.6);
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ConfusionDuration = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FireParalyzeDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan IceFreezeDuration = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// SS14 has no confusion status effect. Drunk is the closest stand-in, the same one the genetics
    /// symptoms already use for tg's adjust_confusion.
    /// </summary>
    private static readonly EntProtoId Confusion = "StatusEffectDrunk";

    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WatcherGazeActionEvent>(OnGazeAction);
    }

    private void OnGazeAction(WatcherGazeActionEvent args)
    {
        if (args.Handled
            || !HasComp<WatcherGazeComponent>(args.Performer)
            || !_mobState.IsAlive(args.Performer))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("watcher-gaze-warning"), args.Performer, PopupType.MediumCaution);

        var watcher = args.Performer;
        Timer.Spawn(WarningDelay, () => ApplyGaze(watcher));
    }

    private void ApplyGaze(EntityUid watcher)
    {
        if (!Exists(watcher)
            || !_mobState.IsAlive(watcher)
            || !TryComp<WatcherGazeComponent>(watcher, out var gaze))
            return;

        var mapPos = _xform.GetMapCoordinates(watcher);
        foreach (var viewer in _lookup.GetEntitiesInRange(mapPos, GazeRadius))
        {
            if (viewer == watcher
                || !_mobState.IsAlive(viewer)
                || !IsLookingAt(viewer, mapPos.Position)
                || !_examine.InRangeUnOccluded(watcher, viewer, GazeRadius))
                continue;

            ApplyEffect(viewer, watcher, mapPos.Position, gaze.GazeType);
        }

        // tg stuns the watcher itself for a moment after the gaze lands, that is the window to punish it.
        _stun.TryAddParalyzeDuration(watcher, TimeSpan.FromSeconds(1.5));
    }

    private bool IsLookingAt(EntityUid viewer, Vector2 watcherPos)
    {
        var toWatcher = watcherPos - _xform.GetWorldPosition(viewer);
        if (toWatcher.LengthSquared() < 0.001f)
            return false;

        // tg bitwise-checks the viewer's 8-way dir against get_dir(viewer, owner), which is a ~90 degree cone.
        var facing = _xform.GetWorldRotation(viewer).ToVec();
        return Vector2.Dot(facing, Vector2.Normalize(toWatcher)) > 0.7f;
    }

    private void ApplyEffect(EntityUid viewer, EntityUid watcher, Vector2 watcherPos, WatcherGazeType type)
    {
        switch (type)
        {
            case WatcherGazeType.Fire:
                _stun.TryAddParalyzeDuration(viewer, FireParalyzeDuration);
                _flammable.AdjustFireStacks(viewer, FireStacks, ignite: true);
                return;
            case WatcherGazeType.Ice:
                // SS14 has no freon (immobilize only) status, a short paralyze is the closest equivalent.
                _stun.TryAddParalyzeDuration(viewer, IceFreezeDuration);

                var direction = _xform.GetWorldPosition(viewer) - watcherPos;
                if (direction.LengthSquared() < 0.0001f)
                    return;

                _throwing.TryThrow(viewer, direction.Normalized(), IceThrowSpeed,
                    user: watcher, pushbackRatio: 0f, doSpin: false);
                return;
            default:
                _flash.Flash(viewer, watcher, watcher, FlashDuration, 0.8f);

                // tg skips the confusion if the target is immune to flashes.
                if (!HasComp<FlashedComponent>(viewer))
                    return;

                _status.TryAddStatusEffectDuration(viewer, Confusion, ConfusionDuration);
                return;
        }
    }
}
