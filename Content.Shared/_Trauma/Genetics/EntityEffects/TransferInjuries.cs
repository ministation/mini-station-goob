// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._Trauma.Genetics.EntityEffects;

public sealed partial class TransferInjuries : EntityEffectBase<TransferInjuries>
{
    [DataField]
    public float Fraction = 0.25f;

    [DataField]
    public FixedPoint2 MaxPerType = 10;

    [DataField]
    public List<ProtoId<DamageGroupPrototype>> Groups = new()
    {
        "Brute",
        "Burn",
    };

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-transfer-injuries", ("chance", Probability), ("percent", Fraction * 100));
}

public sealed partial class TransferInjuriesEffectSystem : EntityEffectSystem<DamageableComponent, TransferInjuries>
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void Effect(Entity<DamageableComponent> ent, ref EntityEffectEvent<TransferInjuries> args)
    {
        if (args.User is not { } healer || healer == ent.Owner)
            return;

        if (!HasComp<DamageableComponent>(healer))
            return;

        var transfer = new DamageSpecifier();
        var fraction = args.Effect.Fraction * args.Scale;
        var maxPerType = args.Effect.MaxPerType * args.Scale;

        foreach (var group in args.Effect.Groups)
        {
            foreach (var type in _proto.Index(group).DamageTypes)
            {
                if (!ent.Comp.Damage.DamageDict.TryGetValue(type.Id, out var current) || current <= FixedPoint2.Zero)
                    continue;

                var amount = FixedPoint2.Min(current * fraction, maxPerType);
                if (amount <= FixedPoint2.Zero)
                    continue;

                transfer.DamageDict[type.Id] = amount;
            }
        }

        if (transfer.Empty)
        {
            _popup.PopupClient(Loc.GetString("MutationMendingTouch-popup-none", ("target", ent.Owner)), ent, healer);
            return;
        }

        _damageable.TryChangeDamage(ent.Owner, -transfer, ignoreResistances: true, interruptsDoAfters: false, ignoreBlockers: true);
        _damageable.TryChangeDamage(healer, transfer, ignoreResistances: true, interruptsDoAfters: false, ignoreBlockers: true, origin: healer);
        _popup.PopupClient(Loc.GetString("MutationMendingTouch-popup-done", ("target", ent.Owner)), ent, healer);
    }
}
