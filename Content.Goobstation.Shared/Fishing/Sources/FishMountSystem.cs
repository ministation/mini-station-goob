// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Goobstation.Shared.Fishing.Systems;

/// <summary>
/// Mounting a caught fish on a trophy mount, taking it off and taking the mount down.
/// </summary>
public sealed partial class FishMountSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";

    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishMountComponent, InteractUsingEvent>(OnMountInteractUsing);
        SubscribeLocalEvent<FishMountComponent, InteractHandEvent>(OnMountInteractHand);
        SubscribeLocalEvent<FishMountComponent, ExaminedEvent>(OnMountExamined);
        SubscribeLocalEvent<FishMountItemComponent, AfterInteractEvent>(OnFrameAfterInteract);
    }

    private void OnMountInteractUsing(Entity<FishMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, ScrewingQuality))
        {
            args.Handled = true;
            TakeDown(ent);
            return;
        }

        if (!HasComp<FishComponent>(args.Used))
            return;

        args.Handled = true;

        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            _popup.PopupClient(Loc.GetString("fish-mount-occupied"), ent, args.User);
            return;
        }

        _container.Insert(args.Used, container);
        _audio.PlayPredicted(new SoundPathSpecifier("/Audio/Machines/click.ogg"), ent, args.User);
        _popup.PopupClient(Loc.GetString("fish-mount-mounted"), ent, args.User);
    }

    private void OnMountInteractHand(Entity<FishMountComponent> ent, ref InteractHandEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("fish-mount-empty"), ent, args.User);
            return;
        }

        var fish = container.ContainedEntities[0];
        _container.Remove(fish, container);

        if (!_hands.TryPickupAnyHand(args.User, fish, checkActionBlocker: false))
        {
            _xform.SetCoordinates(fish, Transform(ent.Owner).Coordinates);
            _popup.PopupClient(Loc.GetString("fish-mount-removed"), ent, args.User);
            return;
        }

        _popup.PopupClient(Loc.GetString("fish-mount-removed"), ent, args.User);
    }

    private void TakeDown(Entity<FishMountComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        _container.EmptyContainer(container, destination: Transform(ent.Owner).Coordinates);

        var frame = Spawn(ent.Comp.Frame, Transform(ent.Owner).Coordinates);
        _xform.SetLocalRotationNoLerp(frame, Transform(ent.Owner).LocalRotation);
        QueueDel(ent.Owner);
    }

    private void OnMountExamined(Entity<FishMountComponent> ent, ref ExaminedEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return;

        if (container.ContainedEntities.Count == 0)
            return;

        var fish = container.ContainedEntities[0];
        if (!TryComp<FishComponent>(fish, out var fishComp))
            return;

        args.PushMarkup(Loc.GetString("fish-mount-examine-size",
            ("size", MathF.Round(fishComp.Size)),
            ("weight", MathF.Round(fishComp.Weight))));

        if (fishComp.Catcher is { } catcher)
            args.PushMarkup(Loc.GetString("fish-mount-examine-catcher", ("catcher", catcher)));

        if (fishComp.CatchDate is { } catchDate)
            args.PushMarkup(Loc.GetString("fish-mount-examine-date", ("minutes", (int) catchDate.TotalMinutes)));
    }

    private void OnFrameAfterInteract(Entity<FishMountItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target != null || !args.CanReach || !args.ClickLocation.IsValid(EntityManager))
            return;

        Spawn(ent.Comp.Mount, args.ClickLocation);
        args.Handled = true;
        QueueDel(ent.Owner);
    }
}
