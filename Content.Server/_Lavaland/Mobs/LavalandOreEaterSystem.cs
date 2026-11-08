// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG goldgrub: eats ore lying around, heals for the rarer metals and spits the
/// consumed ore back out when it dies.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandOreEaterComponent : Component
{
    /// <summary>
    /// How close the ore has to be to be eaten.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Seconds between meals.
    /// </summary>
    [DataField]
    public float Interval = 1f;

    /// <summary>
    /// Health restored per ore tag. Rare ores heal more, as on TG.
    /// </summary>
    [DataField]
    public Dictionary<string, float> HealByTag = new()
    {
        { "OreGold", 5f },
        { "OreUranium", 5f },
        { "OreDiamond", 15f },
        { "OreBananium", 30f },
        { "OreBluespace", 20f },
    };

    /// <summary>
    /// Health restored for ore that is not in <see cref="HealByTag"/>.
    /// </summary>
    [DataField]
    public float DefaultHeal;

    [ViewVariables]
    public float Accumulator;

    /// <summary>
    /// Prototypes of the ore stacks it has eaten, spat back out on death.
    /// </summary>
    [ViewVariables]
    public List<EntProtoId> Eaten = new();
}

public sealed class LavalandOreEaterSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandOreEaterComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandOreEaterComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var eater, out var damageable))
        {
            eater.Accumulator += frameTime;
            if (eater.Accumulator < eater.Interval)
                continue;

            eater.Accumulator -= eater.Interval;

            var coords = _transform.GetMapCoordinates(uid);
            foreach (var ore in _lookup.GetEntitiesInRange<TagComponent>(coords, eater.Range))
            {
                if (!_tags.HasTag(ore.Owner, OreTag))
                    continue;

                // Ore in a backpack or pocket reports the wearer's position; only eat what's on the ground.
                if (!HasComp<MapGridComponent>(Transform(ore.Owner).ParentUid))
                    continue;

                var heal = eater.DefaultHeal;
                foreach (var tag in ore.Comp.Tags)
                {
                    if (eater.HealByTag.TryGetValue(tag.Id, out var value))
                    {
                        heal = value;
                        break;
                    }
                }

                if (TryComp<StackComponent>(ore.Owner, out var stack) && stack.Count > 1)
                {
                    _stack.SetCount(ore.Owner, stack.Count - 1, stack);
                }
                else
                {
                    eater.Eaten.Add(MetaData(ore.Owner).EntityPrototype!.ID);
                    QueueDel(ore.Owner);
                }

                Heal(uid, damageable, heal);
                break;
            }
        }
    }

    private void OnMobStateChanged(Entity<LavalandOreEaterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Eaten.Count == 0)
            return;

        var coords = _transform.GetMapCoordinates(ent);
        foreach (var ore in ent.Comp.Eaten)
            Spawn(ore, coords);

        ent.Comp.Eaten.Clear();
    }

    private void Heal(EntityUid uid, DamageableComponent damageable, float amount)
    {
        if (amount <= 0)
            return;

        var spec = new DamageSpecifier();
        var remaining = amount;
        foreach (var (type, value) in damageable.Damage.DamageDict)
        {
            if (value <= FixedPoint2.Zero || remaining <= 0f)
                continue;

            var healed = Math.Min(value.Float(), remaining);
            spec.DamageDict[type] = -healed;
            remaining -= healed;
        }

        if (spec.DamageDict.Count > 0)
            _damageable.TryChangeDamage(uid, spec, true);
    }
}
