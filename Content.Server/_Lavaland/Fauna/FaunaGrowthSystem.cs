// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Fauna;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Fauna;

/// <summary>
/// Grows lavaland fauna into their next stage after a fixed time.
/// TG parity: growth_and_differentiation (grublunch -> warrior/milk gubbuck,
/// juvenile lobstrosity -> adult).
/// </summary>
public sealed class FaunaGrowthSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly LavalandTamingSystem _taming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FaunaGrowthComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FaunaGrowthComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextGrowthTime = _timing.CurTime + ent.Comp.GrowthTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var toGrow = new List<Entity<FaunaGrowthComponent>>();

        var query = EntityQueryEnumerator<FaunaGrowthComponent>();
        while (query.MoveNext(out var uid, out var growth))
        {
            if (now < growth.NextGrowthTime)
                continue;
            if (_mobState.IsDead(uid))
                continue;

            toGrow.Add((uid, growth));
        }

        foreach (var (uid, growth) in toGrow)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (growth.GrowInto.Count == 0)
                continue;

            var pick = growth.GrowInto[_random.Next(growth.GrowInto.Count)];
            if (!_prototypes.HasIndex<EntityPrototype>(pick))
                continue;

            var xform = Transform(uid);
            var grown = Spawn(pick, xform.Coordinates);
            _taming.TransferTaming(uid, grown);
            QueueDel(uid);
        }
    }
}
