// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Necropolis;

/// <summary>
/// Wakes the tumor when players come close, spawns a random elite,
/// and marks the tumor popped when the elite dies.
/// </summary>
public sealed class EliteTumorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EliteTumorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EliteTumorTrackerComponent, MobStateChangedEvent>(OnEliteStateChanged);
    }

    private void OnMapInit(Entity<EliteTumorComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, EliteTumorVisuals.State, EliteTumorVisualState.Dormant);
    }

    private void OnEliteStateChanged(Entity<EliteTumorTrackerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || TerminateOrDeletedTumor(ent.Comp.Tumor))
            return;

        _appearance.SetData(ent.Comp.Tumor, EliteTumorVisuals.State, EliteTumorVisualState.Popped);
        RemComp<EliteTumorTrackerComponent>(ent);
    }

    private bool TerminateOrDeletedTumor(EntityUid tumor)
        => TerminatingOrDeleted(tumor) || !TryComp<EliteTumorComponent>(tumor, out _);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EliteTumorComponent>();
        while (query.MoveNext(out var uid, out var tumor))
        {
            if (tumor.Triggered)
                continue;

            // Only living players wake the tumor; ghosts and fauna do not.
            var hasCrew = false;
            foreach (var ent in _lookup.GetEntitiesInRange<ActorComponent>(Transform(uid).Coordinates, tumor.TriggerRadius))
            {
                if (HasComp<GhostComponent>(ent))
                    continue;
                hasCrew = true;
                break;
            }

            if (!hasCrew)
                continue;

            tumor.Triggered = true;
            var eliteProto = _random.Pick(tumor.Elites);
            var elite = Spawn(eliteProto, Transform(uid).Coordinates.Offset(new Vector2(0, -1)));

            var tracker = EnsureComp<EliteTumorTrackerComponent>(elite);
            tracker.Tumor = uid;

            _appearance.SetData(uid, EliteTumorVisuals.State, EliteTumorVisualState.Advanced);
        }
    }
}

/// <summary>
/// Links a spawned elite back to its tumor so the tumor pops on elite death.
/// </summary>
[RegisterComponent]
public sealed partial class EliteTumorTrackerComponent : Component
{
    [DataField]
    public EntityUid Tumor;
}
