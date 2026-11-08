// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.RPSX.DarkForces.Ratvar.Construction;
using Robust.Client.Placement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.RPSX.DarkForces.Ratvar.Construction;

public sealed class RatvarConstructionSystem : EntitySystem
{
    [Dependency] private readonly ConstructionSystem _construction = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RatvarConstructionComponent, RatvarConstructionSelectedMessage>(OnItemReceived);
    }

    private void OnItemReceived(Entity<RatvarConstructionComponent> ent, ref RatvarConstructionSelectedMessage args)
    {
        if (!_proto.TryIndex(args.SelectedItem, out ConstructionPrototype? prototype) ||
            !_gameTiming.IsFirstTimePredicted)
            return;

        if (prototype.Type == ConstructionType.Item)
        {
            _construction.TryStartItemConstruction(prototype.ID);
            return;
        }

        var hijack = new ConstructionPlacementHijack(_construction, prototype);

        _placement.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = prototype.PlacementMode,
            },
            hijack);
    }
}
