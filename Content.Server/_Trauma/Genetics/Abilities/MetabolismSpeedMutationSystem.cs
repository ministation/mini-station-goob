// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Shared._Trauma.Genetics.Abilities;
using Content.Shared._Trauma.Genetics.Mutations;
using Content.Shared.Body.Systems;

namespace Content.Server._Trauma.Genetics.Abilities;

/// <summary>
/// Applies the flat metabolism speed bonus of <see cref="MetabolismSpeedMutationComponent"/> to the mob's metabolizers.
/// </summary>
public sealed partial class MetabolismSpeedMutationSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private EntityQuery<MetabolizerComponent> _query = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<MetabolismSpeedMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<MetabolismSpeedMutationComponent> ent, ref MutationAddedEvent args)
    {
        Modify(args.Target, ent.Comp.Bonus);
    }

    private void OnRemoved(Entity<MetabolismSpeedMutationComponent> ent, ref MutationRemovedEvent args)
    {
        Modify(args.Target, -ent.Comp.Bonus);
    }

    private void Modify(EntityUid uid, float add)
    {
        // some mobs, like the dragon, have the metabolizer on the mob itself instead of on an organ
        if (_query.TryComp(uid, out var mobComp))
            mobComp.UpdateIntervalMultiplier += add;

        foreach (var (organId, _) in _body.GetBodyOrgans(uid))
        {
            if (_query.TryComp(organId, out var organComp))
                organComp.UpdateIntervalMultiplier += add;
        }
    }
}
