// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared._Mini.Fishing.Traits;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Fishing.Traits;

/// <summary>
/// Runs the fish trait effects that live on the fish item itself: stinger component, venom, ink and self revival.
/// </summary>
public sealed partial class FishTraitSystem : EntitySystem
{
    /// <summary>
    /// tg TRAIT_FISH_INK_ON_COOLDOWN, how long an ink fish needs to refill its sac.
    /// </summary>
    private static readonly TimeSpan InkCooldown = TimeSpan.FromSeconds(9);

    /// <summary>
    /// tg adjust_temp_blindness_up_to(4 SECONDS, 8 SECONDS).
    /// </summary>
    private static readonly TimeSpan InkBlindness = TimeSpan.FromSeconds(4);

    private const float ReviveMinDelay = 60f;
    private const float ReviveMaxDelay = 120f;

    [Dependency] private SharedFishTraitSystem _trait = default!;
    [Dependency] private FishSystem _fish = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// When a dead fish with the Revival trait comes back to life.
    /// </summary>
    internal readonly Dictionary<EntityUid, TimeSpan> ReviveAt = new();

    /// <summary>
    /// When an ink fish may splat someone again.
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _inkAt = new();

    private float _timer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishInitializedEvent>(OnFishInitialized);
        SubscribeLocalEvent<FishComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FishComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnFishInitialized(FishInitializedEvent args)
    {
        if (!TryComp<FishComponent>(args.Fish, out var fish))
            return;

        foreach (var id in _fish.GetTraits(fish))
        {
            if (!_proto.TryIndex(id, out FishTraitPrototype? trait) || trait.Components == null)
                continue;

            // Existing components win, so an ink fish does not overwrite the stinger damage and vice versa.
            EntityManager.AddComponents(args.Fish, trait.Components, removeExisting: false);
        }
    }

    private void OnShutdown(Entity<FishComponent> ent, ref ComponentShutdown args)
    {
        ReviveAt.Remove(ent.Owner);
        _inkAt.Remove(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        _timer += frameTime;
        if (_timer < 1f)
            return;

        _timer -= 1f;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FishComponent>();
        while (query.MoveNext(out var uid, out var fish))
        {
            UpdateElectrified(uid, fish);

            if (fish.Alive)
            {
                ReviveAt.Remove(uid);
                continue;
            }

            if (!_trait.HasTrait(fish, FishTraitFlags.Revival))
                continue;

            if (!ReviveAt.TryGetValue(uid, out var reviveAt))
            {
                ReviveAt[uid] = now + TimeSpan.FromSeconds(_random.NextFloat(ReviveMinDelay, ReviveMaxDelay));
                continue;
            }

            if (now < reviveAt)
                continue;

            ReviveAt.Remove(uid);
            Revive((uid, fish));
        }
    }

    /// <summary>
    /// An electrogenesis fish only shocks while it is alive.
    /// </summary>
    private void UpdateElectrified(EntityUid uid, FishComponent fish)
    {
        if (!TryComp<ElectrifiedComponent>(uid, out var electrified) || electrified.Enabled == fish.Alive)
            return;

        electrified.Enabled = fish.Alive;
        Dirty(uid, electrified);
    }

    /// <summary>
    /// Brings a dead fish back to life and tells everyone around that it moved.
    /// </summary>
    public void Revive(Entity<FishComponent> ent)
    {
        _fish.SetAlive(ent, true);
        _popup.PopupEntity(Loc.GetString("fish-trait-revival-revived", ("fish", ent.Owner)), ent.Owner,
            PopupType.Medium);
    }

    private void OnMeleeHit(Entity<FishComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var inked = Ink(ent, args);
        if (inked || !_trait.HasTrait(ent.Comp, FishTraitFlags.Stinger))
            return;

        // tg only puts the venom on fish with a stinger, all other toxic fish are only poisonous to eat.
        var flags = FishTraitFlags.Toxic | FishTraitFlags.ToxicBarbs | FishTraitFlags.Hallucinogenic;
        if (_trait.GetTrait(ent.Comp, flags) is not { Venom: { } venom } venomTrait)
            return;

        var dose = SharedFishTraitSystem.GetVenomDose(venomTrait, ent.Comp.Weight, ent.Comp.Alive);
        foreach (var target in args.HitEntities)
            InjectVenom(target, venom, dose);
    }

    /// <summary>
    /// Splats ink into the face of whoever the fish hits. Mirrors tg attacked_someone.
    /// </summary>
    private bool Ink(Entity<FishComponent> ent, MeleeHitEvent args)
    {
        if (!ent.Comp.Alive || !_trait.HasTrait(ent.Comp, FishTraitFlags.Ink))
            return false;

        var now = _timing.CurTime;
        if (_inkAt.TryGetValue(ent.Owner, out var at) && now < at)
            return false;

        _inkAt[ent.Owner] = now + InkCooldown;

        foreach (var target in args.HitEntities)
        {
            if (!_status.TryAddStatusEffect(target, TemporaryBlindnessSystem.BlindingStatusEffect, InkBlindness,
                    false, TemporaryBlindnessSystem.BlindingStatusEffect))
                continue;

            _popup.PopupEntity(Loc.GetString("fish-trait-ink-splat", ("fish", ent.Owner)), target,
                PopupType.MediumCaution);
        }

        return true;
    }

    private void InjectVenom(EntityUid target, ProtoId<ReagentPrototype> reagent, float amount)
    {
        if (amount <= 0f || !_solutions.TryGetInjectableSolution(target, out var solutionEnt, out _))
            return;

        var venom = new Solution();
        venom.AddReagent(reagent, FixedPoint2.New(amount));
        _reactive.DoEntityReaction(target, venom, ReactionMethod.Injection);
        _solutions.TryAddSolution(solutionEnt.Value, venom);
    }
}
