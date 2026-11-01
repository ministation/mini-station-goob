// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.Paper;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Fills fishing tip papers with a pearl of wisdom about a random catalog fish, like tg fishing tips.
/// </summary>
public sealed class FishingTipSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private List<(ProtoId<FishPrototype> Data, string Entity)>? _catalogFish;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingTipComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FishingTipComponent> ent, ref MapInitEvent args)
    {
        if (!_net.IsServer || !TryComp<PaperComponent>(ent, out var paper))
            return;

        paper.Content = ent.Comp.Instructions ? BuildInstructions() : BuildTip();
        Dirty(ent.Owner, paper);
    }

    private string BuildInstructions()
    {
        return string.Join("\n\n", new[]
        {
            Loc.GetString("fishing-lures-instructions-1"),
            Loc.GetString("fishing-lures-instructions-2"),
            Loc.GetString("fishing-lures-instructions-3"),
            Loc.GetString("fishing-lures-instructions-4"),
        });
    }

    private string BuildTip()
    {
        var options = CatalogFish();
        if (options.Count == 0)
            return Loc.GetString("fishing-tip-empty");

        var (dataId, entityId) = _random.Pick(options);
        var data = _proto.Index(dataId);
        var fishName = _proto.Index<EntityPrototype>(entityId).Name;

        var spots = new List<string>();
        foreach (var source in _proto.EnumeratePrototypes<FishingSourcePrototype>())
        {
            if (source.CatalogDescription is not { } description)
                continue;

            var sourceFish = new HashSet<string>();
            CollectIds(source.Table, sourceFish);
            if (sourceFish.Contains(entityId))
                spots.Add(Loc.GetString(description));
        }

        var favourite = BaitNames(data.FavoriteBaits, data.FavoriteReagents, data.FavoriteBaitTraits);
        var disliked = BaitNames(data.DislikedBaits, data.DislikedReagents, data.DislikedBaitTraits);

        var difficulty = data.DifficultyModifier;
        var traits = new List<string>();
        foreach (var traitId in data.Traits)
        {
            if (!_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                continue;

            traits.Add(Loc.GetString(trait.CatalogDescription));
            difficulty += trait.AddedDifficulty;
        }

        var difficultyText = difficulty < 10f
            ? Loc.GetString("fishing-tip-difficulty-easy")
            : difficulty < 20f
                ? Loc.GetString("fishing-tip-difficulty-medium")
                : Loc.GetString("fishing-tip-difficulty-hard");

        var parts = new[]
        {
            Loc.GetString("fishing-tip-header", ("fish", fishName)),
            Loc.GetString("fishing-tip-spots", ("spots", spots.Count == 0 ? Loc.GetString("fishing-tip-unknown") : string.Join(", ", spots))),
            Loc.GetString("fishing-tip-favourite", ("bait", favourite.Count == 0 ? Loc.GetString("fishing-tip-none") : string.Join(", ", favourite))),
            Loc.GetString("fishing-tip-disliked", ("bait", disliked.Count == 0 ? Loc.GetString("fishing-tip-none") : string.Join(", ", disliked))),
            Loc.GetString("fishing-tip-traits", ("traits", traits.Count == 0 ? Loc.GetString("fishing-tip-no-traits") : string.Join(", ", traits))),
            Loc.GetString("fishing-tip-difficulty", ("difficulty", difficultyText)),
        };

        return string.Join("\n", parts);
    }

    private List<string> BaitNames(List<EntProtoId> entities, Dictionary<ProtoId<ReagentPrototype>, float> reagents, List<ProtoId<FishTraitPrototype>> traits)
    {
        var names = new List<string>();
        foreach (var bait in entities)
        {
            if (_proto.TryIndex<EntityPrototype>(bait.Id, out var entityProto))
                names.Add(entityProto.Name);
        }

        foreach (var (reagent, amount) in reagents)
        {
            if (_proto.TryIndex(reagent, out ReagentPrototype? reagentProto))
                names.Add($"{reagentProto.LocalizedName}, {amount:0.#}u");
        }

        foreach (var trait in traits)
        {
            if (_proto.TryIndex(trait, out FishTraitPrototype? traitProto))
                names.Add(Loc.GetString(traitProto.Name));
        }

        return names;
    }

    private List<(ProtoId<FishPrototype> Data, string Entity)> CatalogFish()
    {
        if (_catalogFish != null)
            return _catalogFish;

        _catalogFish = new List<(ProtoId<FishPrototype>, string)>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<FishComponent>(out var fish, EntityManager.ComponentFactory) || fish.Fish is not { } dataId)
                continue;

            if (!_proto.TryIndex(dataId, out FishPrototype? data) || !data.ShowInCatalog)
                continue;

            _catalogFish.Add((dataId, proto.ID));
        }

        return _catalogFish;
    }

    private void CollectIds(EntityTableSelector selector, HashSet<string> ids)
    {
        switch (selector)
        {
            case EntSelector ent:
                ids.Add(ent.Id.Id);
                break;
            case GroupSelector group:
                foreach (var child in group.Children)
                    CollectIds(child, ids);
                break;
            case AllSelector all:
                foreach (var child in all.Children)
                    CollectIds(child, ids);
                break;
            case NestedSelector nested:
                if (_proto.TryIndex(nested.TableId, out EntityTablePrototype? table))
                    CollectIds(table.Table, ids);
                break;
        }
    }
}
