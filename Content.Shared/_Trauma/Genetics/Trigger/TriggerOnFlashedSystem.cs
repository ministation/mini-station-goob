// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Flash;
using Content.Shared.Random.Helpers;
using Content.Shared.Trigger.Systems;
using Content.Shared._Trauma.Genetics.Trigger;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Trauma.Genetics.Trigger.Triggers;

public sealed partial class TriggerOnFlashedSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnFlashedComponent, AfterFlashedEvent>(OnFlashed);
    }

    private void OnFlashed(Entity<TriggerOnFlashedComponent> ent, ref AfterFlashedEvent args)
    {
        if (_random.Prob(ent.Comp.Prob))
            _trigger.Trigger(ent, args.User, ent.Comp.KeyOut);
    }
}
