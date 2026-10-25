// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Picks a random nearby hostile for a holographic clone to attack. Hostiles with a mindshield are
/// preferred, which matches the reference behaviour of the clones going after security first.
/// </summary>
public sealed partial class NinjaGhostCloneTargetOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// Blackboard key receiving the picked target entity.
    /// </summary>
    [DataField("key")]
    public string Key = "Target";

    /// <summary>
    /// Blackboard key receiving the coordinates of the picked target.
    /// </summary>
    [DataField("keyCoordinates")]
    public string KeyCoordinates = "TargetCoordinates";

    /// <summary>
    /// Search radius.
    /// </summary>
    [DataField("range")]
    public float Range = 10f;

    public override Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var ninja = _entManager.TryGetComponent<NinjaGhostCloneComponent>(owner, out var cloneComp) ? cloneComp.Owner : EntityUid.Invalid;
        var faction = _entManager.System<NpcFactionSystem>();
        var mobState = _entManager.System<MobStateSystem>();

        var candidates = new List<EntityUid>();
        var mindShielded = new List<EntityUid>();

        foreach (var target in faction.GetNearbyHostiles(owner, Range))
        {
            if (target == ninja)
                continue;

            if (!_entManager.TryGetComponent<MobStateComponent>(target, out var state) || !mobState.IsAlive(target, state))
                continue;

            candidates.Add(target);

            if (_entManager.HasComponent<MindShieldComponent>(target))
                mindShielded.Add(target);
        }

        var pool = mindShielded.Count > 0 ? mindShielded : candidates;
        if (pool.Count == 0)
            return Task.FromResult<(bool, Dictionary<string, object>?)>((false, new Dictionary<string, object>()));

        var picked = _random.Pick(pool);
        var effects = new Dictionary<string, object>
        {
            { Key, picked },
            { KeyCoordinates, new EntityCoordinates(picked, Vector2.Zero) },
        };

        return Task.FromResult<(bool, Dictionary<string, object>?)>((true, effects));
    }
}