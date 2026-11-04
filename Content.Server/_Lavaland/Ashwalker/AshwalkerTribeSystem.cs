// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Clothing.Systems;
using Content.Server.GameTicking;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Destructible;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Ashwalker;

/// <summary>
/// Ash walker tribe from TG: a team antagonist that protects its necropolis nest.
/// Keeps the member list, the sacrifice tally and the round end report.
/// </summary>
public sealed class AshwalkerTribeSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;

    private const string ProtectObjective = "AshwalkerProtectNecropolisObjective";
    private const string TribalGear = "AshWalker";

    /// <summary>
    /// How far from a nest an ash walker may spawn and still join its tribe.
    /// </summary>
    private const float TribeRange = 60f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshwalkerTribeMemberComponent, MapInitEvent>(OnMemberMapInit);
        SubscribeLocalEvent<AshwalkerTribeMemberComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
    }

    /// <summary>
    /// Gets the tribe state of a nest, creating it on first use.
    /// </summary>
    public AshwalkerTribeComponent EnsureTribe(EntityUid nest)
    {
        var tribe = EnsureComp<AshwalkerTribeComponent>(nest);
        tribe.Nest = nest;
        return tribe;
    }

    /// <summary>
    /// Counts a corpse the nest consumed.
    /// </summary>
    public void RegisterSacrifice(EntityUid nest)
    {
        if (TryComp<AshwalkerTribeComponent>(nest, out var tribe))
            tribe.Sacrifices++;
    }

    /// <summary>
    /// Counts an egg the nest hatched.
    /// </summary>
    public void RegisterEgg(EntityUid nest)
    {
        if (TryComp<AshwalkerTribeComponent>(nest, out var tribe))
            tribe.EggsCreated++;
    }

    public void NotifyNestDestroyed(EntityUid nest)
    {
        if (!TryComp<AshwalkerTribeComponent>(nest, out var tribe) || tribe.Members.Count == 0)
            return;

        // Keep reporting a fallen tribe after the nest itself is gone.
        var grave = Spawn();
        var fallen = AddComp<AshwalkerTribeComponent>(grave);
        fallen.Nest = EntityUid.Invalid;
        fallen.Members.UnionWith(tribe.Members);
        fallen.Sacrifices = tribe.Sacrifices;
        fallen.EggsCreated = tribe.EggsCreated;
    }

    // Gear is applied once per body at spawn: a re-taken ghost role must not stack a second kit.
    private void OnMemberMapInit(Entity<AshwalkerTribeMemberComponent> ent, ref MapInitEvent args)
    {
        // Body parts come from the body's own MapInit, so hands do not exist yet. Apply the kit
        // one tick later, otherwise the in-hand spear is left lying on the floor.
        Timer.Spawn(TimeSpan.Zero, () =>
        {
            if (TerminatingOrDeleted(ent.Owner))
                return;

            _outfit.SetOutfit(ent.Owner, TribalGear);
        });
    }

    private void OnMindAdded(Entity<AshwalkerTribeMemberComponent> ent, ref MindAddedMessage args)
    {
        if (FindNearestNest(ent.Owner) is not { } nest)
            return;

        var tribe = EnsureTribe(nest);
        if (!tribe.Members.Add(args.Mind))
            return;

        if (!_objectives.TryCreateObjective(args.Mind, ProtectObjective, out var objective) || objective is not { } objectiveUid)
            return;

        // The objective completes while the nest exists, so point it at this nest.
        if (TryComp<NecropolisAliveConditionComponent>(objectiveUid, out var condition))
            condition.Nest = nest;

        _mind.AddObjective(args.Mind.Owner, args.Mind.Comp, objectiveUid);
    }

    private EntityUid? FindNearestNest(EntityUid member)
    {
        var memberXform = Transform(member);
        if (memberXform.MapID == MapId.Nullspace)
            return null;

        EntityUid? best = null;
        var bestDistance = TribeRange * TribeRange;

        var query = EntityQueryEnumerator<AshwalkerNestComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var nestXform))
        {
            if (nestXform.MapID != memberXform.MapID)
                continue;

            var distance = (nestXform.WorldPosition - memberXform.WorldPosition).LengthSquared();
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        return best;
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        var query = EntityQueryEnumerator<AshwalkerTribeComponent>();
        while (query.MoveNext(out _, out var tribe))
        {
            if (tribe.Members.Count == 0)
                continue;

            var standing = tribe.Nest != EntityUid.Invalid && !TerminatingOrDeleted(tribe.Nest);
            args.AddLine(Loc.GetString("ashwalker-tribe-roundend-header"));
            args.AddLine(Loc.GetString(standing
                ? "ashwalker-tribe-roundend-standing"
                : "ashwalker-tribe-roundend-fallen"));
            args.AddLine(Loc.GetString("ashwalker-tribe-roundend-counters",
                ("sacrifices", tribe.Sacrifices),
                ("eggs", tribe.EggsCreated)));

            // TG prints the tribe roster at round end, the names make the report useful.
            var names = new List<string>();
            foreach (var member in tribe.Members)
            {
                if (!TerminatingOrDeleted(member))
                    names.Add(Name(member));
            }

            if (names.Count > 0)
                args.AddLine(Loc.GetString("ashwalker-tribe-roundend-members", ("members", string.Join(", ", names))));
        }
    }
}
