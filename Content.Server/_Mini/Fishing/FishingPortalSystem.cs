// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// The fish-porter 3000: lets the player pick any fishing source and fish right at the machine.
/// </summary>
public sealed partial class FishingPortalSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingPortalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FishingPortalComponent, FishingPortalSelectMessage>(OnSelect);
    }

    private void OnUiOpened(Entity<FishingPortalComponent> ent, ref BoundUIOpenedEvent args)
    {
        var options = new List<FishingPortalOption>();

        foreach (var source in _proto.EnumeratePrototypes<FishingSourcePrototype>().OrderBy(p => p.ID))
        {
            var name = source.RadialName is { } radial
                ? Loc.GetString(radial)
                : source.CatalogDescription is { } key
                    ? Loc.GetString(key)
                    : Loc.GetString("fishing-source-secret");

            options.Add(new FishingPortalOption(
                source.ID,
                name,
                new SpriteSpecifier.Rsi(new ResPath("_Goobstation/Interface/Fishing/radial.rsi"), source.RadialState)));
        }

        _ui.SetUiState(ent.Owner, FishingPortalUiKey.Key, new FishingPortalBoundUserInterfaceState(options));
    }

    private void OnSelect(Entity<FishingPortalComponent> ent, ref FishingPortalSelectMessage args)
    {
        if (args.SourceId == null)
        {
            RemComp<FishingSourceComponent>(ent.Owner);
            ent.Comp.ActiveSource = null;
            Dirty(ent, ent.Comp);
            return;
        }

        if (!_proto.TryIndex(args.SourceId, out FishingSourcePrototype? selected))
            return;

        var source = EnsureComp<FishingSourceComponent>(ent.Owner);
        source.Source = selected.ID;
        Dirty(ent.Owner, source);

        ent.Comp.ActiveSource = selected.ID;
        Dirty(ent, ent.Comp);
    }
}
