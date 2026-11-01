// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Chasm;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Fishes items that fell into a chasm back out, like tgstation's chasm detritus.
/// </summary>
public sealed class ChasmDetritusSystem : EntitySystem
{
    private static readonly ProtoId<FishingSourcePrototype> ChasmSource = "FishingSourceChasm";

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly HashSet<Entity<ChasmStorageComponent>> _chasms = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingDetritusEvent>(OnDetritus);
    }

    private void OnDetritus(ref FishingDetritusEvent args)
    {
        if (args.Source != ChasmSource)
            return;

        _chasms.Clear();
        _lookup.GetEntitiesInRange<ChasmStorageComponent>(Transform(args.SourceEntity).Coordinates, 2f, _chasms);

        foreach (var chasm in _chasms)
        {
            if (!_container.TryGetContainer(chasm, ChasmStorageComponent.ContainerId, out var container))
                continue;

            if (container.ContainedEntities.Count == 0)
                continue;

            // tg rolls the default table a quarter of the time.
            if (_random.NextFloat() > chasm.Comp.StoredChance)
                return;

            var index = _random.Next(container.ContainedEntities.Count);
            args.ExistingCatch = container.ContainedEntities[index];
            return;
        }
    }
}
