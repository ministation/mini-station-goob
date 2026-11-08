// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Administration.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
///     TG megafauna legion infest: a legion that melee-hits an unconscious human
///     kills and swallows the body into a lesser legion; the shell is released when
///     that legion dies, fresh and revivable.
/// </summary>
public sealed class LegionInfestSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public const string ShellContainerId = "LegionShell";

    // Leaving a container makes Shitmed re-evaluate body integrity: a fresh corpse would
    // stand back up into critical. Force it dead for a few ticks after the release.
    private readonly List<EntityUid> _pendingShells = new();
    private readonly Dictionary<EntityUid, int> _shellTicksLeft = new();
    private const int ShellForceTicks = 3;

    public override void Update(float frameTime)
    {
        if (_pendingShells.Count == 0)
            return;

        var finished = new List<EntityUid>();
        foreach (var body in _pendingShells)
        {
            if (TerminatingOrDeleted(body))
            {
                finished.Add(body);
                continue;
            }

            if (!_mobState.IsDead(body))
                _mobState.ChangeMobState(body, MobState.Dead);

            var left = _shellTicksLeft.TryGetValue(body, out var ticks) ? ticks : 1;
            if (left <= 1)
                finished.Add(body);
            else
                _shellTicksLeft[body] = left - 1;
        }

        foreach (var body in finished)
        {
            _pendingShells.Remove(body);
            _shellTicksLeft.Remove(body);
        }
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<LegionInfestComponent, MeleeHitEvent>(OnMelee);
        SubscribeLocalEvent<LegionShellComponent, MobStateChangedEvent>(OnShellDeath);
        SubscribeLocalEvent<LegionShellComponent, EntityTerminatingEvent>(OnShellDeleted);
    }

    private void OnMelee(Entity<LegionInfestComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            // TG: only unconscious (but alive) humans are infested.
            if (!HasComp<HumanoidAppearanceComponent>(hit)
                || !TryComp<MobStateComponent>(hit, out var state)
                || state.CurrentState != MobState.Critical)
            {
                continue;
            }

            Infest(ent.Owner, hit);
        }
    }

    private void Infest(EntityUid legion, EntityUid victim)
    {
        _mobState.ChangeMobState(victim, MobState.Dead);

        var shell = EnsureComp<LegionShellComponent>(legion);
        var container = _container.EnsureContainer<ContainerSlot>(legion, ShellContainerId);
        if (!_container.Insert(victim, container))
            return;

        shell.StoredBody = victim;
        _popup.PopupEntity(Loc.GetString("legion-infest", ("legion", legion), ("victim", victim)), legion, PopupType.MediumCaution);
    }

    private void OnShellDeath(Entity<LegionShellComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ReleaseShell(ent);
    }

    private void OnShellDeleted(Entity<LegionShellComponent> ent, ref EntityTerminatingEvent args)
        => ReleaseShell(ent);

    /// <summary>
    ///     Drops the stored body where the shell legion died (TG releases the consumed
    ///     mob on death, fresh enough to be revived).
    /// </summary>
    private void ReleaseShell(Entity<LegionShellComponent> ent)
    {
        if (!_container.TryGetContainer(ent.Owner, ShellContainerId, out var container))
            return;

        // The body is released as it died: dead but far below the death threshold,
        // so it stays defib-able (TG fully heals it; healing here fights the Shitmed
        // woundable regeneration, which keeps reviving a zero-damage corpse).
        _container.EmptyContainer(container);
        if (ent.Comp.StoredBody is { } body && !TerminatingOrDeleted(body))
        {
            _pendingShells.Add(body);
            _shellTicksLeft[body] = ShellForceTicks;
        }

        RemCompDeferred<LegionShellComponent>(ent.Owner);
    }
}
