// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Linq;

using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Station.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.Fishing;

public sealed class FishingModeRuleSystem : GameRuleSystem<FishingModeRuleComponent>
{
    private const string PlatingTileId = "Plating";
    private const string WaterEntity = "FloorWaterEntity";

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        // FishCaughtEvent already has a MobStateComponent subscriber in the achievements system
        // and directed events allow one subscriber, so players are matched by their mind instead.
        SubscribeLocalEvent<MindContainerComponent, FishCaughtEvent>(OnFishCaught);
        SubscribeNetworkEvent<FishingModeStatusRequestEvent>(OnStatusRequest);
    }

    protected override void Started(EntityUid uid, FishingModeRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Scores.Clear();
        component.Leader = null;

        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("fishing-mode-start-announcement"),
            Loc.GetString("fishing-mode-announce-sender"));

        BroadcastStatus(component);
        RemoveForeignSources(component);

        // Round start spawns the water later, from RoundStartedEvent, when the maps exist.
        if (GameTicker.RunLevel == GameRunLevel.InRound)
            SpawnWater(component);
    }

    protected override void ActiveTick(EntityUid uid, FishingModeRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        component.HudTimer += frameTime;
        if (component.HudTimer < component.HudIntervalSeconds)
            return;

        component.HudTimer = 0f;
        BroadcastStatus(component);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        if (!TryGetRunningRule(out _, out var component))
            return;

        SpawnWater(component);
    }

    /// <summary>
    /// Every fisher gets a rod in hand and a random bait in the backpack.
    /// </summary>
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!TryGetRunningRule(out _, out var component))
            return;

        GiveGear(args.Mob, component);
    }

    private void GiveGear(EntityUid mob, FishingModeRuleComponent component)
    {
        if (HasComp<GhostComponent>(mob))
            return;

        var coords = Transform(mob).Coordinates;

        var rod = Spawn(component.Rod.Id, coords);
        _hands.TryPickupAnyHand(mob, rod);

        if (component.Baits.Count == 0)
            return;

        var bait = Spawn(RobustRandom.Pick(component.Baits).Id, coords);
        if (_inventory.TryGetSlotEntity(mob, "back", out var backpack) && _storage.Insert(backpack.Value, bait, out _))
            return;

        // No backpack: the bait stays on the floor at the fisher's feet.
    }

    private void OnFishCaught(Entity<MindContainerComponent> ent, ref FishCaughtEvent args)
    {
        if (!TryGetRunningRule(out _, out var component))
            return;

        var weight = FishingModeRarity.DefaultWeight;
        var size = 0f;
        var fishName = string.Empty;

        if (args.Fish is { } fishUid && !TerminatingOrDeleted(fishUid))
        {
            fishName = Name(fishUid);

            if (TryComp<FishComponent>(fishUid, out var fish))
            {
                size = fish.Size;

                if (fish.Fish is { } fishProtoId && _proto.TryIndex(fishProtoId, out FishPrototype? fishProto))
                    weight = fishProto.Rarity;
            }
        }

        var tier = FishingModeRarity.GetTier(weight);
        var points = FishingModeRarity.GetPoints(tier) * 10 + Math.Max(0, (int) (size / 10f));

        component.Scores.TryGetValue(ent.Owner, out var score);
        score.Fish += 1;
        score.Score += points;

        if (score.BestFish == null || tier > score.BestFishRarity)
        {
            score.BestFish = fishName;
            score.BestFishRarity = tier;
        }

        component.Scores[ent.Owner] = score;

        var leader = GetLeader(component);
        if (leader == null || leader == component.Leader)
            return;

        component.Leader = leader;
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString("fishing-mode-new-leader", ("name", Name(leader.Value))),
            Loc.GetString("fishing-mode-announce-sender"));
    }

    /// <summary>
    /// Sends the final table before the round end window opens, so the manifest can show it.
    /// </summary>
    protected override void AppendRoundEndText(EntityUid uid,
        FishingModeRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Scores.Count == 0)
            return;

        RaiseNetworkEvent(new FishingModeRoundEndEvent(BuildLeaders(component, 15)));
    }

    private void OnStatusRequest(FishingModeStatusRequestEvent args, EntitySessionEventArgs ev)
    {
        if (!TryGetRunningRule(out _, out var component))
        {
            RaiseNetworkEvent(new FishingModeStatusEvent(false, Array.Empty<FishingModeLeader>()), ev.SenderSession);
            return;
        }

        RaiseNetworkEvent(new FishingModeStatusEvent(true, BuildLeaders(component, 3)), ev.SenderSession);
    }

    private void BroadcastStatus(FishingModeRuleComponent component)
    {
        RaiseNetworkEvent(new FishingModeStatusEvent(true, BuildLeaders(component, 3)));
    }

    private FishingModeLeader[] BuildLeaders(FishingModeRuleComponent component, int count)
    {
        var leaders = new List<FishingModeLeader>(component.Scores.Count);
        foreach (var (mob, score) in component.Scores)
        {
            if (Deleted(mob))
                continue;

            leaders.Add(new FishingModeLeader(Name(mob), score.Fish, score.Score, score.BestFish ?? string.Empty, score.BestFishRarity));
        }

        leaders.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : b.Fish.CompareTo(a.Fish);
        });

        return leaders.Take(count).ToArray();
    }

    private EntityUid? GetLeader(FishingModeRuleComponent component)
    {
        EntityUid? leader = null;
        var bestScore = int.MinValue;

        foreach (var (mob, score) in component.Scores)
        {
            if (Deleted(mob) || score.Score <= bestScore)
                continue;

            leader = mob;
            bestScore = score.Score;
        }

        return leader;
    }

    /// <summary>
    /// Fishing mode is about water: toilets, vending machines, hydroponics trays and
    /// aquariums stop being fishing sources, otherwise bait can be cast from spawn.
    /// </summary>
    private void RemoveForeignSources(FishingModeRuleComponent component)
    {
        if (component.AllowedSources.Count == 0)
            return;

        var foreign = new List<EntityUid>();
        var query = EntityQueryEnumerator<FishingSourceComponent>();
        while (query.MoveNext(out var uid, out var source))
        {
            if (!component.AllowedSources.Contains(source.Source))
                foreign.Add(uid);
        }

        foreach (var uid in foreign)
            RemComp<FishingSourceComponent>(uid);
    }

    /// <summary>
    /// Places water tiles on plating, which is what maintenance tunnels are made of.
    /// shortcut: maintenance is detected by the plating tile, upgrade to map areas when they are available on the server.
    /// </summary>
    private void SpawnWater(FishingModeRuleComponent component)
    {
        var gridQuery = EntityQueryEnumerator<StationMemberComponent, MapGridComponent>();
        if (!gridQuery.MoveNext(out var gridUid, out _, out var grid))
            return;

        var candidates = new List<Vector2i>();
        var tiles = _map.GetAllTiles(gridUid, grid);
        while (tiles.MoveNext(out var tileRef))
        {
            var tile = tileRef.Value;

            if (_tiles[tile.Tile.TypeId].ID != PlatingTileId)
                continue;

            // Walls and machines are anchored, free plating is a corridor tile.
            if (HasAnchoredEntity(gridUid, grid, tile.GridIndices))
                continue;

            candidates.Add(tile.GridIndices);
        }

        if (candidates.Count == 0)
            return;

        RobustRandom.Shuffle(candidates);
        var amount = Math.Min(component.WaterPools, candidates.Count);
        for (var i = 0; i < amount; i++)
            Spawn(WaterEntity, _map.GridTileToLocal(gridUid, grid, candidates[i]));
    }

    private bool HasAnchoredEntity(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        foreach (var _ in _map.GetAnchoredEntities(gridUid, grid, indices))
            return true;

        return false;
    }

    private bool TryGetRunningRule(out EntityUid ruleUid, out FishingModeRuleComponent component)
    {
        var query = EntityQueryEnumerator<FishingModeRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var ruleComponent, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            ruleUid = uid;
            component = ruleComponent;
            return true;
        }

        ruleUid = default;
        component = default!;
        return false;
    }
}
