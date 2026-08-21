// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Trauma.Genetics.Abilities;
using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Server._Trauma.Genetics.Abilities;

public sealed partial class ThermalRegulatorMutationSystem : EntitySystem
{
    [Dependency] private ThermalRegulatorSystem _regulator = default!;
    [Dependency] private EntityQuery<ThermalRegulatorComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<ThermalRegulatorMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<ThermalRegulatorMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (!_query.HasComp(args.Target))
            return;

        _regulator.ScaleHeatRegulation(args.Target.Owner, ent.Comp.Shivering, ent.Comp.Sweating, ent.Comp.Metabolism, ent.Comp.Regulation);
    }

    private void OnRemoved(Entity<ThermalRegulatorMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!_query.HasComp(args.Target))
            return;

        _regulator.ScaleHeatRegulation(args.Target.Owner, 1f / ent.Comp.Shivering, 1f / ent.Comp.Sweating, 1f / ent.Comp.Metabolism, 1f / ent.Comp.Regulation);
    }
}
