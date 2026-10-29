// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Sources;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Goobstation.Shared.Fishing.Systems;

/// <summary>
/// Handles scanning a fish with the fish analyzer and feeding the result to the analyzer window.
/// </summary>
public sealed partial class FishAnalyzerSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FishSystem _fish = default!;
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
    }

    private void OnAfterInteract(Entity<FishAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !HasComp<FishComponent>(target))
            return;

        if (!args.CanReach)
            return;

        args.Handled = true;

        if (_ui.IsUiOpen(ent.Owner, FishAnalyzerUiKey.Key, args.User))
        {
            _ui.SetUiState(ent.Owner, FishAnalyzerUiKey.Key, BuildState(target));
            return;
        }

        ent.Comp.Scanned = target;
        _ui.OpenUi(ent.Owner, FishAnalyzerUiKey.Key, args.User);
    }

    private void OnUseInHand(Entity<FishAnalyzerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Scanned is not { } scanned || !Exists(scanned) || !HasComp<FishComponent>(scanned))
        {
            _popup.PopupClient(Loc.GetString("fishing-analyzer-no-data"), ent, args.User);
            return;
        }

        _ui.OpenUi(ent.Owner, FishAnalyzerUiKey.Key, args.User);
    }

    private void OnUiOpened(Entity<FishAnalyzerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ent.Comp.Scanned is not { } scanned || !Exists(scanned) || !HasComp<FishComponent>(scanned))
            return;

        _ui.SetUiState(ent.Owner, FishAnalyzerUiKey.Key, BuildState(scanned));
    }

    private void OnExamined(Entity<FishAnalyzerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fishing-analyzer-examine"));
    }

    private FishAnalyzerBoundUserInterfaceState BuildState(EntityUid fish)
    {
        var entry = new FishAnalyzerEntry
        {
            Species = MetaData(fish).EntityPrototype?.ID ?? string.Empty,
        };

        if (TryComp<FishComponent>(fish, out var comp))
        {
            entry.Fish = comp.Fish?.Id;
            entry.Size = comp.Size;
            entry.Weight = comp.Weight;
            entry.Alive = comp.Alive;
            entry.Catcher = comp.Catcher;
            entry.CatchMinutes = comp.CatchDate is { } date ? (float) date.TotalMinutes : -1f;
            entry.HasExtraTraits = comp.ExtraTraits.Count > 0;

            foreach (var trait in _fish.GetTraits(comp))
            {
                entry.Traits.Add(trait.Id);
            }
        }

        return new FishAnalyzerBoundUserInterfaceState(new List<FishAnalyzerEntry> { entry });
    }
}
