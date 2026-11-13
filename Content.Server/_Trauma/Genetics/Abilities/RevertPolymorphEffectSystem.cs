// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.EntityEffects;
using Content.Shared._Trauma.Genetics.EntityEffects;

namespace Content.Server._Trauma.Genetics.Abilities;

/// <summary>
/// Reverts a polymorph. A reverting organ (like a shot tongue) goes back into its original body if it can.
/// </summary>
public sealed partial class RevertPolymorphEffectSystem : EntityEffectSystem<PolymorphedEntityComponent, RevertPolymorph>
{
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedBodySystem _body = default!;

    protected override void Effect(Entity<PolymorphedEntityComponent> ent, ref EntityEffectEvent<RevertPolymorph> args)
    {
        if (_polymorph.Revert(ent.AsNullable()) is not { } parent)
            return;

        if (!TryComp<OrganComponent>(parent, out var organ) ||
            organ.OriginalBody is not { } body ||
            Deleted(body))
            return;

        foreach (var part in _body.GetBodyChildren(body))
        {
            if (_body.InsertOrgan(part.Id, parent, organ.SlotId, part.Component, organ))
                return;
        }
    }
}
