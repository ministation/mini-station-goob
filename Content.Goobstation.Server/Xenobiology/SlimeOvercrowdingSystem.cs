// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Shared.Xenobiology;
using Content.Goobstation.Shared.Xenobiology.Components;
using Content.Server.Mind;
using Content.Server.NPC.HTN;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Goobstation.Server.Xenobiology;

public sealed class SlimeOvercrowdingSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SlimeLatchSystem _latch = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedMindSystem _sharedMind = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    private EntityQuery<SlimeComponent> _slimeQuery;
    private EntityQuery<SlimeClusterComponent> _clusterQuery;

    private float _radius = 4f;
    private int _htnThreshold = 8;
    private int _mergeThreshold = 10;
    private float _checkInterval = 3f;
    private TimeSpan _nextCheck = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        _slimeQuery = GetEntityQuery<SlimeComponent>();
        _clusterQuery = GetEntityQuery<SlimeClusterComponent>();

        Subs.CVar(_cfg, GoobCVars.OvercrowdingRadius, val => _radius = val, true);
        Subs.CVar(_cfg, GoobCVars.OvercrowdingHtnThreshold, val => _htnThreshold = val, true);
        Subs.CVar(_cfg, GoobCVars.OvercrowdingMergeThreshold, val => _mergeThreshold = val, true);
        Subs.CVar(_cfg, GoobCVars.OvercrowdingCheckInterval, val => _checkInterval = val, true);

        SubscribeLocalEvent<SlimeClusterComponent, ExaminedEvent>(OnClusterExamined);
        SubscribeLocalEvent<SlimeClusterComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SlimeClusterComponent, SlimeClusterPeelDoAfterEvent>(OnPeelDoAfter);
        SubscribeLocalEvent<SlimeClusterComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshClusterSpeed);
    }

    private void OnRefreshClusterSpeed(Entity<SlimeClusterComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Count <= 1)
            return;

        args.ModifySpeed(ent.Comp.MergedWalkSpeedModifier, ent.Comp.MergedSprintSpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateMergeAnimations();

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(_checkInterval);
        ClearStaleMerging();
        ProcessOvercrowding();
    }

    private void UpdateMergeAnimations()
    {
        var anchors = new List<EntityUid>();
        var mergeQuery = EntityQueryEnumerator<SlimeMergingComponent>();
        while (mergeQuery.MoveNext(out var uid, out var merging))
        {
            if (merging.IsAnchor)
                anchors.Add(uid);
        }

        foreach (var anchor in anchors)
        {
            if (!TryComp<SlimeMergingComponent>(anchor, out var merge) || !merge.IsAnchor)
                continue;

            if (TerminatingOrDeleted(anchor))
            {
                CancelMerge(anchor, merge);
                continue;
            }

            var durationSeconds = merge.Duration.TotalSeconds;
            var progress = durationSeconds <= 0
                ? 1f
                : Math.Clamp((float)((_timing.CurTime - merge.StartedAt).TotalSeconds / durationSeconds), 0f, 1f);

            var eased = progress * progress;
            var anchorPos = _transform.GetWorldPosition(anchor);
            var scale = MathHelper.Lerp(merge.StartScale, merge.TargetScale, progress);
            SetClusterVisualScale(anchor, scale);

            foreach (var victim in merge.Victims.ToArray())
            {
                if (TerminatingOrDeleted(victim)
                    || !TryComp<SlimeMergingComponent>(victim, out var victimMerge)
                    || !CanReachSlime(victim, anchor))
                {
                    merge.Victims.Remove(victim);
                    RemComp<SlimeMergingComponent>(victim);
                    RestoreVictimMovement(victim);
                    continue;
                }

                var pos = Vector2.Lerp(victimMerge.MergeStartPosition, anchorPos, eased);
                _transform.SetWorldPosition(victim, pos);

                if (TryComp<PhysicsComponent>(victim, out var body))
                    _physics.SetLinearVelocity(victim, Vector2.Zero, body: body);
            }

            if (progress < 1f)
                continue;

            CompleteMergeGroup(anchor, merge.Victims, merge.Breed, merge.TotalCount);
        }
    }

    private void ClearStaleMerging()
    {
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(6);
        var query = EntityQueryEnumerator<SlimeMergingComponent>();
        while (query.MoveNext(out var uid, out var merging))
        {
            if (merging.StartedAt >= cutoff)
                continue;

            if (merging.IsAnchor)
                CancelMerge(uid, merging);
            else
                RemComp<SlimeMergingComponent>(uid);
        }
    }

    private void CancelMerge(EntityUid anchor, SlimeMergingComponent merge)
    {
        foreach (var victim in merge.Victims)
        {
            RemComp<SlimeMergingComponent>(victim);
            RestoreVictimMovement(victim);
        }

        if (!TerminatingOrDeleted(anchor))
            RemComp<SlimeMergingComponent>(anchor);
    }

    private void ProcessOvercrowding()
    {
        var visitedSpatial = new HashSet<EntityUid>();
        var visitedMerge = new HashSet<EntityUid>();
        var overcrowdedNow = new HashSet<EntityUid>();

        var slimeEnum = EntityQueryEnumerator<SlimeComponent, MobStateComponent>();
        while (slimeEnum.MoveNext(out var uid, out _, out _))
        {
            if (visitedSpatial.Contains(uid) || !CanParticipate(uid))
                continue;

            var spatialGroup = BuildSpatialGroup(uid, visitedSpatial);
            if (spatialGroup.Count < _htnThreshold)
                continue;

            var showedPopup = false;
            foreach (var member in spatialGroup)
            {
                overcrowdedNow.Add(member);
                SetOvercrowded(member, ref showedPopup);
            }
        }

        slimeEnum = EntityQueryEnumerator<SlimeComponent, MobStateComponent>();
        while (slimeEnum.MoveNext(out var uid, out _, out _))
        {
            if (visitedMerge.Contains(uid) || !CanParticipate(uid))
                continue;

            var spatialGroup = BuildSpatialGroup(uid, visitedMerge);

            foreach (var (breed, members) in GroupByBreed(spatialGroup))
            {
                var anchor = SelectMergeAnchor(members);
                var reachable = FilterReachableFromAnchor(anchor, members, spatialGroup);
                var totalCount = CountSlimes(reachable);

                if (!ShouldMergeGroup(reachable, totalCount))
                    continue;

                BeginMergeGroup(reachable, breed, totalCount);

                foreach (var member in reachable)
                    overcrowdedNow.Add(member);
            }
        }

        var overcrowdedEnum = EntityQueryEnumerator<SlimeOvercrowdedComponent, SlimeComponent>();
        while (overcrowdedEnum.MoveNext(out var uid, out _, out _))
        {
            if (overcrowdedNow.Contains(uid))
                continue;

            ClearOvercrowded(uid);
        }
    }

    private bool ShouldMergeGroup(List<EntityUid> group, int totalCount)
    {
        if (totalCount < _mergeThreshold || group.Count <= 1)
            return false;

        var clusterEntities = 0;
        var looseCount = 0;

        foreach (var uid in group)
        {
            if (_clusterQuery.HasComp(uid))
                clusterEntities++;
            else
                looseCount++;
        }

        if (clusterEntities == 1 && looseCount == 1)
            return false;

        return true;
    }

    private void BeginMergeGroup(List<EntityUid> group, ProtoId<BreedPrototype> breed, int totalCount)
    {
        if (group.Count == 0)
            return;

        foreach (var uid in group)
        {
            if (HasComp<SlimeMergingComponent>(uid))
                return;
        }

        var anchor = SelectMergeAnchor(group);
        var victims = new List<EntityUid>();

        foreach (var uid in group)
        {
            if (uid == anchor)
                continue;

            victims.Add(uid);
        }

        var duration = _clusterQuery.TryComp(anchor, out var cluster)
            ? cluster.MergeDelay
            : TimeSpan.FromSeconds(1.5);

        var startScale = _clusterQuery.TryComp(anchor, out var existingCluster)
            ? GetScaleForCount(existingCluster.Count)
            : 1f;

        var targetScale = GetScaleForCount(totalCount);

        var anchorMerge = EnsureComp<SlimeMergingComponent>(anchor);
        anchorMerge.IsAnchor = true;
        anchorMerge.Anchor = anchor;
        anchorMerge.StartedAt = _timing.CurTime;
        anchorMerge.Duration = duration;
        anchorMerge.TotalCount = totalCount;
        anchorMerge.Breed = breed;
        anchorMerge.StartScale = startScale;
        anchorMerge.TargetScale = targetScale;
        anchorMerge.Victims = victims;

        foreach (var victim in victims)
        {
            if (_slimeQuery.TryComp(victim, out var slime) && _latch.IsLatched((victim, slime)))
                _latch.Unlatch((victim, slime));

            if (TryComp<HTNComponent>(victim, out _))
                _htn.SetHTNEnabled(victim, false);

            DisableVictimMovement(victim);

            var victimMerge = EnsureComp<SlimeMergingComponent>(victim);
            victimMerge.IsAnchor = false;
            victimMerge.Anchor = anchor;
            victimMerge.MergeStartPosition = _transform.GetWorldPosition(victim);
            victimMerge.StartedAt = anchorMerge.StartedAt;
            victimMerge.Duration = duration;
        }

        if (!IsPlayerControlled(anchor) && TryComp<HTNComponent>(anchor, out _))
            _htn.SetHTNEnabled(anchor, false);

        SetClusterVisualScale(anchor, startScale);
        PlayMergeStartEffects(anchor, duration);
    }

    private void PlayMergeStartEffects(EntityUid anchor, TimeSpan duration)
    {
        if (_slimeQuery.TryComp(anchor, out var anchorSlime))
            _audio.PlayPvs(anchorSlime.MitosisSound, anchor);

        _jitter.DoJitter(anchor, duration, true, amplitude: 6f, frequency: 12);
    }

    private void DisableVictimMovement(EntityUid uid)
    {
        if (TryComp<InputMoverComponent>(uid, out var mover))
            mover.CanMove = false;
    }

    private void RestoreVictimMovement(EntityUid uid)
    {
        if (TryComp<InputMoverComponent>(uid, out var mover))
            mover.CanMove = true;
    }

    private static float GetScaleForCount(int count)
    {
        return Math.Clamp(1f + (count - 1) * 0.12f, 1f, 3f);
    }

    private void CompleteMergeGroup(EntityUid anchor, List<EntityUid> victims, ProtoId<BreedPrototype> breed, int totalCount)
    {
        if (TerminatingOrDeleted(anchor))
        {
            foreach (var victim in victims)
                RemComp<SlimeMergingComponent>(victim);

            RemComp<SlimeMergingComponent>(anchor);
            return;
        }

        foreach (var uid in victims)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (_sharedMind.TryGetMind(uid, out var mindId, out var mind))
                _mind.TransferTo(mindId, anchor, ghostCheckOverride: true, mind: mind);

            if (_slimeQuery.TryComp(uid, out var slime) && _latch.IsLatched((uid, slime)))
                _latch.Unlatch((uid, slime));

            RemComp<SlimeMergingComponent>(uid);
            QueueDel(uid);
        }

        RemComp<SlimeMergingComponent>(anchor);

        var cluster = EnsureComp<SlimeClusterComponent>(anchor);
        cluster.Count = totalCount;
        Dirty(anchor, cluster);

        if (_slimeQuery.TryComp(anchor, out var anchorSlime) && anchorSlime.Breed != breed)
        {
            anchorSlime.Breed = breed;
            Dirty(anchor, anchorSlime);
        }

        UpdateClusterScale(anchor, cluster.Count);
        UpdateClusterName(anchor, breed);

        if (cluster.Count > 1 && TryComp<HTNComponent>(anchor, out _))
        {
            EnsureComp<SlimeOvercrowdedComponent>(anchor);
            _htn.SetHTNEnabled(anchor, false);
        }

        _popup.PopupCoordinates(Loc.GetString("slime-overcrowding-merged"), Transform(anchor).Coordinates, PopupType.MediumCaution);
    }

    private EntityUid SelectMergeAnchor(List<EntityUid> group)
    {
        foreach (var uid in group)
        {
            if (IsPlayerControlled(uid))
                return uid;
        }

        foreach (var uid in group)
        {
            if (HasComp<SlimeClusterComponent>(uid))
                return uid;
        }

        return group[0];
    }

    private bool IsPlayerControlled(EntityUid uid)
    {
        if (HasComp<ActorComponent>(uid))
            return true;

        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }

    private void UpdateClusterName(EntityUid uid, ProtoId<BreedPrototype> breed)
    {
        if (!_proto.TryIndex(breed, out var breedProto))
            return;

        _meta.SetEntityName(uid, Loc.GetString("slime-cluster-name", ("breed", XenobiologyLoc.GetBreedName(breedProto))));
    }

    private List<EntityUid> BuildSpatialGroup(EntityUid start, HashSet<EntityUid> visited)
    {
        var group = new List<EntityUid>();
        var queue = new Queue<EntityUid>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var uid = queue.Dequeue();
            if (!visited.Add(uid))
                continue;

            if (CanParticipate(uid))
                group.Add(uid);

            foreach (var nearby in _lookup.GetEntitiesInRange<SlimeComponent>(Transform(uid).Coordinates, _radius))
            {
                if (visited.Contains(nearby) || !CanReachSlime(uid, nearby))
                    continue;

                queue.Enqueue(nearby);
            }
        }

        return group;
    }

    private List<EntityUid> FilterReachableFromAnchor(
        EntityUid anchor,
        IReadOnlyList<EntityUid> breedMembers,
        IReadOnlyList<EntityUid> spatialGroup)
    {
        var reachable = new HashSet<EntityUid>();
        var queue = new Queue<EntityUid>();
        queue.Enqueue(anchor);
        reachable.Add(anchor);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var other in spatialGroup)
            {
                if (reachable.Contains(other) || !CanReachSlime(current, other))
                    continue;

                reachable.Add(other);
                queue.Enqueue(other);
            }
        }

        var result = new List<EntityUid>();
        foreach (var uid in breedMembers)
        {
            if (reachable.Contains(uid))
                result.Add(uid);
        }

        return result;
    }

    private bool CanReachSlime(EntityUid from, EntityUid to)
    {
        if (from == to)
            return true;

        return _interaction.InRangeUnobstructed(
            from,
            to,
            range: 0,
            collisionMask: CollisionGroup.Impassable | CollisionGroup.InteractImpassable | CollisionGroup.TableLayer,
            predicate: entity => entity == from || entity == to || _slimeQuery.HasComp(entity),
            overlapCheck: false);
    }

    private IEnumerable<KeyValuePair<ProtoId<BreedPrototype>, List<EntityUid>>> GroupByBreed(IReadOnlyList<EntityUid> spatialGroup)
    {
        var byBreed = new Dictionary<ProtoId<BreedPrototype>, List<EntityUid>>();

        foreach (var uid in spatialGroup)
        {
            if (!_slimeQuery.TryComp(uid, out var slime))
                continue;

            if (!byBreed.TryGetValue(slime.Breed, out var members))
            {
                members = new List<EntityUid>();
                byBreed[slime.Breed] = members;
            }

            members.Add(uid);
        }

        foreach (var entry in byBreed)
            yield return entry;
    }

    private bool CanParticipate(EntityUid uid)
    {
        if (!_slimeQuery.HasComp(uid) || _mobState.IsDead(uid))
            return false;

        if (_container.IsEntityInContainer(uid))
            return false;

        if (HasComp<SlimeMergingComponent>(uid))
            return false;

        if (HasComp<BeingLatchedComponent>(uid))
            return false;

        if (_slimeQuery.TryComp(uid, out var slime) && slime.LatchedTarget != null)
            return false;

        return true;
    }

    private int CountSlimes(IReadOnlyCollection<EntityUid> group)
    {
        var total = 0;
        foreach (var uid in group)
        {
            if (_clusterQuery.TryComp(uid, out var cluster))
                total += cluster.Count;
            else
                total += 1;
        }

        return total;
    }

    private void SetOvercrowded(EntityUid uid, ref bool showedPopup)
    {
        if (IsPlayerControlled(uid))
            return;

        var alreadyOvercrowded = HasComp<SlimeOvercrowdedComponent>(uid);
        EnsureComp<SlimeOvercrowdedComponent>(uid);

        if (TryComp<HTNComponent>(uid, out _))
            _htn.SetHTNEnabled(uid, false);

        if (showedPopup || alreadyOvercrowded)
            return;

        showedPopup = true;
        _popup.PopupCoordinates(Loc.GetString("slime-overcrowding-htn-off"), Transform(uid).Coordinates, PopupType.MediumCaution);
    }

    private void ClearOvercrowded(EntityUid uid)
    {
        RemComp<SlimeOvercrowdedComponent>(uid);

        if (!TryComp<HTNComponent>(uid, out _))
            return;

        if (_clusterQuery.TryComp(uid, out var cluster) && cluster.Count > 1)
            return;

        _htn.SetHTNEnabled(uid, true, 2f);
    }

    public void UpdateClusterScale(EntityUid uid, int count)
    {
        SetClusterVisualScale(uid, GetScaleForCount(count));
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void SetClusterVisualScale(EntityUid uid, float scale)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, XenoSlimeVisuals.ClusterScale, scale, appearance);

        if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.TryGetValue("fix1", out var fixture))
            _physics.SetRadius(uid, "fix1", fixture, fixture.Shape, MathF.Min(0.3f * scale, 0.9f));
    }

    private void OnClusterExamined(Entity<SlimeClusterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("slime-cluster-examine", ("count", ent.Comp.Count)));
    }

    private void OnInteractUsing(Entity<SlimeClusterComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<UtensilComponent>(args.Used, out var utensil) || (utensil.Types & UtensilType.Knife) == 0)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PeelDelay, new SlimeClusterPeelDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPeelDoAfter(Entity<SlimeClusterComponent> ent, ref SlimeClusterPeelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp<UtensilComponent>(args.Used, out var utensil) || (utensil.Types & UtensilType.Knife) == 0)
            return;

        if (!_slimeQuery.TryComp(ent, out var template))
            return;

        if (!_proto.TryIndex(template.Breed, out var breed))
            return;

        var spawned = SpawnNextToOrDrop(ent.Comp.PeelPrototype, ent.Owner, null, breed.Components);
        if (!TryComp<SlimeComponent>(spawned, out var newSlime))
            return;

        CopySlimeState(template, newSlime);
        Dirty(spawned, newSlime);

        if (newSlime.ShouldHaveShader && newSlime.Shader != null)
            _appearance.SetData(spawned, XenoSlimeVisuals.Shader, newSlime.Shader);

        _appearance.SetData(spawned, XenoSlimeVisuals.Color, newSlime.SlimeColor);
        _meta.SetEntityName(spawned, XenobiologyLoc.GetBreedName(breed));

        ent.Comp.Count--;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("slime-cluster-peel-success", ("target", ent)), ent, args.User);

        if (ent.Comp.Count <= 0)
        {
            RemComp<SlimeClusterComponent>(ent);
            UpdateClusterScale(ent, 1);
            RemComp<SlimeOvercrowdedComponent>(ent);
            _meta.SetEntityName(ent, XenobiologyLoc.GetBreedName(breed));

            if (!IsPlayerControlled(ent))
                _htn.SetHTNEnabled(ent, true, 2f);

            return;
        }

        UpdateClusterScale(ent, ent.Comp.Count);

        if (ent.Comp.Count <= 1 && !IsPlayerControlled(ent) && TryComp<HTNComponent>(ent, out _))
            _htn.SetHTNEnabled(ent, true, 2f);
    }

    private static void CopySlimeState(SlimeComponent source, SlimeComponent target)
    {
        target.Breed = source.Breed;
        target.SlimeColor = source.SlimeColor;
        target.Tamer = source.Tamer;
        target.MaxOffspring = source.MaxOffspring;
        target.ExtractsProduced = source.ExtractsProduced;
        target.MutationChance = source.MutationChance;
        target.PotentialMutations = new HashSet<ProtoId<BreedPrototype>>(source.PotentialMutations);
        target.DefaultSlimeProto = source.DefaultSlimeProto;
        target.DefaultExtract = source.DefaultExtract;
        target.ShouldHaveShader = source.ShouldHaveShader;
        target.Shader = source.Shader;
    }
}
