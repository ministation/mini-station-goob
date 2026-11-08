// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Necropolis;

/// <summary>
/// TG elite tumor flow. The tumor is woken by hand, spawns one of the four elites
/// after a delay and seals the arena with forcefield walls. The tumor is
/// indestructible and only reacts to the fight outcome.
/// </summary>
public sealed class EliteTumorSystem : EntitySystem
{
    private static readonly EntProtoId GhostRoleTemplate = "LavalandEliteTumorGhostRole";
    private static readonly ProtoId<EntityTablePrototype> TendrilLootTable = "LavalandLootTendril";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EliteTumorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EliteTumorComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<EliteTumorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<EliteTumorTrackerComponent, MobStateChangedEvent>(OnEliteStateChanged);
        SubscribeLocalEvent<EliteTumorActivatorComponent, MobStateChangedEvent>(OnActivatorStateChanged);
    }

    private void OnMapInit(Entity<EliteTumorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.State = EliteTumorState.Inactive;
        _appearance.SetData(ent, EliteTumorVisuals.State, EliteTumorVisualState.Dormant);
    }

    private void OnInteractHand(Entity<EliteTumorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || HasComp<GhostComponent>(args.User))
            return;

        switch (ent.Comp.State)
        {
            case EliteTumorState.Inactive:
                args.Handled = true;
                StartFight(ent, args.User);
                break;
            case EliteTumorState.Passive:
                args.Handled = true;
                Rematch(ent, args.User);
                break;
        }
    }

    private void OnInteractUsing(Entity<EliteTumorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.State != EliteTumorState.Inactive || ent.Comp.Boosted)
            return;

        var usedId = MetaData(args.Used).EntityPrototype?.ID;
        if (usedId != ent.Comp.BoostItem.Id)
            return;

        args.Handled = true;
        // Consume one skull, the whole stack should not vanish.
        if (!_stack.TryUse(args.Used, 1))
            QueueDel(args.Used);
        ent.Comp.Boosted = true;
        _popup.PopupEntity(Loc.GetString("elite-tumor-boosted"), args.User, args.User);
    }

    private void StartFight(Entity<EliteTumorComponent> ent, EntityUid user)
    {
        ent.Comp.State = EliteTumorState.Active;
        ent.Comp.Activator = user;
        ent.Comp.SpawnEliteProto = _random.Pick(ent.Comp.Elites);
        ent.Comp.SpawnAt = _timing.CurTime + ent.Comp.SpawnDelay;

        EnsureComp<EliteTumorActivatorComponent>(user).Tumor = ent.Owner;
        _appearance.SetData(ent, EliteTumorVisuals.State, EliteTumorVisualState.Advanced);
        BuildArena(ent);
        _popup.PopupEntity(Loc.GetString("elite-tumor-awakened"), ent, user);
    }

    private void Rematch(Entity<EliteTumorComponent> ent, EntityUid user)
    {
        ent.Comp.State = EliteTumorState.Active;
        ent.Comp.Activator = user;
        EnsureComp<EliteTumorActivatorComponent>(user).Tumor = ent.Owner;
        _appearance.SetData(ent, EliteTumorVisuals.State, EliteTumorVisualState.Advanced);
        BuildArena(ent);

        if (ent.Comp.SpawnedElite is { } elite && !TerminatingOrDeleted(elite))
        {
            _xform.SetWorldPosition(elite, Transform(ent).WorldPosition);
            _damageable.ClearAllDamage(elite);
            // Restore the health the elite lost after its boosted victory.
            if (ent.Comp.BaseDeadThreshold is { } baseThreshold)
                _threshold.SetMobStateThreshold(elite, baseThreshold, MobState.Dead);
            ent.Comp.SpawnAt = _timing.CurTime;
        }
        else
        {
            ent.Comp.SpawnEliteProto = _random.Pick(ent.Comp.Elites);
            ent.Comp.SpawnAt = _timing.CurTime + ent.Comp.SpawnDelay;
        }

        _popup.PopupEntity(Loc.GetString("elite-tumor-awakened"), ent, user);
    }

    private void SpawnElite(Entity<EliteTumorComponent> ent)
    {
        if (ent.Comp.SpawnEliteProto is not { } eliteProto)
            return;

        var spawnCoords = Transform(ent).Coordinates.Offset(new Vector2(0f, -1f));
        var elite = Spawn(eliteProto, spawnCoords);
        EnsureComp<EliteTumorTrackerComponent>(elite).Tumor = ent.Owner;
        ent.Comp.SpawnedElite = elite;

        if (_threshold.TryGetDeadThreshold(elite, out var deadThreshold) && deadThreshold is { } threshold)
            ent.Comp.BaseDeadThreshold = threshold;

        if (ent.Comp.Boosted)
            MakeGhostRole(elite);
    }

    private void MakeGhostRole(EntityUid elite)
    {
        // The template carries the whole ghost role config, copying it avoids
        // writing to GhostRoleComponent members from outside GhostRoleSystem.
        if (_proto.TryIndex<EntityPrototype>(GhostRoleTemplate.Id, out var template))
            EntityManager.AddComponents(elite, template, removeExisting: false);
    }

    private void OnEliteStateChanged(Entity<EliteTumorTrackerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || TerminateOrDeletedTumor(ent.Comp.Tumor))
            return;

        var tumor = ent.Comp.Tumor;
        RemComp<EliteTumorTrackerComponent>(ent);
        OnEliteDied((tumor, Comp<EliteTumorComponent>(tumor)));
    }

    private void OnEliteDied(Entity<EliteTumorComponent> tumor)
    {
        DropLoot(tumor);
        RemoveArena(tumor.Comp);
        RemoveActivator(tumor.Comp);

        _appearance.SetData(tumor, EliteTumorVisuals.State, EliteTumorVisualState.Popped);
        tumor.Comp.State = EliteTumorState.Inactive;
        QueueDel(tumor);
    }

    private void DropLoot(Entity<EliteTumorComponent> tumor)
    {
        var coords = Transform(tumor).Coordinates;
        var crate = Spawn(tumor.Comp.LootCrate, coords);
        _containers.TryGetContainer(crate, "entity_storage", out var container);

        // TG parity: the crate always rolls tendril loot, boosting only adds the trophy roll.
        if (container != null)
        {
            foreach (var spawn in _entityTable.GetSpawns(_proto.Index(TendrilLootTable)))
                _containers.Insert(Spawn(spawn, coords), container);
        }

        if (!tumor.Comp.Boosted)
            return;

        var lootProto = tumor.Comp.FallbackLoot;
        if (tumor.Comp.SpawnEliteProto is { } eliteProto
            && tumor.Comp.Trophies.TryGetValue(eliteProto.Id, out var trophy)
            && _random.Prob(tumor.Comp.TrophyChance))
        {
            lootProto = trophy;
        }

        var loot = Spawn(lootProto, coords);
        if (container != null)
            _containers.Insert(loot, container);
    }

    private void OnActivatorStateChanged(Entity<EliteTumorActivatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || TerminateOrDeletedTumor(ent.Comp.Tumor))
            return;

        Entity<EliteTumorComponent> tumor = (ent.Comp.Tumor, Comp<EliteTumorComponent>(ent.Comp.Tumor));
        if (tumor.Comp.State != EliteTumorState.Active)
            return;

        SetPassive(tumor);
    }

    private void SetPassive(Entity<EliteTumorComponent> tumor)
    {
        tumor.Comp.State = EliteTumorState.Passive;
        _appearance.SetData(tumor, EliteTumorVisuals.State, EliteTumorVisualState.Popped);
        RemoveArena(tumor.Comp);
        RemoveActivator(tumor.Comp);

        if (tumor.Comp.SpawnedElite is not { } elite
            || TerminatingOrDeleted(elite)
            || _mobState.IsDead(elite))
        {
            return;
        }

        _damageable.ClearAllDamage(elite);

        // TG only weakens a boosted elite; an ordinary fight ends with a full heal.
        if (!tumor.Comp.Boosted)
            return;

        tumor.Comp.TimesWon++;
        if (_threshold.TryGetDeadThreshold(elite, out var deadThreshold) && deadThreshold is { } threshold)
            _threshold.SetMobStateThreshold(elite, threshold * tumor.Comp.PostBattleHealthMultiplier, MobState.Dead);
    }

    private void RemoveActivator(EliteTumorComponent tumor)
    {
        if (tumor.Activator is { } activator && !TerminatingOrDeleted(activator))
            RemComp<EliteTumorActivatorComponent>(activator);

        tumor.Activator = null;
    }

    private void BuildArena(EliteTumorComponent tumor)
    {
        RemoveArena(tumor);

        var radius = Math.Max(1, tumor.ArenaRadius);
        var coords = Transform(tumor.Owner).Coordinates;

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (Math.Abs(x) != radius && Math.Abs(y) != radius)
                    continue;

                var wall = Spawn(tumor.ArenaWall, coords.Offset(new Vector2(x, y)));
                tumor.Walls.Add(wall);
            }
        }
    }

    private void RemoveArena(EliteTumorComponent tumor)
    {
        foreach (var wall in tumor.Walls)
        {
            if (!TerminatingOrDeleted(wall))
                QueueDel(wall);
        }

        tumor.Walls.Clear();
    }

    private void KeepInside(EntityUid tumorUid, EliteTumorComponent tumor, EntityUid? entity)
    {
        if (entity is not { } uid || TerminatingOrDeleted(uid))
            return;

        var tumorPos = Transform(tumorUid).WorldPosition;
        var pos = Transform(uid).WorldPosition;
        var diff = pos - tumorPos;
        var limit = MathF.Max(1f, tumor.ArenaRadius - 1f);
        if (diff.LengthSquared() <= limit * limit)
            return;

        var direction = diff.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(diff);
        _xform.SetWorldPosition(uid, tumorPos + direction * MathF.Max(0.5f, limit - 1f));
    }

    private bool TerminateOrDeletedTumor(EntityUid tumor)
        => TerminatingOrDeleted(tumor) || !TryComp<EliteTumorComponent>(tumor, out _);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<EliteTumorComponent>();
        while (query.MoveNext(out var uid, out var tumor))
        {
            if (tumor.State != EliteTumorState.Active)
                continue;

            if (tumor.SpawnedElite is { } elite && TerminatingOrDeleted(elite))
            {
                // The elite vanished without dying (admin delete, chasm): end the fight,
                // otherwise the tumor respawns a fresh elite forever.
                OnEliteDied((uid, tumor));
                continue;
            }

            if (tumor.SpawnedElite == null)
            {
                if (_timing.CurTime < tumor.SpawnAt)
                    continue;

                SpawnElite((uid, tumor));
                continue;
            }

            KeepInside(uid, tumor, tumor.Activator);
            KeepInside(uid, tumor, tumor.SpawnedElite);
        }
    }
}
