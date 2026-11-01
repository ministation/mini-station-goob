// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Server.Cargo.Components;
using Content.Server._Mini.Fishing.Traits;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Weight makes a fish hit harder and bigger fish sell for more, following tg
/// get_force_rank and get_export_price.
/// </summary>
public sealed class FishForceSystem : EntitySystem
{
    // tg FISH_WEIGHT_FORCE_DIVISOR and the base force of a fish.
    private const float ForceDivisor = 250f;
    private const float BaseForce = 6f;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    private const double PriceMultiplier = 0.01;
    private const double CurveExponent = 0.85;
    private const double SoftCapThreshold = 6000;
    private const double SoftCapExponent = 0.86;

    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Trait components may add their own melee damage, like the stinger fish piercing.
        SubscribeLocalEvent<FishInitializedEvent>(OnFishInitialized, after: new[] { typeof(FishTraitSystem) });
    }

    private void OnFishInitialized(FishInitializedEvent args)
    {
        if (!TryComp<FishComponent>(args.Fish, out var fish))
            return;

        UpdateForce(args.Fish, fish);
        UpdatePrice(args.Fish, fish);
    }

    private void UpdateForce(EntityUid uid, FishComponent fish)
    {
        // tg GET_FISH_WEIGHT_RANK.
        var weightRank = Math.Max(1, (int) MathF.Round(1f + MathF.Log2(MathF.Max(fish.Weight / ForceDivisor, 1f))));

        // tg get_force_rank and calculate_fish_force_bonus, with the tg weight class as the rank.
        var (weightClass, bonus) = fish.Size switch
        {
            < 30f => (1, -3f),
            < 50f => (2, 0f),
            < 80f => (3, 2f),
            < 120f => (4, 5f),
            < 165f => (5, 9f),
            _ => (6, 13f),
        };

        var malus = weightRank - weightClass;
        var force = MathF.Max(0f, MathF.Round((BaseForce + bonus) * (1f + malus * 0.1f), 1));

        var melee = EnsureComp<MeleeWeaponComponent>(uid);
        melee.Damage ??= new DamageSpecifier();
        melee.Damage.DamageDict[Blunt] = force;
        Dirty(uid, melee);
    }

    private void UpdatePrice(EntityUid uid, FishComponent fish)
    {
        if (!TryComp<StaticPriceComponent>(uid, out var price))
            return;

        var raw = price.Price + Math.Pow(fish.Size * fish.Weight * PriceMultiplier, CurveExponent);

        // Stop huge fish from being a money printer.
        if (raw >= SoftCapThreshold + 1)
            raw = SoftCapThreshold + Math.Pow(raw - SoftCapThreshold, SoftCapExponent);

        // StaticPrice is server only, so no Dirty here.
        price.Price = raw;
    }
}
