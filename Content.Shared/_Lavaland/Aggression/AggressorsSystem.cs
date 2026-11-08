// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using Content.Shared._Lavaland.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Lavaland.Aggression;

public sealed class AggressorsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBossMusicSystem _bossMusic = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AggressiveComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<AggressiveComponent, DamageModifyEvent>(OnDamageModified);
        SubscribeLocalEvent<AggressiveComponent, EntityTerminatingEvent>(OnDeleted);
        SubscribeLocalEvent<AggressiveComponent, MobStateChangedEvent>(OnStateChange);

        SubscribeLocalEvent<AggressorComponent, MobStateChangedEvent>(OnAggressorStateChange);
        SubscribeLocalEvent<AggressorComponent, EntityTerminatingEvent>(OnAggressorDeleted);
        SubscribeLocalEvent<AggressorComponent, AggressiveAddedEvent>(OnAggressorAdded);
        SubscribeLocalEvent<AggressorComponent, AggressiveRemovedEvent>(OnAggressorRemoved);

        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // All who are aggressive check their aggressors, and remove them if they are too far away.
        var query = EntityQueryEnumerator<AggressiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var aggressive, out var xform))
        {
            if (aggressive.ForgiveRange == null
                || aggressive.NextUpdate > curTime)
                continue;

            aggressive.NextUpdate = curTime + aggressive.UpdateDelay;

            var toRemove = new List<EntityUid>();

            foreach (var aggressor in aggressive.Aggressors)
            {
                if (!_xformQuery.TryComp(aggressor, out var aggroXform))
                    continue;

                var aggroPos = _xform.GetWorldPosition(aggroXform);
                var aggressivePos = _xform.GetWorldPosition(xform);
                var distance = (aggressivePos - aggroPos).Length();

                if (distance > aggressive.ForgiveRange
                    || xform.MapID != aggroXform.MapID)
                    toRemove.Add(aggressor);
            }
            foreach (var remove in toRemove)
            {
                RemoveAggressor((uid, aggressive), remove);
            }
        }
    }

    #region Event Handling

    private void OnDamageChanged(Entity<AggressiveComponent> ent, ref DamageChangedEvent args)
    {
        if (!ent.Comp.AutoAddOnDamage)
            return;

        // A corpse is not a threat: shooting a dead megafauna must not restart its AI,
        // bring its boss music back or let the rejuvenate on shutdown revive it.
        if (_mobState.IsDead(ent.Owner))
            return;

        var aggro = args.Origin;

        if (aggro == null
            || !HasComp<ActorComponent>(aggro))
            return;

        AddAggressor(ent, aggro.Value);
    }

    // An armor pool can eat the whole hit, then no DamageChangedEvent is raised and the boss
    // never learns about the attacker. Hook the modify step, which runs for absorbed hits too.
    private void OnDamageModified(Entity<AggressiveComponent> ent, ref DamageModifyEvent args)
    {
        // OriginalDamage keeps the incoming hit even if another handler already absorbed it.
        if (!ent.Comp.AutoAddOnDamage
            || _mobState.IsDead(ent.Owner)
            || args.OriginalDamage.GetTotal() <= 0)
            return;

        if (args.Origin is not { } aggro
            || !HasComp<ActorComponent>(aggro)
            || ent.Comp.Aggressors.Contains(aggro))
            return;

        AddAggressor(ent, aggro);
    }

    private void OnDeleted(Entity<AggressiveComponent> ent, ref EntityTerminatingEvent args)
        => RemoveAllAggressors(ent);

    private void OnStateChange(Entity<AggressiveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RaiseLocalEvent(ent, new AggressiveKilledEvent(ent.Comp.Aggressors.ToList()));

        RemoveAllAggressors(ent);
    }

    private void OnAggressorAdded(Entity<AggressorComponent> ent, ref AggressiveAddedEvent args)
    {
        if (ent.Comp.Aggressives.TryFirstOrNull(out var boss))
            _bossMusic.StartBossMusic(boss.Value);
    }

    private void OnAggressorRemoved(Entity<AggressorComponent> ent, ref AggressiveRemovedEvent args)
        => _bossMusic.EndAllMusic(); // Stop the music if we are no longer get attacked by anyone.

    private void OnAggressorStateChange(Entity<AggressorComponent> ent, ref MobStateChangedEvent args)
    {
        if (_mobState.IsDead(ent.Owner))
            CleanAggressions((ent.Owner, ent.Comp));
    }

    private void OnAggressorDeleted(Entity<AggressorComponent> ent, ref EntityTerminatingEvent args)
        => CleanAggressions((ent.Owner, ent.Comp));

    #endregion

    #region Aggressive API

    public void AddAggressor(Entity<AggressiveComponent> ent, EntityUid aggressor)
    {
        var (uid, comp) = ent;
        ent.Comp.Aggressors.Add(aggressor);

        var aggComp = EnsureComp<AggressorComponent>(aggressor);
        aggComp.Aggressives.Add(uid);

        RaiseLocalEvent(uid, new AggressorAddedEvent(aggressor));
        RaiseLocalEvent(aggressor, new AggressiveAddedEvent(uid));

        Dirty(uid, comp);
        Dirty(aggressor, aggComp);
    }

    public void RemoveAggressor(Entity<AggressiveComponent> ent, Entity<AggressorComponent?> aggressor)
    {
        if (!Resolve(aggressor, ref aggressor.Comp))
            return;

        ent.Comp.Aggressors.Remove(aggressor);
        aggressor.Comp.Aggressives.Remove(ent);

        RaiseLocalEvent(ent.Owner, new AggressorRemovedEvent(aggressor.Owner));

        Dirty(ent.Owner, ent.Comp);

        if (aggressor.Comp.Aggressives.Count == 0)
        {
            RaiseLocalEvent(aggressor.Owner, new AggressiveRemovedEvent(ent.Owner));
            RemComp(aggressor, aggressor.Comp);
        }
        else
            Dirty(aggressor.Owner, aggressor.Comp);
    }

    public void RemoveAllAggressors(Entity<AggressiveComponent> ent)
    {
        foreach (var aggressor in ent.Comp.Aggressors)
        {
            if (!TryComp<AggressorComponent>(aggressor, out var aggressorComp))
                continue;

            aggressorComp.Aggressives.Remove(ent.Owner);
            if (aggressorComp.Aggressives.Count == 0)
            {
                RaiseLocalEvent(aggressor, new AggressiveRemovedEvent(ent.Owner));
                RemComp(aggressor, aggressorComp);
            }
        }

        ent.Comp.Aggressors.Clear();
    }

    #endregion

    #region Aggressor API

    public void CleanAggressions(Entity<AggressorComponent?> aggressor)
    {
        if (!Resolve(aggressor, ref aggressor.Comp))
            return;

        foreach (var aggressive in aggressor.Comp.Aggressives.ToArray())
        {
            if (TryComp<AggressiveComponent>(aggressive, out var aggressors))
                RemoveAggressor((aggressive, aggressors), aggressor);
        }

        // RemoveAggressor already removes the component when the last aggression is gone;
        // removing it again would log a spurious "already deleted" warning on every death.
        if (!aggressor.Comp.Deleted)
            RemComp(aggressor, aggressor.Comp);
    }

    #endregion
}
