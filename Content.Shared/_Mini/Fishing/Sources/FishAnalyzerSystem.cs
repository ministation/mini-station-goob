// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing.Aquarium;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Sources;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Network;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Handles scanning fish and aquariums with the fish analyzer and feeding the result to the window.
/// </summary>
public sealed partial class FishAnalyzerSystem : EntitySystem
{
    /// <summary>
    /// How far from the user the scanned specimen may be when the window is opened again.
    /// </summary>
    private const float ScanRange = 7f;

    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishSystem _fish = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedAquariumSystem _aquarium = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_net.IsServer)
            return;

        SubscribeLocalEvent<FishAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FishAnalyzerComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<FishAnalyzerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FishAnalyzerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FishComponent, GetVerbsEvent<InteractionVerb>>(OnFishVerbs);
        SubscribeLocalEvent<AquariumComponent, GetVerbsEvent<InteractionVerb>>(OnAquariumVerbs);
    }

    private void OnAfterInteract(Entity<FishAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        List<EntityUid> fish;

        if (HasComp<FishComponent>(target))
        {
            fish = new List<EntityUid> { target };
        }
        else if (TryComp<AquariumComponent>(target, out var aquarium))
        {
            fish = _aquarium.GetFish((target, aquarium))
                .Where(f => HasComp<FishComponent>(f.Owner))
                .Select(f => f.Owner)
                .ToList();

            if (fish.Count == 0)
            {
                _popup.PopupClient(Loc.GetString("fishing-analyzer-empty-tank"), ent, args.User);
                args.Handled = true;
                return;
            }
        }
        else
        {
            return;
        }

        Scan(ent, args.User, fish);
        args.Handled = true;
    }

    private void OnUseInHand(Entity<FishAnalyzerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Чужая рыба обычно лежит в другой руке: сканируем то, что держим, без клика по миру.
        if (FindHeldFish(args.User) is { } held)
        {
            Scan(ent, args.User, new List<EntityUid> { held });
            return;
        }

        Show(ent, args.User);
    }

    private void OnFishVerbs(Entity<FishComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        if (FindHeldAnalyzer(args.User) is not { } analyzer ||
            !TryComp<FishAnalyzerComponent>(analyzer, out var comp))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("fishing-analyzer-verb"),
            Act = () => Scan((analyzer, comp), user, new List<EntityUid> { ent.Owner }),
        });
    }

    private void OnAquariumVerbs(Entity<AquariumComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HandsComponent>(args.User))
            return;

        if (FindHeldAnalyzer(args.User) is not { } analyzer ||
            !TryComp<FishAnalyzerComponent>(analyzer, out var comp))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("fishing-analyzer-verb"),
            Act = () =>
            {
                var fish = _aquarium.GetFish(ent)
                    .Where(f => HasComp<FishComponent>(f.Owner))
                    .Select(f => f.Owner)
                    .ToList();

                if (fish.Count == 0)
                {
                    _popup.PopupClient(Loc.GetString("fishing-analyzer-empty-tank"), analyzer, user);
                    return;
                }

                Scan((analyzer, comp), user, fish);
            },
        });
    }

    private void OnUiOpened(Entity<FishAnalyzerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!HasFreshScan(ent, args.Actor))
        {
            _ui.CloseUi(ent.Owner, FishAnalyzerUiKey.Key, args.Actor);
            _popup.PopupClient(Loc.GetString("fishing-analyzer-lost"), ent, args.Actor);
            return;
        }

        _ui.SetUiState(ent.Owner, FishAnalyzerUiKey.Key, BuildState(ent.Comp.ScannedFish));
    }

    private void OnExamined(Entity<FishAnalyzerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fishing-analyzer-examine"));
    }

    private void Scan(Entity<FishAnalyzerComponent> ent, EntityUid user, List<EntityUid> fish)
    {
        ent.Comp.ScannedFish = fish.Where(f => HasComp<FishComponent>(f)).ToList();
        Show(ent, user);
    }

    private void Show(Entity<FishAnalyzerComponent> ent, EntityUid user)
    {
        if (!HasFreshScan(ent, user))
        {
            _popup.PopupClient(Loc.GetString("fishing-analyzer-no-data"), ent, user);
            return;
        }

        if (_ui.IsUiOpen(ent.Owner, FishAnalyzerUiKey.Key, user))
            _ui.SetUiState(ent.Owner, FishAnalyzerUiKey.Key, BuildState(ent.Comp.ScannedFish));
        else
            _ui.OpenUi(ent.Owner, FishAnalyzerUiKey.Key, user);
    }

    /// <summary>
    /// True when every fish from the last scan still exists and is close enough to read.
    /// </summary>
    private bool HasFreshScan(Entity<FishAnalyzerComponent> ent, EntityUid? user)
    {
        if (ent.Comp.ScannedFish.Count == 0)
            return false;

        foreach (var fish in ent.Comp.ScannedFish)
        {
            if (!Exists(fish) || !HasComp<FishComponent>(fish))
            {
                ent.Comp.ScannedFish.Clear();
                return false;
            }

            if (user is { } reader && !InRange(reader, fish))
            {
                ent.Comp.ScannedFish.Clear();
                return false;
            }
        }

        return true;
    }

    private bool InRange(EntityUid reader, EntityUid target)
    {
        var readerXform = Transform(reader);
        var targetXform = Transform(target);

        if (readerXform.MapID != targetXform.MapID)
            return false;

        return (_xform.GetWorldPosition(readerXform) - _xform.GetWorldPosition(targetXform)).Length() <= ScanRange;
    }

    private EntityUid? FindHeldFish(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return null;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<FishComponent>(held))
                return held;
        }

        return null;
    }

    private EntityUid? FindHeldAnalyzer(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return null;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<FishAnalyzerComponent>(held))
                return held;
        }

        return null;
    }

    private FishAnalyzerBoundUserInterfaceState BuildState(List<EntityUid> fish)
    {
        var entries = new List<FishAnalyzerEntry>(fish.Count);

        foreach (var fishUid in fish)
        {
            if (!TryComp<FishComponent>(fishUid, out var comp))
                continue;

            var entry = new FishAnalyzerEntry
            {
                Species = MetaData(fishUid).EntityPrototype?.ID ?? string.Empty,
                Fish = comp.Fish?.Id,
                Size = comp.Size,
                Weight = comp.Weight,
                Alive = comp.Alive,
                Catcher = comp.Catcher,
                CatchMinutes = comp.CatchDate is { } date ? (float) date.TotalMinutes : -1f,
                HasExtraTraits = comp.ExtraTraits.Count > 0,
            };

            foreach (var trait in _fish.GetTraits(comp))
                entry.Traits.Add(trait.Id);

            entries.Add(entry);
        }

        return new FishAnalyzerBoundUserInterfaceState(entries);
    }
}
