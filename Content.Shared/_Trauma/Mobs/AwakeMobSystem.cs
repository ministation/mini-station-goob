// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bed.Sleep;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared._Trauma.Mobs;

/// <summary>
/// Keeps <see cref="AwakeMobComponent"/> on mobs that are alive and not asleep.
/// </summary>
public sealed partial class AwakeMobSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SleepingComponent, SleepStateChangedEvent>(OnSleepStateChanged);
    }

    private void OnMapInit(Entity<MobStateComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.CurrentState == MobState.Alive && !HasComp<SleepingComponent>(ent))
            EnsureComp<AwakeMobComponent>(ent);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive && !HasComp<SleepingComponent>(args.Target))
            EnsureComp<AwakeMobComponent>(args.Target);
        else
            RemComp<AwakeMobComponent>(args.Target);
    }

    private void OnSleepStateChanged(Entity<SleepingComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
        {
            RemComp<AwakeMobComponent>(ent);
            return;
        }

        // the sleeping component is still present during its own removal, check the mob state instead
        if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState == MobState.Alive)
            EnsureComp<AwakeMobComponent>(ent);
    }
}
