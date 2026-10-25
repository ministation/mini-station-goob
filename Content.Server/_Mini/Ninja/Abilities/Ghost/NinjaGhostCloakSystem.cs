// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 2: the phase cloak. Toggles the stealth of the suit wearer, drains the suit power cell,
/// heats the suit up and forces the cloak off on overheat or when the ninja is revealed.
/// </summary>
public sealed class NinjaGhostCloakSystem : EntitySystem
{
    private const string CloakActionProto = "ActionNinjaGhostCloak";

    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Wearer currently cloaked by each suit. Suits without an entry are uncloaked.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _cloaked = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostCloakComponent, NinjaGhostCloakEvent>(OnToggle);
        SubscribeLocalEvent<NinjaGhostCloakComponent, NinjaSuitPowerChangedEvent>(OnSuitPowerChanged);
        SubscribeLocalEvent<NinjaGhostCloakComponent, NinjaSuitRevealedEvent>(OnRevealed);
        SubscribeLocalEvent<NinjaGhostCloakComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaGhostCloakComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggle(Entity<NinjaGhostCloakComponent> ent, ref NinjaGhostCloakEvent args)
    {
        if (args.Handled)
            return;

        var suit = ent.Owner;
        var user = args.Performer;

        if (Transform(suit).ParentUid != user)
            return;

        if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            return;

        args.Handled = true;

        if (_cloaked.ContainsKey(suit))
        {
            SetCloaked(ent, user, false);
            return;
        }

        if (ent.Comp.Heat >= ent.Comp.MaxHeat)
        {
            _popup.PopupEntity(Loc.GetString("ninja-ghost-cloak-overheated"), user, user, PopupType.MediumCaution);
            return;
        }

        SetCloaked(ent, user, true);
    }

    private void OnSuitPowerChanged(Entity<NinjaGhostCloakComponent> ent, ref NinjaSuitPowerChangedEvent args)
    {
        if (!args.Activated)
            SetCloaked(ent, args.Wearer, false);
    }

    private void OnRevealed(Entity<NinjaGhostCloakComponent> ent, ref NinjaSuitRevealedEvent args)
    {
        if (!SetCloaked(ent, args.Wearer, false))
            return;

        // Being revealed also puts the cloak action on cooldown.
        if (FindCloakAction(ent.Owner) is { } action)
            _actions.StartUseDelay(action);
    }

    private void OnUnequipped(Entity<NinjaGhostCloakComponent> ent, ref GotUnequippedEvent args)
    {
        if (_cloaked.TryGetValue(ent.Owner, out var wearer))
            SetCloaked(ent, wearer, false);
    }

    private void OnShutdown(Entity<NinjaGhostCloakComponent> ent, ref ComponentShutdown args)
    {
        if (_cloaked.TryGetValue(ent.Owner, out var wearer))
            SetCloaked(ent, wearer, false);
    }

    private bool SetCloaked(Entity<NinjaGhostCloakComponent> ent, EntityUid wearer, bool cloaked)
    {
        var suit = ent.Owner;

        if (!cloaked)
        {
            if (!_cloaked.Remove(suit))
                return false;

            if (wearer.IsValid())
                RemoveStealth(wearer);

            return true;
        }

        if (!wearer.IsValid() || _cloaked.ContainsKey(suit))
            return false;

        _cloaked[suit] = wearer;
        ApplyStealth(ent.Comp, wearer);
        return true;
    }

    private void ApplyStealth(NinjaGhostCloakComponent comp, EntityUid wearer)
    {
        if (comp.CloakComponents.Count > 0)
            EntityManager.AddComponents(wearer, comp.CloakComponents);

        if (TryComp<StealthComponent>(wearer, out var stealth))
            _stealth.SetVisibility(wearer, 1f, stealth);
    }

    private void RemoveStealth(EntityUid wearer)
    {
        if (!TerminatingOrDeleted(wearer))
        {
            RemComp<StealthOnMoveComponent>(wearer);
            RemComp<StealthComponent>(wearer);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaGhostCloakComponent>();
        while (query.MoveNext(out var suit, out var comp))
        {
            var isCloaked = _cloaked.TryGetValue(suit, out var wearer);

            if (!isCloaked)
            {
                CoolDown(comp, frameTime);
                continue;
            }

            if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated || !wearer.IsValid())
            {
                SetCloaked((suit, comp), wearer, false);
                CoolDown(comp, frameTime);
                continue;
            }

            // Drain the suit cell every tick.
            var cost = comp.DrainRate * frameTime;
            if (!_suitPower.HasCharge(wearer, cost))
            {
                SetCloaked((suit, comp), wearer, false);
                if (FindCloakAction(suit) is { } action)
                    _actions.StartUseDelay(action);

                continue;
            }

            _suitPower.TryUseCharge(wearer, cost);

            var oldHeat = comp.Heat;
            comp.Heat = MathF.Min(comp.MaxHeat, comp.Heat + comp.HeatRate * frameTime);

            if (oldHeat < comp.EffectsThreshold && comp.Heat >= comp.EffectsThreshold)
                _popup.PopupEntity(Loc.GetString("ninja-ghost-cloak-heat-warning"), wearer, wearer, PopupType.MediumCaution);

            if (oldHeat < comp.DangerThreshold && comp.Heat >= comp.DangerThreshold)
                _popup.PopupEntity(Loc.GetString("ninja-ghost-cloak-heat-danger"), wearer, wearer, PopupType.MediumCaution);

            // Sparks scale with the accumulated heat.
            if (comp.Heat >= comp.EffectsThreshold && comp.MaxHeat > comp.EffectsThreshold)
            {
                var chance = (comp.Heat - comp.EffectsThreshold) / (comp.MaxHeat - comp.EffectsThreshold) * frameTime * 0.1f;
                if (_random.Prob(chance))
                    Spawn(comp.EffectPrototype, Transform(wearer).Coordinates);
            }

            if (comp.Heat < comp.MaxHeat)
                continue;

            // Overheated: the cloak turns off and the suit slowly cools down.
            SetCloaked((suit, comp), wearer, false);
            _popup.PopupEntity(Loc.GetString("ninja-ghost-cloak-overheated"), wearer, wearer, PopupType.MediumCaution);
        }
    }

    private void CoolDown(NinjaGhostCloakComponent comp, float frameTime)
    {
        if (comp.Heat <= 0f)
            return;

        comp.Heat = MathF.Max(0f, comp.Heat - comp.CoolRate * frameTime);
    }

    private EntityUid? FindCloakAction(EntityUid suit)
    {
        if (!TryComp<ActionsContainerComponent>(suit, out var container))
            return null;

        foreach (var action in container.Container.ContainedEntities)
        {
            if (MetaData(action).EntityPrototype?.ID == CloakActionProto)
                return action;
        }

        return null;
    }
}