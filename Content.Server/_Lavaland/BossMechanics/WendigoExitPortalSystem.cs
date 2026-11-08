// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG parity for <see cref="WendigoExitPortalComponent"/>: spawns a one-way portal at the
/// arena origin when the boss dies, linked to an exit point outside the arena.
/// </summary>
public sealed class WendigoExitPortalSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly LinkedEntitySystem _linked = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoExitPortalComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<WendigoExitPortalComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<WendigoBossComponent>(ent, out var boss) || !boss.Origin.IsValid(EntityManager))
            return;

        var origin = boss.Origin;
        var entry = Spawn(ent.Comp.PortalProto, origin);
        var exit = Spawn(ent.Comp.ExitProto, origin.Offset(ent.Comp.ExitOffset));

        _linked.OneWayLink(entry, exit);
    }
}
