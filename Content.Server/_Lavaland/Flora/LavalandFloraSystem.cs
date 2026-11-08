// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using Content.Goobstation.Common.Gatherable;
using Content.Server.Destructible;
using Content.Server.Gatherable.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Sprite;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Flora;

/// <summary>
/// TG ash flora behaviour: a picked plant leaves a picked stub behind and regrows after a
/// random delay, and cacti hurt whoever steps on them while they are still standing.
/// </summary>
public sealed class LavalandFloraSystem : EntitySystem
{
    /// <summary>
    /// Tint used for the picked stub when its prototype has no dedicated picked sprite state.
    /// </summary>
    private static readonly Color WitheredColor = Color.FromHex("#6b5a3e");

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandFloraRegrowComponent, GatheredEvent>(OnGathered);
        SubscribeLocalEvent<LavalandFloraCaltropComponent, StepTriggeredOffEvent>(OnSteppedOn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var ready = new List<(EntityUid Stub, string Prototype, EntityCoordinates Coordinates)>();

        var query = EntityQueryEnumerator<LavalandFloraRegrowComponent>();
        while (query.MoveNext(out var uid, out var regrow))
        {
            if (regrow.RegrowAt == default || now < regrow.RegrowAt)
                continue;

            if (MetaData(uid).EntityPrototype?.ID is not { } prototype)
                continue;

            ready.Add((uid, prototype, Transform(uid).Coordinates));
        }

        foreach (var (stub, prototype, coordinates) in ready)
        {
            Del(stub);
            Spawn(prototype, coordinates);
        }
    }

    private void OnGathered(Entity<LavalandFloraRegrowComponent> ent, ref GatheredEvent args)
    {
        if (MetaData(ent).EntityPrototype?.ID is not { } prototype)
            return;

        var coordinates = Transform(ent).Coordinates;
        var delay = _random.NextFloat(ent.Comp.MinDelay, ent.Comp.MaxDelay);

        var stub = Spawn(prototype, coordinates);
        RemComp<GatherableComponent>(stub);
        // TG leaves nothing behind after a harvest, so the stub cannot be broken to skip the regrow.
        RemComp<DestructibleComponent>(stub);
        ShowHarvestedSprite(stub, ent.Comp);

        // The stub itself is the regrow timer. If its map is gone, the stub is gone and nothing is left behind.
        EnsureComp<LavalandFloraRegrowComponent>(stub).RegrowAt = _timing.CurTime + TimeSpan.FromSeconds(delay);
    }

    private void ShowHarvestedSprite(EntityUid stub, LavalandFloraRegrowComponent regrow)
    {
        if (!TryComp<RandomSpriteComponent>(stub, out var randomSprite) || randomSprite.Selected.Count == 0)
            return;

        var color = regrow.HarvestedState == null ? WitheredColor : (Color?) null;
        foreach (var key in randomSprite.Selected.Keys.ToArray())
        {
            var state = regrow.HarvestedState ?? randomSprite.Selected[key].State;
            randomSprite.Selected[key] = (state, color);
        }

        Dirty(stub, randomSprite);
    }

    private void OnSteppedOn(Entity<LavalandFloraCaltropComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (!_random.Prob(ent.Comp.Probability))
            return;

        var damageType = _prototypes.Index<DamageTypePrototype>("Piercing");
        var damage = new DamageSpecifier(damageType, _random.Next(ent.Comp.MinDamage, ent.Comp.MaxDamage + 1));
        _damageable.TryChangeDamage(args.Tripper, damage, origin: ent.Owner);
    }
}

/// <summary>
/// Marks a gatherable TG ash flora plant that regrows after being picked.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandFloraRegrowComponent : Component
{
    /// <summary>
    /// Minimum regrow delay in seconds. TG uses three minutes at the low end.
    /// </summary>
    [DataField]
    public float MinDelay = 180f;

    /// <summary>
    /// Maximum regrow delay in seconds. TG uses twelve minutes at the high end.
    /// </summary>
    [DataField]
    public float MaxDelay = 720f;

    /// <summary>
    /// Sprite state used for the picked plant while it regrows. Falls back to a withered tint.
    /// </summary>
    [DataField]
    public string? HarvestedState;

    /// <summary>
    /// Runtime moment of the next regrow. Not saved: lavaland is procedural and the stub dies with its map.
    /// </summary>
    public TimeSpan RegrowAt;
}

/// <summary>
/// Marks a TG ash flora cactus that hurts whoever steps on it, both before and between harvests.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandFloraCaltropComponent : Component
{
    [DataField]
    public int MinDamage = 3;

    [DataField]
    public int MaxDamage = 6;

    [DataField]
    public float Probability = 0.7f;
}
