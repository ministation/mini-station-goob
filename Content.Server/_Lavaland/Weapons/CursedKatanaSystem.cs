// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Cursed katana combos. A swing that connects is R, a whiff is L; finishing
/// one of six sequences (LLR, RLL, LRR, RRL, LRLR, RLRL) triggers the move.
/// TG parity: datum/component/combo_attacks.
/// </summary>
public sealed class CursedKatanaSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly (string Name, bool[] Sequence)[] Moves =
    {
        ("katana-hilt-strike", new[] { false, false, true }),
        ("katana-wide-slice", new[] { true, false, false }),
        ("katana-dash-attack", new[] { false, true, true }),
        ("katana-tendon-cut", new[] { true, true, false }),
        ("katana-dark-cloak", new[] { false, true, false, true }),
        ("katana-shatter", new[] { true, false, true, false }),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursedKatanaComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<CursedKatanaComponent> ent, ref MeleeHitEvent args)
    {
        var katana = ent.Comp;

        // TG: L = a click on empty air, R = a click on a target.
        katana.Combo.Add(args.HitEntities.Count > 0);
        if (args.HitEntities.Count > 0)
            katana.LastVictim = args.HitEntities[0];
        while (katana.Combo.Count > 4)
            katana.Combo.RemoveAt(0);

        for (var i = Moves.Length - 1; i >= 0; i--)
        {
            var (name, sequence) = Moves[i];
            if (katana.Combo.Count < sequence.Length)
                continue;

            var tail = katana.Combo.GetRange(katana.Combo.Count - sequence.Length, sequence.Length);
            var matches = true;
            for (var s = 0; s < sequence.Length; s++)
            {
                if (tail[s] == sequence[s])
                    continue;
                matches = false;
                break;
            }

            if (!matches)
                continue;

            katana.Combo.Clear();
            PerformMove((ent, katana), name, args);
            return;
        }
    }

    private void PerformMove(Entity<CursedKatanaComponent> ent, string name, MeleeHitEvent args)
    {
        var katana = ent.Comp;
        var user = args.User;
        _popup.PopupClient(Loc.GetString(name), user, user);

        switch (name)
        {
            case "katana-hilt-strike":
                args.BonusDamage += katana.HiltStrikeDamage;
                if (args.HitEntities.Count > 0)
                {
                    var victim = args.HitEntities[0];
                    var victimCoords = Transform(victim).Coordinates;
                    var userPos = _transform.GetMapCoordinates(user).Position;
                    var victimPos = _transform.GetMapCoordinates(victim).Position;
                    var delta = victimPos - userPos;
                    delta = delta.LengthSquared() > 0.001f
                        ? Vector2.Normalize(delta)
                        : new Vector2(0f, 1f);
                    _throwing.TryThrow(victim, victimCoords.Offset(delta * katana.HiltStrikeThrowDistance), 3f);
                }
                break;

            case "katana-wide-slice":
                var userCoords = _transform.GetMapCoordinates(user);
                var hitAlready = new List<EntityUid>(args.HitEntities);
                foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(userCoords, katana.WideSliceRadius))
                {
                    if (other.Owner == user || hitAlready.Contains(other.Owner))
                        continue;                    if (!_mobState.IsAlive(other.Owner))
                        continue;

                    _damageable.TryChangeDamage(other.Owner, katana.WideSliceDamage, origin: user);
                }
                break;

            case "katana-dash-attack":
                args.BonusDamage += katana.DashAttackDamage;
                if (args.HitEntities.Count > 0)
                {
                    var victim = args.HitEntities[0];
                    var victimPos = _transform.GetMapCoordinates(victim);
                    var userPos = _transform.GetMapCoordinates(user);
                    var delta = victimPos.Position - userPos.Position;
                    delta = delta.LengthSquared() > 0.001f
                        ? Vector2.Normalize(delta)
                        : new Vector2(0f, 1f);
                    // TG: dash through and behind the victim, up to 8 tiles.
                    _transform.SetMapCoordinates(user, new MapCoordinates(victimPos.Position + delta, victimPos.MapId));
                }
                break;


            case "katana-tendon-cut":
                args.BonusDamage += katana.TendonCutDamage;
                break;

            case "katana-dark-cloak":
                // TG: fade into a dark cloak for a few seconds; hostiles lose their
                // target. The default stealth value is the TG alpha-150 translucency.
                EnsureComp<StealthComponent>(user);

                Timer.Spawn(katana.CloakDuration, () =>
                {
                    if (!Deleted(user) && !Terminating(user))
                        RemComp<StealthComponent>(user);
                });
                break;

            case "katana-shatter":
                // TG: the finisher ends on a whiff and slams the last connected
                // victim — mining fauna take the full shatter, everything else
                // less; the blade nullspaces for a while, then coagulates back.
                var shatterVictim = args.HitEntities.Count > 0 ? args.HitEntities[0] : katana.LastVictim;
                if (shatterVictim != null && Exists(shatterVictim.Value))
                {
                    var shatterDamage = HasComp<FaunaComponent>(shatterVictim.Value)
                        ? katana.ShatterFaunaDamage
                        : katana.ShatterDamage;
                    _damageable.TryChangeDamage(shatterVictim.Value, shatterDamage, origin: user);
                }

                Vanish(ent);
                break;
        }
    }

    private void Vanish(Entity<CursedKatanaComponent> ent)
    {
        var katana = ent.Comp;
        if (!TryComp<MetaDataComponent>(ent, out var meta) || meta.EntityPrototype?.ID is not { } protoId)
            return;

        var owner = Transform(ent).ParentUid;

        QueueDel(ent);

        // The blade nullspaces and coagulates back near its wielder.
        Timer.Spawn(katana.ShatterVanishDuration, () =>
        {
            if (!Exists(owner) || TerminatingOrDeleted(owner))
                return;

            Spawn(protoId, Transform(owner).Coordinates);
        });
    }
}
