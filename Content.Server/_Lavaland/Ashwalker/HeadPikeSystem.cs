// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Containers;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// TG head pike: takes a severed head and puts it on the spear, empty hand takes it back.
/// </summary>
public sealed class HeadPikeSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadPikeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HeadPikeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HeadPikeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<HeadPikeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<HeadPikeComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<AshwalkerTribeMemberComponent>(args.Examiner))
            return;

        // TG gives an ash walker a mood buff for this; the fork has no mood system, so show the line.
        args.PushMarkup(Loc.GetString("ashwalker-sacrifice-good"));
    }

    private void OnMapInit(Entity<HeadPikeComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent.Owner, HeadPikeVisuals.HasHead, false);
    }

    private void OnInteractUsing(Entity<HeadPikeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Head != null)
            return;

        if (!TryComp<BodyPartComponent>(args.Used, out var part) || part.PartType != BodyPartType.Head)
            return;

        if (!_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container)
            || !_container.Insert(args.Used, container))
            return;

        ent.Comp.Head = args.Used;
        _appearance.SetData(ent.Owner, HeadPikeVisuals.HasHead, true);
        _metaData.SetEntityName(ent.Owner, Loc.GetString("tribe-headpike-name", ("head", Name(args.Used))));
        args.Handled = true;
    }

    private void OnInteractHand(Entity<HeadPikeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Head is not { } head)
            return;

        if (_container.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container))
            _container.Remove(head, container);

        ent.Comp.Head = null;
        _appearance.SetData(ent.Owner, HeadPikeVisuals.HasHead, false);
        _metaData.SetEntityName(ent.Owner, Loc.GetString("ent-TribeHeadPike"));
        args.Handled = true;
    }
}
