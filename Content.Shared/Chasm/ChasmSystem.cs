// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Weapons.Misc;
using Content.Shared.Item;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Chasm;

/// <summary>
///     Handles making entities fall into chasms when stepped on.
/// </summary>
public sealed class ChasmSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGrapplingGunSystem _grapple = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ChasmComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ChasmComponent, MapInitEvent>(OnChasmMapInit);
        SubscribeLocalEvent<ChasmFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    /// <summary>
    /// Mini: a chasm can be spawned under an entity that is already standing there (admin spawn or
    /// a chasm opening). A sleeping physics body does not always produce a collision event for the
    /// newly spawned floor, so such an entity would just stand on the chasm. Trigger the fall for
    /// everyone already overlapping the chasm when it appears.
    /// </summary>
    private void OnChasmMapInit(EntityUid uid, ChasmComponent component, MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var overlapping = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, 0.45f, overlapping);

        foreach (var tripper in overlapping)
        {
            if (tripper == uid
                || HasComp<ChasmFallingComponent>(tripper)
                || !TryComp<PhysicsComponent>(tripper, out var physics)
                || !physics.CanCollide
                || !TryComp<FixturesComponent>(tripper, out var fixtures))
                continue;

            var hasHardFixture = false;
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;
                hasHardFixture = true;
                break;
            }

            if (!hasHardFixture)
                continue;

            var attempt = new StepTriggerAttemptEvent { Source = uid, Tripper = tripper };
            RaiseLocalEvent(uid, ref attempt);
            RaiseLocalEvent(tripper, ref attempt);

            if (attempt.Cancelled || !attempt.Continue)
                continue;

            StartFalling(uid, component, tripper);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // don't predict queuedels on client
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<ChasmFallingComponent>();
        while (query.MoveNext(out var uid, out var chasm))
        {
            if (_timing.CurTime < chasm.NextDeletionTime)
                continue;

            // Lavaland Change start: Jaunter
            var ev = new _Lavaland.Chasm.BeforeChasmFallingEvent(uid);
            RaiseLocalEvent(uid, ref ev);
            if (ev.Cancelled)
            {
                RemComp<ChasmFallingComponent>(uid);
                _blocker.UpdateCanMove(uid);
                continue;
            }
            // Lavaland Change end: Jaunter

            // tg keeps whatever falls in the chasm, so it can be fished back out.
            if (chasm.Source is { } chasmUid && !Deleted(chasmUid) && HasComp<ItemComponent>(uid))
            {
                var storage = EnsureComp<ChasmStorageComponent>(chasmUid);
                var container = _container.EnsureContainer<Container>(chasmUid, ChasmStorageComponent.ContainerId);
                if (_container.Insert(uid, container))
                {
                    RemComp<ChasmFallingComponent>(uid);
                    _blocker.UpdateCanMove(uid);
                    continue;
                }
            }

            QueueDel(uid);
        }
    }

    private void OnStepTriggered(EntityUid uid, ChasmComponent component, ref StepTriggeredOffEvent args)
    {
        // already doomed
        if (HasComp<ChasmFallingComponent>(args.Tripper))
            return;

        StartFalling(uid, component, args.Tripper);
    }

    public void StartFalling(EntityUid chasm, ChasmComponent component, EntityUid tripper, bool playSound = true)
    {
        var falling = AddComp<ChasmFallingComponent>(tripper);

        falling.Source = chasm;
        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        if (playSound)
            _audio.PlayPredicted(component.FallingSound, chasm, tripper);
    }

    private void OnStepTriggerAttempt(EntityUid uid, ChasmComponent component, ref StepTriggerAttemptEvent args)
    {
        if (_grapple.IsEntityHooked(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        args.Continue = true;
    }

    private void OnUpdateCanMove(EntityUid uid, ChasmFallingComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
}
