// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity for the regenerator component: brute damage heals back after
/// <see cref="RegeneratorComponent.RegenerationDelay"/> seconds without new damage.
/// </summary>
public sealed class RegeneratorSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RegeneratorComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<RegeneratorComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta || delta.GetTotal() <= 0)
            return;

        ent.Comp.LastDamageAt = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var group = _proto.Index(BruteGroup);

        var query = EntityQueryEnumerator<RegeneratorComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable))
        {
            if (damageable.TotalDamage <= 0 || curTime - comp.LastDamageAt < comp.RegenerationDelay)
                continue;

            var heal = new DamageSpecifier(group, -comp.BrutePerSecond * frameTime);
            _damageable.TryChangeDamage((uid, damageable), heal, ignoreResistances: true);
        }
    }
}
