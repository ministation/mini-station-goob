// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing/fishing_equipment.dm,
// code/modules/mining/shelters.dm, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// The fishing spot capsule: deploys a fishing spot connected to a faraway place.
/// Unsafe spots stay locked until the capsule is emagged, like in tg.
/// </summary>
public sealed partial class FishingSpotCapsuleSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingSpotCapsuleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<FishingSpotCapsuleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FishingSpotCapsuleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FishingSpotCapsuleComponent, FishingSpotCapsuleSelectMessage>(OnSelect);
        SubscribeLocalEvent<FishingSpotCapsuleComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnUseInHand(Entity<FishingSpotCapsuleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = Deploy(ent, args.User);
    }

    private void OnGetVerbs(Entity<FishingSpotCapsuleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fishing-spot-capsule-change"),
            Category = VerbCategory.Adjust,
            Act = () => _ui.OpenUi(ent.Owner, FishingSpotCapsuleUiKey.Key, user),
        });
    }

    private void OnUiOpened(Entity<FishingSpotCapsuleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, FishingSpotCapsuleUiKey.Key, new FishingSpotCapsuleBoundUserInterfaceState(GetOptions(ent)));
    }

    private void OnSelect(Entity<FishingSpotCapsuleComponent> ent, ref FishingSpotCapsuleSelectMessage args)
    {
        if (args.SourceId is not { } sourceId)
            return;

        var option = FindOption(ent.Comp, sourceId);
        if (option == null)
            return;

        // Unsafe spots are hidden until emagged; reject forged selections too.
        if (!option.Safe && !ent.Comp.Emagged)
            return;

        ent.Comp.SelectedSource = option.Source;
        Dirty(ent.Owner, ent.Comp);

        if (!args.Actor.Valid)
            return;

        _popup.PopupClient(Loc.GetString("fishing-spot-capsule-selected", ("spot", Loc.GetString(option.Name))), args.Actor, args.Actor);
        _audio.PlayPvs(ent.Comp.SelectSound, ent.Owner, AudioParams.Default.WithVolume(-4f));
    }

    private void OnEmagged(Entity<FishingSpotCapsuleComponent> ent, ref GotEmaggedEvent args)
    {
        if (ent.Comp.Emagged)
            return;

        // tg: "Use a cryptographic sequencer to disable safeties".
        ent.Comp.Emagged = true;
        Dirty(ent.Owner, ent.Comp);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("fishing-spot-capsule-safeties"), ent.Owner, args.UserUid);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks2.ogg"), ent.Owner, AudioParams.Default.WithVolume(-4f));
    }

    private bool Deploy(Entity<FishingSpotCapsuleComponent> ent, EntityUid user)
    {
        var option = FindOption(ent.Comp, ent.Comp.SelectedSource);
        if (option == null)
            return false;

        if (!FindFreeTile(user, out var coords))
        {
            _popup.PopupClient(Loc.GetString("fishing-spot-capsule-bad-area"), ent.Owner, user, PopupType.MediumCaution);
            return false;
        }

        if (option.WaterOverlay)
            Spawn("FloorWaterEntity", coords);

        Spawn(option.Spot, coords);

        _popup.PopupClient(Loc.GetString("fishing-spot-capsule-deployed", ("spot", Loc.GetString(option.Name))), ent.Owner, user);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg"), coords);
        QueueDel(ent.Owner);
        return true;
    }

    private bool FindFreeTile(EntityUid user, out EntityCoordinates coords)
    {
        coords = EntityCoordinates.Invalid;

        var xform = Transform(user);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        // Space has no ground to hold the water.
        if (_map.TryGetTileRef(gridUid, grid, indices, out var tileRef) && tileRef.Tile.IsEmpty)
            return false;

        // Anything already anchored blocks the deploy, including other fishing spots.
        foreach (var _ in _map.GetAnchoredEntities(gridUid, grid, indices))
            return false;

        coords = _map.GridTileToLocal(gridUid, grid, indices);
        return true;
    }

    private FishingSpotCapsuleOption? FindOption(FishingSpotCapsuleComponent comp, ProtoId<FishingSourcePrototype> source)
    {
        foreach (var option in comp.Spots)
        {
            if (option.Source == source)
                return option;
        }

        return null;
    }

    private List<FishingSpotCapsuleOptionState> GetOptions(Entity<FishingSpotCapsuleComponent> ent)
    {
        var options = new List<FishingSpotCapsuleOptionState>();

        foreach (var spot in ent.Comp.Spots)
        {
            // tg hides unsafe templates until the capsule is emagged.
            if (!spot.Safe && !ent.Comp.Emagged)
                continue;

            if (!_proto.HasIndex(spot.Source))
                continue;

            var description = spot.Description == default ? string.Empty : Loc.GetString(spot.Description);
            options.Add(new FishingSpotCapsuleOptionState(
                spot.Source,
                Loc.GetString(spot.Name),
                description,
                new SpriteSpecifier.Rsi(new ResPath("_Goobstation/Interface/Fishing/radial.rsi"), spot.RadialState)));
        }

        return options;
    }
}
