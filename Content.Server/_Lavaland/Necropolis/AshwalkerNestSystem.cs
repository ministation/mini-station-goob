// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Necropolis;

/// <summary>
/// Ashwalker nest from TG lavaland: consumes nearby corpses to grow meat,
/// and hatches ashwalker eggs (ghost roles) from the accumulated meat.
/// </summary>
public sealed class AshwalkerNestSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string Egg = "LavalandAshwalkerEgg";
    private const int MeatForEgg = 100;
    private const int MeatPerCorpse = 10;
    private const int MeatPerMegafauna = 25;

    private TimeSpan _nextScan = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshwalkerNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshwalkerNestComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnMapInit(Entity<AshwalkerNestComponent> ent, ref MapInitEvent args)
    {
        // The nest starts with some meat so a first egg comes quickly.
        ent.Comp.Meat = 40;
    }

    private void OnDestroyed(Entity<AshwalkerNestComponent> ent, ref DestructionEventArgs args)
    {
        var coords = Transform(ent).Coordinates;
        _popup.PopupCoordinates(Loc.GetString("ashwalker-nest-destroyed"), coords);
        Spawn("MaterialBones1", coords.Offset(new System.Numerics.Vector2(-0.5f, 0)));
        Spawn("MaterialBones1", coords.Offset(new System.Numerics.Vector2(0.5f, 0)));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextScan)
            return;
        _nextScan = now + TimeSpan.FromSeconds(2);

        var query = EntityQueryEnumerator<AshwalkerNestComponent>();
        while (query.MoveNext(out var uid, out var nest))
        {
            var coords = Transform(uid).Coordinates;

            // Consume corpses lying near the nest.
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, nest.ConsumeRadius))
            {
                if (mob.Comp.CurrentState != MobState.Dead || mob.Owner == uid)
                    continue;

                var meat = HasComp<MegafaunaAiComponent>(mob) ? MeatPerMegafauna : MeatPerCorpse;
                QueueDel(mob);
                nest.Meat += meat;
                Spawn("MaterialBones1", coords.Offset(_random.NextVector2Box(-0.4f, -0.4f, 0.4f, 0.4f)));
            }

            // Hatch an egg once enough meat is gathered.
            if (nest.Meat >= MeatForEgg && nest.Eggs < nest.MaxEggs)
            {
                nest.Meat -= MeatForEgg;
                nest.Eggs++;
                Spawn(Egg, coords.Offset(_random.NextVector2Box(-1.5f, -1.5f, 1.5f, 1.5f)));
            }
        }
    }
}
