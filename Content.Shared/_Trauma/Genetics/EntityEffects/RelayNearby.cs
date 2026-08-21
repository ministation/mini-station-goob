using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;

namespace Content.Shared._Trauma.Genetics.EntityEffects;

public sealed partial class RelayNearby : EntityEffectBase<RelayNearby>
{
    [DataField]
    public EntityEffect Effect = default!;

    [DataField(required: true)]
    public string CompName = string.Empty;

    internal Type? Comp;

    [DataField]
    public float Range = 5f;

    [DataField]
    public LookupFlags Flags = LookupFlags.All;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Effect.EntityEffectGuidebookText(prototype, entSys);
}

public sealed partial class RelayNearbyEffectSystem : EntityEffectSystem<TransformComponent, RelayNearby>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _found = new();

    protected override void Effect(Entity<TransformComponent> ent, ref EntityEffectEvent<RelayNearby> args)
    {
        var effect = args.Effect;
        if (effect.Comp == null)
        {
            var reg = Factory.GetRegistration(effect.CompName);
            effect.Comp = reg.Type;
        }

        var coords = _transform.GetMapCoordinates(ent, ent.Comp);
        _found.Clear();
        _lookup.GetEntitiesInRange(coords.MapId, coords.Position, effect.Range, _found, effect.Flags);
        foreach (var uid in _found)
        {
            if (uid == ent.Owner)
                continue;

            if (!EntityManager.HasComponent(uid, effect.Comp))
                continue;

            if (!_whitelist.CheckBoth(uid, blacklist: effect.Blacklist, whitelist: effect.Whitelist))
                continue;

            _effects.TryApplyEffect(uid, effect.Effect, args.Scale, args.User);
        }
    }
}
