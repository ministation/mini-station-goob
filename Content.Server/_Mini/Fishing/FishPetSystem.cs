// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Traits;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Fishing fish can be petted like in tg: a heart, a happiness bonus, and predators may bite your finger.
/// Also makes a live fish slip out of the hands in pain when bitten.
/// </summary>
public sealed class FishPetSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedFishTraitSystem _trait = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // InteractionVerb on fish is already taken by the fish analyzer, so petting is an alternative verb.
        SubscribeLocalEvent<FishComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<IngestedEvent>(OnIngested);
    }

    private void OnGetVerbs(Entity<FishComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using != ent.Owner)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fish-pet-verb"),
            Act = () => Pet(ent, user),
        });
    }

    private void Pet(Entity<FishComponent> ent, EntityUid user)
    {
        var now = _timing.CurTime;
        if (ent.Comp.LastPetted is { } last && now - last < TimeSpan.FromSeconds(30))
        {
            _popup.PopupEntity(Loc.GetString("fish-pet-squirm", ("fish", ent.Owner)), ent, user);
            return;
        }

        ent.Comp.LastPetted = now;
        Dirty(ent, ent.Comp);

        Spawn("EffectHearts", Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("fish-pet", ("fish", ent.Owner)), ent, user);

        // shortcut: tg only bites for strong electrogenesis fish, this fork does not model the power.
        if (_trait.HasTrait(ent.Comp, FishTraitFlags.Predator) && _random.Prob(0.5f))
        {
            _popup.PopupEntity(Loc.GetString("fish-pet-bite", ("fish", ent.Owner)), ent, user);
            _damage.TryChangeDamage(user, new DamageSpecifier(_proto.Index(Blunt), 5), origin: ent.Owner);
        }
    }

    private void OnIngested(ref IngestedEvent args)
    {
        if (!TryComp<FishComponent>(args.Target, out var fish) || !fish.Alive)
            return;

        if (!_hands.IsHolding(args.User, args.Target) || !_random.Prob(0.5f))
            return;

        // shortcut: tg also damages the fish item, items here have no integrity to lose.
        _hands.TryDrop(args.User, args.Target);
        _popup.PopupEntity(Loc.GetString("fish-eat-flinch", ("fish", args.Target)), args.Target, args.User);
    }
}
