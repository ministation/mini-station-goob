// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.TypanWar;

public sealed class TypanWarRespawnSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly TypanStationWarRuleSystem _warRule = default!;
    [Dependency] private readonly TypanWarCaptureZoneSystem _captureZones = default!;
    [Dependency] private readonly TypanWarFriendlyFireSystem _friendlyFire = default!;
    [Dependency] private readonly TypanWarMinimapSystem _minimap = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, HumanoidCharacterProfile> _profiles = new();
    private float _uiUpdateAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        Subs.BuiEvents<GhostComponent>(TypanWarRespawnUiKey.Key, subs =>
        {
            subs.Event<TypanWarRespawnRequestMessage>(OnRespawnRequest);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        _uiUpdateAccumulator += frameTime;
        if (_uiUpdateAccumulator < 0.5f)
            return;

        _uiUpdateAccumulator = 0f;
        UpdateOpenRespawnUis();
        TryOpenPendingRespawnUis();
    }

    private void TryOpenPendingRespawnUis()
    {
        var minds = EntityQueryEnumerator<TypanWarCombatMindComponent, MindComponent>();
        while (minds.MoveNext(out var mindId, out var combat, out var mind))
        {
            if (combat.RespawnUiOpen)
                continue;

            if (GetGhostEntity(mind) is not { } ghost)
                continue;

            if (mind.UserId is not { } userId || !_players.TryGetSessionById(userId, out var session))
                continue;

            OpenRespawnUi(ghost, mindId, combat, session);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive || !HasComp<GhostComponent>(args.Entity))
            return;

        var uid = args.Entity;

        if (!TryGetCombatMindFromGhost(uid, out var mindId, out var combat))
            return;

        if (!TryComp<MindComponent>(mindId.Value, out var mind) || mind.UserId != args.Player.UserId)
            return;

        if (combat.RespawnUiOpen)
        {
            PushRespawnUiState(uid, combat);
            return;
        }

        OpenRespawnUi(uid, mindId.Value, combat, args.Player);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _profiles.Clear();
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!TypanStationWarRuleSystem.IsModeActive)
            return;

        if (!_mind.TryGetMind(ev.Mob, out var mindId, out var mind))
            return;

        if (!_warRule.TryGetWarSide((mindId, mind), out _))
            return;

        _profiles[mindId] = ev.Profile;
    }

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead || !TypanStationWarRuleSystem.IsWarActive)
            return;

        if (!TryComp<MindContainerComponent>(ev.Target, out var mindContainer) || mindContainer.Mind == null)
            return;

        if (!TryComp<TypanWarCombatMindComponent>(mindContainer.Mind, out var combat))
            return;

        if (!TryGetActiveRule(out var rule))
            return;

        var delay = CalculateRespawnDelay(rule);
        combat.RespawnAvailableAt = _timing.CurTime + TimeSpan.FromSeconds(delay);
        Dirty(mindContainer.Mind.Value, combat);
    }

    public void HandleGhostMindAdded(EntityUid ghostUid, MindAddedMessage args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        if (!TryComp<TypanWarCombatMindComponent>(args.Mind.Owner, out var combat))
            return;

        if (combat.RespawnAvailableAt == null)
        {
            if (!TryGetActiveRule(out var rule))
                return;

            combat.RespawnAvailableAt = _timing.CurTime + TimeSpan.FromSeconds(CalculateRespawnDelay(rule));
            Dirty(args.Mind.Owner, combat);
        }

        if (args.Mind.Comp.UserId is not { } userId || !_players.TryGetSessionById(userId, out var session))
            return;

        if (combat.RespawnUiOpen)
        {
            PushRespawnUiState(ghostUid, combat);
            return;
        }

        OpenRespawnUi(ghostUid, args.Mind.Owner, combat, session);
    }

    private void OnRespawnRequest(EntityUid uid, GhostComponent component, TypanWarRespawnRequestMessage args)
    {
        if (!TypanStationWarRuleSystem.IsWarActive)
            return;

        EntityUid? mindId = null;
        MindComponent? mind = null;

        if (TryComp<VisitingMindComponent>(uid, out var visiting))
        {
            mindId = visiting.MindId;
            if (mindId != null)
                TryComp(mindId.Value, out mind);
        }
        else if (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.Mind is { } ghostMind)
        {
            mindId = ghostMind;
            TryComp(ghostMind, out mind);
        }
        else if (_mind.TryGetMind(args.Actor, out var actorMind, out var actorMindComp))
        {
            mindId = actorMind;
            mind = actorMindComp;
        }

        if (mindId == null || mind == null)
            return;

        if (!TryComp<TypanWarCombatMindComponent>(mindId.Value, out var combat))
            return;

        if (combat.RespawnAvailableAt != null && _timing.CurTime < combat.RespawnAvailableAt)
            return;

        var options = BuildRespawnOptions(combat);
        if (options.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("typan-war-respawn-no-options"), uid, args.Actor);
            return;
        }

        if (args.OptionIndex < 0 || args.OptionIndex >= options.Length)
            return;

        var option = options[args.OptionIndex];
        if (option.IsBase && combat.BaseSpawn == default)
            return;

        if (!_profiles.TryGetValue(mindId.Value, out var profile) && !TryGetStoredProfile(mind, out profile))
        {
            _popup.PopupEntity(Loc.GetString("typan-war-respawn-no-profile"), uid, args.Actor);
            return;
        }

        var job = ResolveJob(combat);
        var coords = option.IsBase ? combat.BaseSpawn : option.Coordinates;
        var mob = _spawning.SpawnPlayerMob(coords, job, profile, combat.Station);
        if (mob == EntityUid.Invalid)
        {
            Log.Warning($"Typan war respawn failed for mind {mindId} at option {args.OptionIndex}.");
            _popup.PopupEntity(Loc.GetString("typan-war-respawn-failed"), uid, args.Actor);
            return;
        }

        _ui.CloseUi(uid, TypanWarRespawnUiKey.Key, args.Actor);

        _mind.TransferTo(mindId.Value, mob, mind: mind);
        Del(uid);

        combat.RespawnAvailableAt = null;
        combat.RespawnUiOpen = false;
        Dirty(mindId.Value, combat);

        _friendlyFire.SetupCombatant(mob, combat.Side);
        _minimap.EnsureMinimapAction(mob);
    }

    private void OpenRespawnUi(EntityUid ghostUid, EntityUid mindId, TypanWarCombatMindComponent combat, ICommonSession session)
    {
        combat.RespawnUiOpen = true;
        Dirty(mindId, combat);

        _ui.SetUi(ghostUid, TypanWarRespawnUiKey.Key,
            new InterfaceData("TypanWarRespawnBoundUserInterface", interactionRange: 0f, requireInputValidation: false));
        _ui.OpenUi(ghostUid, TypanWarRespawnUiKey.Key, session);
        PushRespawnUiState(ghostUid, combat);
    }

    private void UpdateOpenRespawnUis()
    {
        var minds = EntityQueryEnumerator<TypanWarCombatMindComponent, MindComponent>();
        while (minds.MoveNext(out _, out var combat, out var mind))
        {
            if (!combat.RespawnUiOpen)
                continue;

            if (GetGhostEntity(mind) is not { } ghost)
                continue;

            PushRespawnUiState(ghost, combat);
        }
    }

    private void PushRespawnUiState(EntityUid ghostUid, TypanWarCombatMindComponent combat)
    {
        var remaining = 0f;
        if (combat.RespawnAvailableAt != null)
            remaining = (float) (combat.RespawnAvailableAt.Value - _timing.CurTime).TotalSeconds;

        var canRespawn = remaining <= 0f;
        var options = BuildRespawnOptions(combat);
        var uiOptions = options.Select((o, i) => new TypanWarRespawnOption
        {
            Index = i,
            Label = o.Label,
            Description = o.Description,
        }).ToArray();

        _ui.SetUiState(ghostUid, TypanWarRespawnUiKey.Key, new TypanWarRespawnBoundUserInterfaceState(
            Math.Max(0f, remaining),
            canRespawn,
            uiOptions));
    }

    private ProtoId<JobPrototype> ResolveJob(TypanWarCombatMindComponent combat)
    {
        if (!string.IsNullOrEmpty(combat.Job.Id))
            return combat.Job;

        return combat.Side == TypanWarSide.Nanotrasen
            ? new ProtoId<JobPrototype>("SecurityOfficer")
            : new ProtoId<JobPrototype>("TypanPatrol");
    }

    private RespawnOptionData[] BuildRespawnOptions(TypanWarCombatMindComponent combat)
    {
        var options = new List<RespawnOptionData>();
        var owner = combat.Side switch
        {
            TypanWarSide.Nanotrasen => TypanWarCaptureOwner.Nanotrasen,
            _ => TypanWarCaptureOwner.Typan,
        };

        if (combat.BaseSpawn != default)
        {
            options.Add(new RespawnOptionData(
                true,
                combat.BaseSpawn,
                Loc.GetString("typan-war-respawn-base"),
                Loc.GetString("typan-war-respawn-base-desc")));
        }

        foreach (var zone in _captureZones.GetOwnedZoneCoordinates(owner))
        {
            options.Add(new RespawnOptionData(
                false,
                zone.Coordinates,
                Loc.GetString("typan-war-respawn-zone", ("label", zone.Label), ("location", zone.DisplayName)),
                Loc.GetString("typan-war-respawn-zone-desc")));
        }

        return options.ToArray();
    }

    private bool TryGetStoredProfile(MindComponent mind, [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        profile = null;
        if (mind.UserId is not { } userId)
            return false;

        var prefs = _prefs.GetPreferences(userId);
        profile = (HumanoidCharacterProfile) prefs.SelectedCharacter;
        return true;
    }

    private float CalculateRespawnDelay(TypanStationWarRuleComponent rule)
    {
        if (rule.WarStartTime == null)
            return rule.MinRespawnSeconds;

        var elapsed = (_timing.CurTime - rule.WarStartTime.Value).TotalSeconds;
        var t = Math.Clamp(elapsed / rule.WarDurationSeconds, 0, 1);
        return rule.MinRespawnSeconds + (float) t * (rule.MaxRespawnSeconds - rule.MinRespawnSeconds);
    }

    private bool TryGetActiveRule([NotNullWhen(true)] out TypanStationWarRuleComponent? rule)
    {
        rule = null;
        var query = EntityQueryEnumerator<TypanStationWarRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(uid, gameRule) || comp.Phase != TypanWarPhase.Active)
                continue;

            rule = comp;
            return true;
        }

        return false;
    }

    private EntityUid? GetGhostEntity(MindComponent mind)
    {
        if (mind.VisitingEntity is { } visiting && visiting.IsValid() && HasComp<GhostComponent>(visiting))
            return visiting;

        if (mind.CurrentEntity is { } current && current.IsValid() && HasComp<GhostComponent>(current))
            return current;

        return null;
    }

    private bool TryGetCombatMindFromGhost(
        EntityUid ghostUid,
        [NotNullWhen(true)] out EntityUid? mindId,
        [NotNullWhen(true)] out TypanWarCombatMindComponent? combat)
    {
        mindId = null;
        combat = null;

        if (TryComp<VisitingMindComponent>(ghostUid, out var visiting))
            mindId = visiting.MindId;
        else if (TryComp<MindContainerComponent>(ghostUid, out var container) && container.Mind is { } contained)
            mindId = contained;
        else
            return false;

        if (mindId == null || !TryComp(mindId.Value, out TypanWarCombatMindComponent? comp))
            return false;

        combat = comp;
        return true;
    }

    private readonly record struct RespawnOptionData(bool IsBase, EntityCoordinates Coordinates, string Label, string Description);
}
