// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Item;
using Content.Shared.Kitchen.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Weight makes a fish slow you down and hit harder, size decides the item size and
/// edible fish can be ground in the kitchen grinder, all like in tg.
/// </summary>
public sealed class FishPhysicalSystem : EntitySystem
{
    // tg FISH_WEIGHT_SLOWDOWN.
    private const float SlowdownThreshold = 2100f;
    // tg FISH_WEIGHT_SLOWDOWN_DIVISOR and FISH_WEIGHT_SLOWDOWN_EXPONENT.
    private const float SlowdownDivisor = 500f;
    private const float SlowdownExponent = 0.54f;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishInitializedEvent>(OnFishInitialized);
    }

    private void OnFishInitialized(FishInitializedEvent args)
    {
        if (!_net.IsServer || !TryComp<FishComponent>(args.Fish, out var fish))
            return;

        FishPrototype? proto = null;
        if (fish.Fish is { } protoId)
            _proto.TryIndex(protoId, out proto);

        UpdateItemSize(args.Fish, fish);
        UpdateSlowdown(args.Fish, fish);
        UpdateExtractable(args.Fish, proto);
    }

    private void UpdateItemSize(EntityUid uid, FishComponent fish)
    {
        var size = fish.Size switch
        {
            < 30f => "Tiny",
            < 50f => "Small",
            < 80f => "Normal",
            < 120f => "Large",
            < 165f => "Huge",
            _ => "Ginormous",
        };

        _item.SetSize(uid, size);
    }

    private void UpdateSlowdown(EntityUid uid, FishComponent fish)
    {
        if (fish.Weight < SlowdownThreshold)
            return;

        // tg GET_FISH_SLOWDOWN returns percent, so a tenth of it becomes the speed multiplier.
        var slowdown = MathF.Round(MathF.Pow(fish.Weight / SlowdownDivisor, SlowdownExponent) - 1.3f, 1);
        var modifier = Math.Clamp(1f - slowdown / 100f, 0.1f, 1f);

        var held = EnsureComp<HeldSpeedModifierComponent>(uid);
        held.MirrorClothingModifier = false;
        held.WalkModifier = modifier;
        held.SprintModifier = modifier;
        Dirty(uid, held);
    }

    private void UpdateExtractable(EntityUid uid, FishPrototype? proto)
    {
        if (proto is { Edible: false })
        {
            RemComp<ExtractableComponent>(uid);
            return;
        }

        var extractable = EnsureComp<ExtractableComponent>(uid);
        extractable.GrindableSolution = "food";
        Dirty(uid, extractable);
    }
}
