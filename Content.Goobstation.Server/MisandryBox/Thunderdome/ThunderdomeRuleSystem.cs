using Content.Shared.GameTicking.Components;
using Content.Goobstation.Common.Mind;
using Content.Goobstation.Common.Mobs;
using Content.Goobstation.Server.MisandryBox.Mind;
using Content.Goobstation.Shared.MisandryBox.Mind;
using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Chat.Managers;
using Content.Server._Mini.TypanWar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Fluids.Components;
using Content.Shared.Item;
using Content.Server.Preferences.Managers;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Audio;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Containers;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Server._CorvaxGoob.Skills;
using System.Text;
using System.Linq;

namespace Content.Goobstation.Server.MisandryBox.Thunderdome;

public sealed class ThunderdomeRuleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly TemporaryMindSystem _tempMind = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;

    private const string RulePrototype = "ThunderdomeRule";
    private EntityUid? _ruleEntity;
    private bool _refillOnKill;

    private readonly Dictionary<ICommonSession, ThunderdomeLoadoutEui> _activeEuis = new();
    private readonly Dictionary<NetUserId, GlobalThunderdomeStats> _globalStats = new();
    private TimeSpan _nextGlobalStatsCleanup = TimeSpan.Zero;
    private int _retentionDays = 30;
    private float _suicidePenaltyWindow = 10f;

    private HashSet<string> _allowedSpecies = new();

    private sealed class GlobalThunderdomeStats
    {
        public string LastCharacterName = string.Empty;
        public int Kills;
        public int Deaths;
        public int BestStreak;
        public int LongestKillstreak;
        public DateTime LastActivity = DateTime.UtcNow;
        public int LastWeaponSelection = -1;
        public int LastGrenadeSelection = 0;
        public int LastMedicalSelection = 0;
        public int LastHeadSelection = 0;
        public int LastNeckSelection = 0;
        public int LastGlassesSelection = 0;
        public int LastBackpackSelection = 0;
        public int LastUtilitySelection = 0;
    }

    public override void Initialize()
    {
        base.Initialize();

        // CorvaxGoob-Thunderdome-start
        if (!_cfg.GetCVar(ThunderdomeCVars.ThunderdomeEnabled))
            return;
        // CorvaxGoob-Thunderdome-end

        Subs.CVar(_cfg, ThunderdomeCVars.ThunderdomeRefill, value => _refillOnKill = value, true);

        Subs.CVar(_cfg, ThunderdomeCVars.GlobalStatsRetentionDays, value => _retentionDays = value, true);

        Subs.CVar(_cfg, ThunderdomeCVars.SuicidePenaltyWindow, value => _suicidePenaltyWindow = value, true);

        Subs.CVar(_cfg, ThunderdomeCVars.AllowedSpecies, value =>
        {
            _allowedSpecies = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToHashSet();
        }, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnding);
        SubscribeLocalEvent<ThunderdomeRuleComponent, RuleLoadedGridsEvent>(OnGridsLoaded);
        SubscribeLocalEvent<ThunderdomeLeaderboardComponent, ComponentInit>(OnLeaderboardInit);
        SubscribeNetworkEvent<ThunderdomeJoinRequestEvent>(OnJoinRequest);
        SubscribeNetworkEvent<ThunderdomeLeaveRequestEvent>(OnLeaveRequest);
        SubscribeLocalEvent<ThunderdomePlayerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ThunderdomeOriginalBodyComponent, MobStateChangedEvent>(OnOriginalBodyStateChanged);
        SubscribeNetworkEvent<ThunderdomeRevivalAcceptEvent>(OnRevivalAccept);
        SubscribeLocalEvent<ThunderdomePlayerComponent, SuicideGhostEvent>(OnSuicideAttempt);
        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
        SubscribeLocalEvent<ThunderdomeArenaProtectedComponent, BeforeDamageChangedEvent>(OnArenaEntityDamage);
        SubscribeLocalEvent<TimedDespawnComponent, EntGotInsertedIntoContainerMessage>(OnDespawnPickedUp);
        SubscribeLocalEvent<ThunderdomePlayerComponent, GetAntagSelectionBlockerEvent>(OnAntagSelectionBlocker);
        SubscribeLocalEvent<ThunderdomeOriginalBodyComponent, ExaminedEvent>(OnOriginalBodyExamined);
        SubscribeLocalEvent<ThunderdomePlayerComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ThunderdomePlayerComponent, DamageChangedEvent>(OnThunderdomePlayerDamaged);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // CorvaxGoob-Thunderdome-start
        var duration = _ticker.RoundDuration();
        if (_cfg.GetCVar(ThunderdomeCVars.ActivationDelayEnabled) &&
            (_cfg.GetCVar(ThunderdomeCVars.ActivationDelay) > (int) duration.TotalMinutes))
            return;
        // CorvaxGoob-Thunderdome-end

        // Periodic global stats cleanup
        var curTime = _timing.CurTime;
        if (curTime >= _nextGlobalStatsCleanup)
        {
            CleanupOldGlobalStats();
            _nextGlobalStatsCleanup = curTime + TimeSpan.FromHours(1);
        }

        if (_ruleEntity != null && TryComp<ThunderdomeRuleComponent>(_ruleEntity.Value, out var rule) && rule.Active)
        {
            var now = _timing.CurTime;
            if (now >= rule.NextCleanup)
            {
                rule.NextCleanup = now + rule.CleanupInterval;
                SweepLooseItems(rule);
            }
        }
    }

    private void EnsureRule()
    {
        if (_ruleEntity != null)
            return;

        if (!_ticker.StartGameRule(RulePrototype, out var ruleEntity))
            return;

        _ruleEntity = ruleEntity;
    }

    private void OnRoundEnding(RoundRestartCleanupEvent ev)
    {
        foreach (var eui in _activeEuis.Values)
        {
            if (eui.Player.Status != SessionStatus.Disconnected)
                eui.Close();
        }
        _activeEuis.Clear();

        if (_ruleEntity == null)
            return;

        if (TryComp<ThunderdomeRuleComponent>(_ruleEntity.Value, out var rule))
        {
            var query = EntityQueryEnumerator<ThunderdomePlayerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                _tempMind.TryRestoreAsGhost(uid);
                QueueDel(uid);
            }

            rule.Players.Clear();
            rule.Kills.Clear();
            rule.Deaths.Clear();
            rule.BestStreaks.Clear();
            rule.CharacterNames.Clear();
            rule.CachedLeaderboards.Clear();
            rule.Active = false;
            BroadcastPlayerCount(rule);
        }

        var bodyQuery = EntityQueryEnumerator<ThunderdomeOriginalBodyComponent>();
        while (bodyQuery.MoveNext(out var uid, out _))
        {
            RemComp<ThunderdomeOriginalBodyComponent>(uid);
        }

        _ruleEntity = null;
    }

    private void OnGridsLoaded(Entity<ThunderdomeRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        ent.Comp.ArenaMap = args.Map;
        ent.Comp.ArenaGrids.UnionWith(args.Grids);
        ent.Comp.Active = true;
        WorldGuard(args.Map);
        MarkPreexistingItems(args.Map);
        InitializeLeaderboards(ent);
        BroadcastPlayerCount(ent.Comp);
    }

    private void WorldGuard(MapId map)
    {
        var damageables = new HashSet<Entity<DamageableComponent>>();
        _lookup.GetEntitiesOnMap(map, damageables);

        foreach (var (ent, _) in damageables)
        {
            if (!HasComp<ThunderdomePlayerComponent>(ent))
                EnsureComp<ThunderdomeArenaProtectedComponent>(ent);
        }
    }

    private static void OnArenaEntityDamage(Entity<ThunderdomeArenaProtectedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    private void OnJoinRequest(ThunderdomeJoinRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (TypanStationWarRuleSystem.IsModeActive)
        {
            _chatManager.DispatchServerMessage(session, Loc.GetString("typan-war-ghost-thunderdome-blocked"));
            return;
        }

        if (!_cfg.GetCVar(ThunderdomeCVars.ThunderdomeEnabled))
            return;

        // CorvaxGoob-Thunderdome-start
        var duration = _ticker.RoundDuration();
        if (_cfg.GetCVar(ThunderdomeCVars.ActivationDelayEnabled) &&
            (_cfg.GetCVar(ThunderdomeCVars.ActivationDelay) > (int) duration.TotalMinutes))
            return;
        // CorvaxGoob-Thunderdome-end

        EnsureRule();

        if (_ruleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(_ruleEntity.Value, out var rule)
            || !rule.Active)
            return;

        if (session.AttachedEntity is not { Valid: true } ghostEntity
            || !HasComp<GhostComponent>(ghostEntity)
            || HasComp<ThunderdomePlayerComponent>(ghostEntity))
            return;

        if (_activeEuis.TryGetValue(session, out var existingEui))
        {
            existingEui.Close();
            _activeEuis.Remove(session);
        }

        var eui = new ThunderdomeLoadoutEui(this, _ruleEntity.Value, session);
        _euiManager.OpenEui(eui, session);
        _activeEuis[session] = eui;
    }

    private void OnLeaveRequest(ThunderdomeLeaveRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session.AttachedEntity is not { Valid: true } entity
            || !TryComp<ThunderdomePlayerComponent>(entity, out var tdPlayer))
            return;

        LeaveThunderdome((entity, tdPlayer));
    }

    private void OnMobStateChanged(Entity<ThunderdomePlayerComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.RuleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(ent.Comp.RuleEntity.Value, out var rule))
            return;

        if (args.NewMobState == MobState.Critical && args.Origin is { } attacker && HasComp<ThunderdomePlayerComponent>(attacker))
            ent.Comp.LastAttacker = attacker;

        if (args.NewMobState != MobState.Dead)
            return;

        CreditKill(ent, rule, args.Origin);
        GhostDomePlayer(ent, rule, playSound: false);
    }

    public void SpawnPlayer(ICommonSession session, EntityUid ruleEntity, int weaponIdx, int grenadeIdx, int medicalIdx, int headIdx, int neckIdx, int glassesIdx, int backpackIdx, int utilityIdx)
    {
        if (!TryComp<ThunderdomeRuleComponent>(ruleEntity, out var rule)
            || !rule.Active
            || session.AttachedEntity is not { Valid: true } ghostEntity
            || !HasComp<GhostComponent>(ghostEntity))
            return;

        var spawnCoords = GetRandomSpawnPoint(rule);
        if (spawnCoords == null || !_mind.TryGetMind(ghostEntity, out _, out var mindComp))
            return;

        HumanoidCharacterProfile? profile = null;
        if (mindComp.UserId is { } userId && _prefs.TryGetCachedPreferences(userId, out var prefs))
        {
            var selectedProfile = prefs.SelectedCharacter as HumanoidCharacterProfile;
            if (selectedProfile != null)
            {
                var species = _allowedSpecies.Contains(selectedProfile.Species)
                    ? selectedProfile.Species
                    : SharedHumanoidAppearanceSystem.DefaultSpecies;
                profile = selectedProfile.WithSpecies(species);
            }
        }

        var originalBody = mindComp.OwnedEntity != ghostEntity ? mindComp.OwnedEntity : null;

        var mob = _stationSpawning.SpawnPlayerMob(spawnCoords.Value, null, profile, null);
        _stationSpawning.EquipStartingGear(mob, rule.Gear);

        // Equip cosmetic items first (they don't use storage)
        SpawnHeadLoadout(mob, headIdx, rule);
        SpawnNeckLoadout(mob, neckIdx, rule);
        SpawnGlassesLoadout(mob, glassesIdx, rule);
        SpawnBackpackLoadout(mob, backpackIdx, rule);

        // Then spawn items that go into storage (after backpack is equipped)
        // Grenade first (goes to belt, doesn't affect backpack order)
        SpawnGrenadeLoadout(mob, grenadeIdx, rule);
        // Utility second (goes to backpack)
        SpawnUtilityLoadout(mob, utilityIdx, rule);
        // Medical third (goes to backpack)
        SpawnMedicalLoadout(mob, medicalIdx, rule);
        // Weapon last (ammo goes to backpack, appears in Q/A)
        SpawnLoadoutItems(mob, weaponIdx, rule);

        EnsureComp<IgnoreSkillsComponent>(mob);

        var tdPlayer = EnsureComp<ThunderdomePlayerComponent>(mob);
        tdPlayer.RuleEntity = ruleEntity;
        tdPlayer.WeaponSelection = weaponIdx;
        tdPlayer.GrenadeSelection = grenadeIdx;
        tdPlayer.MedicalSelection = medicalIdx;
        tdPlayer.HeadSelection = headIdx;
        tdPlayer.NeckSelection = neckIdx;
        tdPlayer.GlassesSelection = glassesIdx;
        tdPlayer.BackpackSelection = backpackIdx;
        tdPlayer.UtilitySelection = utilityIdx;
        tdPlayer.CharacterName = profile?.Name ?? "Unknown";

        // Восстанавливаем статистику из rule по UserId
        if (mindComp.UserId != null)
        {
            rule.Kills.TryGetValue(mindComp.UserId.Value, out var kills);
            rule.Deaths.TryGetValue(mindComp.UserId.Value, out var deaths);
            rule.BestStreaks.TryGetValue(mindComp.UserId.Value, out var bestStreak);
            tdPlayer.Kills = kills;
            tdPlayer.Deaths = deaths;
            tdPlayer.CurrentStreak = 0; // Серия сбрасывается при смерти
            tdPlayer.BestStreak = bestStreak;

            // Сохраняем имя персонажа в rule для таблицы лидеров
            rule.CharacterNames[mindComp.UserId.Value] = tdPlayer.CharacterName;

            // Ensure player exists in dictionaries (initialize with 0 if first time)
            if (!rule.Kills.ContainsKey(mindComp.UserId.Value))
                rule.Kills[mindComp.UserId.Value] = 0;
            if (!rule.Deaths.ContainsKey(mindComp.UserId.Value))
                rule.Deaths[mindComp.UserId.Value] = 0;
            if (!rule.BestStreaks.ContainsKey(mindComp.UserId.Value))
                rule.BestStreaks[mindComp.UserId.Value] = 0;

            // Сохраняем выбор лодаута и инициализируем глобальную статистику
            if (!_globalStats.TryGetValue(mindComp.UserId.Value, out var globalStats))
            {
                globalStats = new GlobalThunderdomeStats();
                _globalStats[mindComp.UserId.Value] = globalStats;
            }
            globalStats.LastWeaponSelection = weaponIdx;
            globalStats.LastGrenadeSelection = grenadeIdx;
            globalStats.LastMedicalSelection = medicalIdx;
            globalStats.LastHeadSelection = headIdx;
            globalStats.LastNeckSelection = neckIdx;
            globalStats.LastGlassesSelection = glassesIdx;
            globalStats.LastBackpackSelection = backpackIdx;
            globalStats.LastUtilitySelection = utilityIdx;
            globalStats.LastCharacterName = profile?.Name ?? "Unknown";

            // Update leaderboard immediately to show new player
            UpdateLeaderboard(rule);
        }

        if (originalBody is { Valid: true } body && !HasComp<ThunderdomeOriginalBodyComponent>(body))
        {
            var marker = EnsureComp<ThunderdomeOriginalBodyComponent>(body);
            if (mindComp.UserId is { } ownerId)
                marker.Owner = ownerId;

        }

        if (!_tempMind.TrySwapTempMind(session, mob))
        {
            QueueDel(mob);
            return;
        }

        if (Exists(ghostEntity))
            QueueDel(ghostEntity);

        rule.Players.Add(GetNetEntity(mob));

        _skills.GrantAllSkills(mob); // CorvaxGoob-Skills

        _activeEuis.Remove(session);

        BroadcastPlayerCount(rule);
    }

    private void CleanUpPlayer(Entity<ThunderdomePlayerComponent> ent, ThunderdomeRuleComponent rule, bool playSound, SoundPathSpecifier sound)
    {
        rule.Players.Remove(GetNetEntity(ent));

        if (playSound && _transform.TryGetMapOrGridCoordinates(ent, out var deathCoords))
        {
            var name = Identity.Entity(ent, EntityManager);
            _popup.PopupCoordinates(Loc.GetString("thunderdome-leave-01", ("user", name)),
                deathCoords.Value,
                PopupType.LargeCaution);
            var filter = Filter.Pvs(deathCoords.Value, 1, EntityManager, _playerManager);
            var audioParams = new AudioParams().WithVolume(3);
            _audio.PlayStatic(sound, filter, deathCoords.Value, true, audioParams);
        }

        CleanupStoredOriginalBody(ent, deleteBodies: true);
        _tempMind.TryRestoreAsGhost(ent);
        QueueDel(ent);
        BroadcastPlayerCount(rule);
    }

    private void OnSuicideAttempt(Entity<ThunderdomePlayerComponent> ent, ref SuicideGhostEvent args)
    {
        if (ent.Comp.RuleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(ent.Comp.RuleEntity.Value, out var rule)
            || args.Victim != ent.Owner
            )
            return;

        CreditKill(ent, rule);
        GhostDomePlayer(ent, rule);
        args.Handled = true;
    }

    private void OnGhostAttempt(GhostAttemptHandleEvent args)
    {
        if (args.Mind.CurrentEntity is not { } entity
            || !TryComp<ThunderdomePlayerComponent>(entity, out var tdPlayer)
            || tdPlayer.RuleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(tdPlayer.RuleEntity.Value, out var rule))
            return;

        CreditKill((entity, tdPlayer), rule);
        GhostDomePlayer((entity, tdPlayer), rule);
        args.Handled = true;
        args.Result = true;
    }

    private void CreditKill(Entity<ThunderdomePlayerComponent> victim, ThunderdomeRuleComponent rule, EntityUid? killer = null)
    {
        // Самоубийство или отсутствие киллера
        if (killer == null || killer == victim.Owner || !TryComp<ThunderdomePlayerComponent>(killer.Value, out var killerComp))
        {
            // Check if player is in critical state or recently damaged by another player
            var isCritical = TryComp<MobStateComponent>(victim.Owner, out var mobState) &&
                             mobState.CurrentState == MobState.Critical;
            var timeSinceLastDamage = _timing.CurTime - victim.Comp.LastDamagedByPlayer;
            var recentlyDamaged = timeSinceLastDamage.TotalSeconds < _suicidePenaltyWindow;

            // If in crit OR recently damaged, and attacker exists, credit them with kill
            if ((isCritical || recentlyDamaged) &&
                victim.Comp.LastAttacker != null &&
                TryComp<ThunderdomePlayerComponent>(victim.Comp.LastAttacker.Value, out var attackerComp))
            {
                // Combat death - credit attacker, clear LastAttacker, recurse
                var lastAttacker = victim.Comp.LastAttacker.Value;
                victim.Comp.LastAttacker = null;
                CreditKill(victim, rule, lastAttacker);
                return;
            }

            // Not in combat - peaceful exit, NO death counted
            victim.Comp.LastAttacker = null;
            UpdateLeaderboard(rule);
            return;
        }

        // Has killer - this is a combat death
        victim.Comp.LastAttacker = null;

        // Increment deaths ONLY for combat deaths
        victim.Comp.Deaths++;
        victim.Comp.CurrentStreak = 0;

        _mind.TryGetMind(victim, out _, out var deadMind);
        if (deadMind?.UserId is { } deadUserId)
        {
            rule.Deaths.TryGetValue(deadUserId, out var existingDeaths);
            rule.Deaths[deadUserId] = existingDeaths + 1;

            // Update global stats
            if (!_globalStats.TryGetValue(deadUserId, out var victimGlobalStats))
            {
                victimGlobalStats = new GlobalThunderdomeStats();
                _globalStats[deadUserId] = victimGlobalStats;
            }
            victimGlobalStats.Deaths++;
            victimGlobalStats.LastCharacterName = victim.Comp.CharacterName;
            victimGlobalStats.LastActivity = DateTime.UtcNow;
        }

        // Credit killer
        killerComp.Kills++;
        killerComp.CurrentStreak++;

        if (killerComp.CurrentStreak > killerComp.BestStreak)
            killerComp.BestStreak = killerComp.CurrentStreak;

        if (_mind.TryGetMind(killer.Value, out _, out var killerMind) && killerMind.UserId is { } killerUserId)
        {
            rule.Kills.TryGetValue(killerUserId, out var existingKills);
            rule.Kills[killerUserId] = existingKills + 1;

            // Update best streak in rule
            rule.BestStreaks.TryGetValue(killerUserId, out var existingBestStreak);
            if (killerComp.CurrentStreak > existingBestStreak)
                rule.BestStreaks[killerUserId] = killerComp.CurrentStreak;

            // Update global stats
            if (!_globalStats.TryGetValue(killerUserId, out var killerGlobalStats))
            {
                killerGlobalStats = new GlobalThunderdomeStats();
                _globalStats[killerUserId] = killerGlobalStats;
            }
            killerGlobalStats.Kills++;
            if (killerComp.CurrentStreak > killerGlobalStats.BestStreak)
                killerGlobalStats.BestStreak = killerComp.CurrentStreak;
            if (killerComp.CurrentStreak > killerGlobalStats.LongestKillstreak)
                killerGlobalStats.LongestKillstreak = killerComp.CurrentStreak;
            killerGlobalStats.LastCharacterName = killerComp.CharacterName;
            killerGlobalStats.LastActivity = DateTime.UtcNow;

            BroadcastKillMessage((killer.Value, killerComp), (victim, victim.Comp), rule);
            CheckKillStreak((killer.Value, killerComp), rule);
            UpdateLeaderboard(rule);
        }

        if (_refillOnKill)
        {
            RefillAmmo(killer.Value);
            RefillMedicals(killer.Value);
            RefillGrenade(killer.Value, killerComp, rule);
        }
    }

    private void GhostDomePlayer(
        Entity<ThunderdomePlayerComponent> ent,
        ThunderdomeRuleComponent rule,
        bool playSound = true,
        SoundPathSpecifier? sound = null)
    {
        sound ??= new SoundPathSpecifier("/Audio/Effects/pop_high.ogg");
        CleanUpPlayer(ent, rule, playSound, sound);
    }


    private void LeaveThunderdome(Entity<ThunderdomePlayerComponent> ent)
    {
        if (ent.Comp.RuleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(ent.Comp.RuleEntity.Value, out var rule))
            return;

        // Remove from active players list, but keep stats in dictionaries for round leaderboard
        rule.Players.Remove(GetNetEntity(ent));
        ClearOriginalBodyMarker(ent);
        _tempMind.TryRestoreAsGhost(ent);
        QueueDel(ent);

        BroadcastPlayerCount(rule);
    }

    private void OnOriginalBodyStateChanged(Entity<ThunderdomeOriginalBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Dead or MobState.Invalid || args.OldMobState == MobState.Alive)
            return;

        if (!_playerManager.TryGetSessionById(ent.Comp.Owner, out var session)
            || session.AttachedEntity is not { Valid: true })
            return;

        RaiseNetworkEvent(new ThunderdomeRevivalOfferEvent(), session.Channel);
    }

    private void OnRevivalAccept(ThunderdomeRevivalAcceptEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session.AttachedEntity is not { Valid: true } currentEntity
            || !TryComp<ThunderdomePlayerComponent>(currentEntity, out var tdPlayer)
            || !TryComp<TemporaryMindComponent>(currentEntity, out var tempMind))
            return;

        if (!TryComp<MindComponent>(tempMind.OriginalMind, out var origMind))
            return;

        var originalBody = origMind.OwnedEntity;
        if (originalBody == null || !Exists(originalBody)
            || !TryComp<MobStateComponent>(originalBody, out var mobState)
            || mobState.CurrentState == MobState.Dead)
            return;

        if (tdPlayer.RuleEntity != null
            && TryComp<ThunderdomeRuleComponent>(tdPlayer.RuleEntity.Value, out var rule))
        {
            // Remove from active players list since they're leaving the arena
            rule.Players.Remove(GetNetEntity(currentEntity));
            BroadcastPlayerCount(rule);
        }

        _tempMind.TryRestoreToOriginalBody(currentEntity);
        RemComp<ThunderdomeOriginalBodyComponent>(originalBody.Value);
        QueueDel(currentEntity);
    }

    private void MarkPreexistingItems(MapId map)
    {
        var items = new HashSet<Entity<ItemComponent>>();
        var puddles = new HashSet<Entity<PuddleComponent>>();

        _lookup.GetEntitiesOnMap(map, items);
        _lookup.GetEntitiesOnMap(map, puddles);

        foreach (var (uid, _) in items)
            EnsureComp<ThunderdomeArenaProtectedComponent>(uid);

        foreach (var (uid, _) in puddles)
            EnsureComp<ThunderdomeArenaProtectedComponent>(uid);
    }

    private void SweepLooseItems(ThunderdomeRuleComponent rule)
    {
        if (rule.ArenaMap is not { } map)
            return;

        var items = new HashSet<Entity<ItemComponent>>();
        var puddles = new HashSet<Entity<PuddleComponent>>();

        _lookup.GetEntitiesOnMap(map, items);
        _lookup.GetEntitiesOnMap(map, puddles);

        foreach (var (uid, _) in items)
        {
            if (!HasComp<ThunderdomeArenaProtectedComponent>(uid))
                MarkForDespawn(uid, rule.SweepDespawnTime, checkContainer: true);
        }

        foreach (var (uid, _) in puddles)
        {
            if (!HasComp<ThunderdomeArenaProtectedComponent>(uid))
                MarkForDespawn(uid, rule.SweepDespawnTime);
        }
    }

    private static void OnAntagSelectionBlocker(Entity<ThunderdomePlayerComponent> ent, ref GetAntagSelectionBlockerEvent args)
    {
        args.Blocked = true;
    }

    private void OnShouldLogStateChange(ref ShouldLogMobStateChangeEvent args)
    {
        if (HasComp<ThunderdomePlayerComponent>(args.Target))
            args.Cancelled = true;
    }

    private void OnOriginalBodyExamined(Entity<ThunderdomeOriginalBodyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<MobStateComponent>(ent, out var mobState) && mobState.CurrentState == MobState.Dead)
            args.PushMarkup($"[color=yellow]{Loc.GetString("comp-mind-examined-dead-and-ssd", ("ent", ent))}[/color]");
        else
            args.PushMarkup($"[color=yellow]{Loc.GetString("comp-mind-examined-ssd", ("ent", ent))}[/color]");
    }

    private void OnPlayerDetached(Entity<ThunderdomePlayerComponent> ent, ref PlayerDetachedEvent args)
    {
        if (args.Player.Status != SessionStatus.Disconnected)
            return;

        _activeEuis.Remove(args.Player);

        // TemporaryMindSystem may have already cleaned up, so search by UserId
        var query = EntityQueryEnumerator<ThunderdomeOriginalBodyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Owner == args.Player.UserId)
            {
                RemComp<ThunderdomeOriginalBodyComponent>(uid);
                break;
            }
        }

        if (ent.Comp.RuleEntity == null
            || !TryComp<ThunderdomeRuleComponent>(ent.Comp.RuleEntity.Value, out var rule))
            return;

        rule.Players.Remove(GetNetEntity(ent));
        QueueDel(ent);
        BroadcastPlayerCount(rule);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
        {
            var query = EntityQueryEnumerator<ThunderdomeRuleComponent, ActiveGameRuleComponent>();
            while (query.MoveNext(out var ent, out var rule, out _))
            {
                RefreshLeaderboardOverrides((ent, rule));
            }
        }

        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (_activeEuis.TryGetValue(args.Session, out var eui))
        {
            eui.Close();
            _activeEuis.Remove(args.Session);
        }
    }

    private void ClearOriginalBodyMarker(EntityUid tempBody)
    {
        CleanupStoredOriginalBody(tempBody, deleteBodies: false);
    }

    private void CleanupStoredOriginalBody(EntityUid tempBody, bool deleteBodies)
    {
        NetUserId? owner = null;
        EntityUid? ownedBody = null;

        if (TryComp<TemporaryMindComponent>(tempBody, out var temp)
            && TryComp<MindComponent>(temp.OriginalMind, out var origMind))
        {
            owner = origMind.OriginalOwnerUserId ?? origMind.UserId;
            ownedBody = origMind.OwnedEntity;

            if (ownedBody is { } body && Exists(body))
            {
                RemComp<ThunderdomeOriginalBodyComponent>(body);

                if (deleteBodies)
                {
                    if (HasComp<GhostComponent>(body))
                        _mind.TransferTo(temp.OriginalMind, null, createGhost: false, mind: origMind);

                    QueueDel(body);
                }
            }
        }

        if (owner == null)
            return;

        var query = EntityQueryEnumerator<ThunderdomeOriginalBodyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Owner != owner)
                continue;

            RemComp<ThunderdomeOriginalBodyComponent>(uid);

            if (deleteBodies && uid != ownedBody)
                QueueDel(uid);
        }
    }

    private void OnDespawnPickedUp(Entity<TimedDespawnComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (HasComp<ThunderdomePlayerComponent>(args.Container.Owner))
            RemComp<TimedDespawnComponent>(ent);
    }

    private void MarkForDespawn(EntityUid uid, float lifetime, bool checkContainer = false)
    {
        if (HasComp<TimedDespawnComponent>(uid))
            return;

        if (checkContainer && _container.IsEntityInContainer(uid))
            return;

        EnsureComp<TimedDespawnComponent>(uid).Lifetime = lifetime;
    }

    private void SpawnLoadoutItems(EntityUid mob, int weaponIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.WeaponLoadouts.Count == 0)
            return;

        if (weaponIdx < 0 || weaponIdx >= rule.WeaponLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid weaponIdx {weaponIdx} received, clamping to valid range [0, {rule.WeaponLoadouts.Count - 1}]");
            weaponIdx = Math.Clamp(weaponIdx, 0, rule.WeaponLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.WeaponLoadouts[weaponIdx].Gear);
    }

    private void SpawnGrenadeLoadout(EntityUid mob, int grenadeIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.GrenadeLoadouts.Count == 0)
            return;

        if (grenadeIdx < 0 || grenadeIdx >= rule.GrenadeLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid grenadeIdx {grenadeIdx} received, clamping to valid range [0, {rule.GrenadeLoadouts.Count - 1}]");
            grenadeIdx = Math.Clamp(grenadeIdx, 0, rule.GrenadeLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.GrenadeLoadouts[grenadeIdx].Gear);
    }

    private void SpawnMedicalLoadout(EntityUid mob, int medicalIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.MedicalLoadouts.Count == 0)
            return;

        if (medicalIdx < 0 || medicalIdx >= rule.MedicalLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid medicalIdx {medicalIdx} received, clamping to valid range [0, {rule.MedicalLoadouts.Count - 1}]");
            medicalIdx = Math.Clamp(medicalIdx, 0, rule.MedicalLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.MedicalLoadouts[medicalIdx].Gear);
    }

    private void SpawnHeadLoadout(EntityUid mob, int headIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.HeadLoadouts.Count == 0)
            return;

        if (headIdx < 0 || headIdx >= rule.HeadLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid headIdx {headIdx} received, clamping to valid range [0, {rule.HeadLoadouts.Count - 1}]");
            headIdx = Math.Clamp(headIdx, 0, rule.HeadLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.HeadLoadouts[headIdx].Gear);
    }

    private void SpawnNeckLoadout(EntityUid mob, int neckIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.NeckLoadouts.Count == 0)
            return;

        if (neckIdx < 0 || neckIdx >= rule.NeckLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid neckIdx {neckIdx} received, clamping to valid range [0, {rule.NeckLoadouts.Count - 1}]");
            neckIdx = Math.Clamp(neckIdx, 0, rule.NeckLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.NeckLoadouts[neckIdx].Gear);
    }

    private void SpawnGlassesLoadout(EntityUid mob, int glassesIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.GlassesLoadouts.Count == 0)
            return;

        if (glassesIdx < 0 || glassesIdx >= rule.GlassesLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid glassesIdx {glassesIdx} received, clamping to valid range [0, {rule.GlassesLoadouts.Count - 1}]");
            glassesIdx = Math.Clamp(glassesIdx, 0, rule.GlassesLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.GlassesLoadouts[glassesIdx].Gear);
    }

    private void SpawnBackpackLoadout(EntityUid mob, int backpackIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.BackpackLoadouts.Count == 0)
            return;

        if (backpackIdx < 0 || backpackIdx >= rule.BackpackLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid backpackIdx {backpackIdx} received, clamping to valid range [0, {rule.BackpackLoadouts.Count - 1}]");
            backpackIdx = Math.Clamp(backpackIdx, 0, rule.BackpackLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.BackpackLoadouts[backpackIdx].Gear);
    }

    private void SpawnUtilityLoadout(EntityUid mob, int utilityIdx, ThunderdomeRuleComponent rule)
    {
        if (rule.UtilityLoadouts.Count == 0)
            return;

        if (utilityIdx < 0 || utilityIdx >= rule.UtilityLoadouts.Count)
        {
            Log.Warning($"ThunderDome: Invalid utilityIdx {utilityIdx} received, clamping to valid range [0, {rule.UtilityLoadouts.Count - 1}]");
            utilityIdx = Math.Clamp(utilityIdx, 0, rule.UtilityLoadouts.Count - 1);
        }

        _stationSpawning.EquipStartingGear(mob, rule.UtilityLoadouts[utilityIdx].Gear);
    }

    private EntityCoordinates? GetRandomSpawnPoint(ThunderdomeRuleComponent rule)
    {
        if (rule.ArenaMap == null)
            return null;

        var spawns = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (spawn.SpawnType != SpawnPointType.LateJoin)
                continue;

            if (xform.GridUid is not { } grid || !rule.ArenaGrids.Contains(grid))
                continue;

            spawns.Add(xform.Coordinates);
        }

        if (spawns.Count == 0)
        {
            var fallbackQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (fallbackQuery.MoveNext(out _, out _, out var xform))
            {
                if (xform.GridUid is not { } grid || !rule.ArenaGrids.Contains(grid))
                    continue;

                spawns.Add(xform.Coordinates);
            }
        }

        return spawns.Count > 0 ? _random.Pick(spawns) : null;
    }

    private void CheckKillStreak(Entity<ThunderdomePlayerComponent> killer, ThunderdomeRuleComponent rule)
    {
        var streak = killer.Comp.CurrentStreak;
        if (streak < 3 || streak > 12)
            return;

        var name = killer.Comp.CharacterName;
        if (!_loc.TryGetString($"thunderdome-streak-{streak}", out var message, ("player", name)))
            return;

        // Отправляем через чат всем игрокам
        foreach (var netEntity in rule.Players)
        {
            if (!TryGetEntity(netEntity, out var playerEntity))
                continue;

            if (_mind.TryGetMind(playerEntity.Value, out _, out var mindComp) &&
                mindComp.UserId != null &&
                _playerManager.TryGetSessionById(mindComp.UserId.Value, out var session))
            {
                _chatManager.DispatchServerMessage(session, message);
            }
        }
    }

    private void BroadcastPlayerCount(ThunderdomeRuleComponent rule)
    {
        var ev = new ThunderdomePlayerCountEvent(rule.Players.Count);
        foreach (var session in _playerManager.Sessions)
        {
            RaiseNetworkEvent(ev, session.Channel);
        }
    }

    public ThunderdomeLoadoutEuiState GetLoadoutState(ThunderdomeRuleComponent rule, NetUserId? userId = null)
    {
        var weapons = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.WeaponLoadouts.Count; i++)
        {
            var loadout = rule.WeaponLoadouts[i];
            weapons.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = loadout.Category,
                SpritePrototype = loadout.Sprite,
            });
        }

        var grenades = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.GrenadeLoadouts.Count; i++)
        {
            var loadout = rule.GrenadeLoadouts[i];
            grenades.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var medicals = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.MedicalLoadouts.Count; i++)
        {
            var loadout = rule.MedicalLoadouts[i];
            medicals.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var heads = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.HeadLoadouts.Count; i++)
        {
            var loadout = rule.HeadLoadouts[i];
            heads.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var necks = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.NeckLoadouts.Count; i++)
        {
            var loadout = rule.NeckLoadouts[i];
            necks.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var glasses = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.GlassesLoadouts.Count; i++)
        {
            var loadout = rule.GlassesLoadouts[i];
            glasses.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var backpacks = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.BackpackLoadouts.Count; i++)
        {
            var loadout = rule.BackpackLoadouts[i];
            backpacks.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        var utilities = new List<ThunderdomeLoadoutOption>();
        for (var i = 0; i < rule.UtilityLoadouts.Count; i++)
        {
            var loadout = rule.UtilityLoadouts[i];
            utilities.Add(new ThunderdomeLoadoutOption
            {
                Index = i,
                Name = loadout.Name,
                Description = loadout.Description ?? string.Empty,
                Category = string.Empty,
                SpritePrototype = loadout.Sprite,
            });
        }

        // Получаем сохраненный выбор
        var lastWeapon = -1;
        var lastGrenade = 0;
        var lastMedical = 0;
        var lastHead = 0;
        var lastNeck = 0;
        var lastGlasses = 0;
        var lastBackpack = 0;
        var lastUtility = 0;
        if (userId.HasValue && _globalStats.TryGetValue(userId.Value, out var stats))
        {
            lastWeapon = stats.LastWeaponSelection;
            lastGrenade = stats.LastGrenadeSelection;
            lastMedical = stats.LastMedicalSelection;
            lastHead = stats.LastHeadSelection;
            lastNeck = stats.LastNeckSelection;
            lastGlasses = stats.LastGlassesSelection;
            lastBackpack = stats.LastBackpackSelection;
            lastUtility = stats.LastUtilitySelection;
        }

        return new ThunderdomeLoadoutEuiState(weapons, grenades, medicals, heads, necks, glasses, backpacks, utilities, rule.Players.Count, lastWeapon, lastGrenade, lastMedical, lastHead, lastNeck, lastGlasses, lastBackpack, lastUtility);
    }

    private void RefillAmmo(EntityUid killer)
    {
        var toCheck = new Queue<EntityUid>();
        var visited = new HashSet<EntityUid>();
        toCheck.Enqueue(killer);
        visited.Add(killer);

        while (toCheck.Count > 0)
        {
            var current = toCheck.Dequeue();

            if (!TryComp<ContainerManagerComponent>(current, out var containerManager))
                continue;

            foreach (var container in _container.GetAllContainers(current, containerManager))
            {
                var inGun = container.ID is "gun_magazine" or "gun_chamber" or "revolver-ammo";

                foreach (var contained in container.ContainedEntities)
                {
                    if (visited.Add(contained))
                        toCheck.Enqueue(contained);

                    if (!inGun && TryComp<BallisticAmmoProviderComponent>(contained, out var ballistic))
                        RefillBallistic((contained, ballistic));

                    if (!inGun && (HasComp<BatteryAmmoProviderComponent>(contained)))
                        RefillBattery(contained);

                    if (!inGun && TryComp<RevolverAmmoProviderComponent>(contained, out var revolver))
                        RefillRevolver((contained, revolver));
                }
            }
        }
    }

    private void RefillBallistic(Entity<BallisticAmmoProviderComponent> ent)
    {
        _gun.RefillBallisticAmmo(ent);
    }

    private void RefillBattery(EntityUid uid)
    {
        var getCharge = new GetChargeEvent();
        RaiseLocalEvent(uid, ref getCharge);

        if (getCharge.MaxCharge <= 0)
            return;

        var delta = getCharge.MaxCharge - getCharge.CurrentCharge;
        if (delta <= 0)
            return;

        var change = new ChangeChargeEvent(delta);
        RaiseLocalEvent(uid, ref change);
    }

    private void RefillRevolver(Entity<RevolverAmmoProviderComponent> ent)
    {
        for (var i = 0; i < ent.Comp.AmmoSlots.Count; i++)
        {
            if (ent.Comp.AmmoSlots[i] is { } ammoEnt)
            {
                _container.Remove(ammoEnt, ent.Comp.AmmoContainer);
                QueueDel(ammoEnt);
                ent.Comp.AmmoSlots[i] = null;
            }

            if (i < ent.Comp.Chambers.Length)
                ent.Comp.Chambers[i] = true;
        }

        Dirty(ent);
    }

    private void RefillMedicals(EntityUid killer)
    {
        var toCheck = new Queue<EntityUid>();
        var visited = new HashSet<EntityUid>();
        toCheck.Enqueue(killer);
        visited.Add(killer);

        while (toCheck.Count > 0)
        {
            var current = toCheck.Dequeue();

            if (!TryComp<ContainerManagerComponent>(current, out var containerManager))
                continue;

            foreach (var container in _container.GetAllContainers(current, containerManager))
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (visited.Add(contained))
                        toCheck.Enqueue(contained);

                    // Refill syringes - restore solution to max
                    if (TryComp<SolutionContainerManagerComponent>(contained, out var solutionManager))
                    {
                        RefillSyringe(contained, solutionManager);
                    }

                    // Refill medical stacks (gauze, ointment, brutepack) to 15
                    if (TryComp<StackComponent>(contained, out var stack))
                    {
                        RefillStack((contained, stack));
                    }
                }
            }
        }
    }

    private void RefillSyringe(EntityUid uid, SolutionContainerManagerComponent solutionManager)
    {
        foreach (var (solutionName, solutionEnt) in _solutionContainer.EnumerateSolutions((uid, solutionManager)))
        {
            var solution = solutionEnt.Comp.Solution;
            var currentReagents = solution.Contents.ToList();

            // If empty, try to get original reagent from prototype by spawning a temporary entity
            if (currentReagents.Count == 0)
            {
                var meta = MetaData(uid);
                if (meta.EntityPrototype != null)
                {
                    // Spawn temporary entity to read its solution
                    var tempEntity = Spawn(meta.EntityPrototype.ID, MapCoordinates.Nullspace);

                    if (TryComp<SolutionContainerManagerComponent>(tempEntity, out var tempSolutionManager))
                    {
                        foreach (var (tempSolutionName, tempSolutionEnt) in _solutionContainer.EnumerateSolutions((tempEntity, tempSolutionManager)))
                        {
                            if (tempSolutionName == solutionName)
                            {
                                var tempSolution = tempSolutionEnt.Comp.Solution;
                                // Copy reagents from temp entity to our syringe
                                foreach (var reagent in tempSolution.Contents)
                                {
                                    _solutionContainer.TryAddReagent(solutionEnt, reagent.Reagent.Prototype, reagent.Quantity);
                                }
                                break;
                            }
                        }
                    }

                    QueueDel(tempEntity);
                }
                continue;
            }

            var totalCurrent = solution.Volume;
            if (totalCurrent <= 0)
                continue;

            var maxVolume = solution.MaxVolume;
            var deficit = maxVolume - totalCurrent;

            if (deficit <= 0)
                continue;

            // Add each reagent proportionally to fill to max
            foreach (var reagent in currentReagents)
            {
                var proportion = reagent.Quantity / totalCurrent;
                var amountToAdd = deficit * proportion;
                _solutionContainer.TryAddReagent(solutionEnt, reagent.Reagent.Prototype, amountToAdd);
            }
        }
    }

    private void RefillStack(Entity<StackComponent> stack)
    {
        const int maxMedicalStack = 15;

        if (stack.Comp.Count < maxMedicalStack)
        {
            _stack.SetCount(stack.Owner, maxMedicalStack, stack.Comp);
        }
    }

    private void RefillGrenade(EntityUid killer, ThunderdomePlayerComponent tdPlayer, ThunderdomeRuleComponent rule)
    {
        // Check if player selected a grenade (index 0 = no grenade)
        if (tdPlayer.GrenadeSelection <= 0 || tdPlayer.GrenadeSelection >= rule.GrenadeLoadouts.Count)
            return;

        var grenadeLoadout = rule.GrenadeLoadouts[tdPlayer.GrenadeSelection];

        // Get the startingGear prototype
        if (!_prototype.TryIndex<StartingGearPrototype>(grenadeLoadout.Gear, out var gearProto))
        {
            Log.Warning($"RefillGrenade: Failed to find startingGear prototype {grenadeLoadout.Gear}");
            return;
        }

        // Extract grenade ID from storage.back
        if (gearProto.Storage == null || !gearProto.Storage.TryGetValue("back", out var backItems) || backItems.Count == 0)
            return;

        var grenadeProtoId = backItems[0]; // First item in back storage is the grenade

        // Try to find backpack in player's inventory
        if (!TryComp<ContainerManagerComponent>(killer, out var containerManager))
            return;

        EntityUid? backpack = null;
        foreach (var container in _container.GetAllContainers(killer, containerManager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                // Check if this is a backpack/storage
                if (HasComp<StorageComponent>(contained))
                {
                    backpack = contained;
                    break;
                }
            }
            if (backpack != null)
                break;
        }

        if (backpack == null)
            return;

        // Spawn grenade and try to insert into backpack
        var grenade = Spawn(grenadeProtoId, Transform(killer).Coordinates);

        if (!TryComp<StorageComponent>(backpack.Value, out var storageComp))
        {
            QueueDel(grenade);
            return;
        }

        if (!_storage.Insert(backpack.Value, grenade, out _, storageComp: storageComp, playSound: false))
        {
            QueueDel(grenade);
        }
    }

    private void BroadcastKillMessage(
        Entity<ThunderdomePlayerComponent> killer,
        Entity<ThunderdomePlayerComponent> victim,
        ThunderdomeRuleComponent rule)
    {
        var killerName = killer.Comp.CharacterName;
        var victimName = victim.Comp.CharacterName;

        var kills = killer.Comp.Kills;
        var deaths = killer.Comp.Deaths;
        var kd = deaths > 0 ? (float)kills / deaths : kills;
        var streak = killer.Comp.CurrentStreak;

        var message = Loc.GetString("thunderdome-kill-announcement",
            ("killer", killerName),
            ("victim", victimName),
            ("kills", kills),
            ("kd", $"{kd:F2}"),
            ("streak", streak));

        foreach (var netEntity in rule.Players)
        {
            if (!TryGetEntity(netEntity, out var playerEntity))
                continue;

            if (_mind.TryGetMind(playerEntity.Value, out _, out var mindComp) &&
                mindComp.UserId != null &&
                _playerManager.TryGetSessionById(mindComp.UserId.Value, out var session))
            {
                _chatManager.DispatchServerMessage(session, message);
            }
        }
    }

    private void UpdateLeaderboard(ThunderdomeRuleComponent rule)
    {
        // Fallback: if cache is empty, try to populate it
        if (rule.CachedLeaderboards.Count == 0 && rule.ArenaMap != null)
        {
            var leaderboards = new HashSet<Entity<ThunderdomeLeaderboardComponent>>();
            _lookup.GetEntitiesOnMap(rule.ArenaMap.Value, leaderboards);

            // Double-check to prevent race condition
            if (leaderboards.Count > 0 && rule.CachedLeaderboards.Count == 0)
            {
                rule.CachedLeaderboards = leaderboards.ToList();

                // Make leaderboards visible to thunderdome participants and spectators
                RefreshLeaderboardOverrides((rule.Owner, rule));
            }
        }

        if (rule.CachedLeaderboards.Count == 0)
            return;

        var roundEntries = GenerateRoundLeaderboardEntries(rule);
        var globalEntries = GenerateGlobalLeaderboardEntries();

        foreach (var (lbUid, lbComp) in rule.CachedLeaderboards)
        {
            var entries = lbComp.IsGlobal ? globalEntries : roundEntries;
            lbComp.Entries = entries;
            lbComp.Title = lbComp.IsGlobal ? "THUNDERDOME ALL-TIME TOP 10" : "THUNDERDOME ROUND TOP 10";
            Dirty(lbUid, lbComp);
        }
    }

    private List<ThunderdomeLeaderboardEntry> GenerateRoundLeaderboardEntries(ThunderdomeRuleComponent rule)
    {
        var leaderboardData = new List<(string Name, int Kills, int Deaths, float KD, int BestStreak, NetUserId UserId)>();

        // Collect data from rule dictionaries instead of live entities
        foreach (var (userId, kills) in rule.Kills)
        {
            rule.Deaths.TryGetValue(userId, out var deaths);
            rule.BestStreaks.TryGetValue(userId, out var bestStreak);
            rule.CharacterNames.TryGetValue(userId, out var name);

            if (string.IsNullOrEmpty(name))
                name = "Unknown";

            var kd = deaths > 0 ? (float)kills / deaths : kills;
            leaderboardData.Add((name, kills, deaths, kd, bestStreak, userId));
        }

        leaderboardData.Sort((a, b) =>
        {
            var killCompare = b.Kills.CompareTo(a.Kills);
            if (killCompare != 0) return killCompare;
            return b.KD.CompareTo(a.KD);
        });

        var topPlayers = leaderboardData.Take(10).ToList();
        var entries = new List<ThunderdomeLeaderboardEntry>();

        for (var i = 0; i < topPlayers.Count; i++)
        {
            var player = topPlayers[i];
            var rank = i + 1;
            entries.Add(new ThunderdomeLeaderboardEntry(
                player.Name,
                player.Kills,
                player.Deaths,
                player.KD,
                player.BestStreak,
                rank));
        }

        return entries;
    }

    private List<ThunderdomeLeaderboardEntry> GenerateGlobalLeaderboardEntries()
    {
        var leaderboardData = new List<(string Ckey, string CharName, int Kills, int Deaths, float KD, int BestStreak)>();

        foreach (var (userId, stats) in _globalStats)
        {
            var kd = stats.Deaths > 0 ? (float)stats.Kills / stats.Deaths : stats.Kills;
            var ckey = "Unknown";

            if (_playerManager.TryGetSessionById(userId, out var session))
                ckey = session.Name;

            var charName = string.IsNullOrEmpty(stats.LastCharacterName) ? "Unknown" : stats.LastCharacterName;

            leaderboardData.Add((ckey, charName, stats.Kills, stats.Deaths, kd, stats.BestStreak));
        }

        leaderboardData.Sort((a, b) =>
        {
            var killCompare = b.Kills.CompareTo(a.Kills);
            if (killCompare != 0) return killCompare;
            return b.KD.CompareTo(a.KD);
        });

        var topPlayers = leaderboardData.Take(10).ToList();
        var entries = new List<ThunderdomeLeaderboardEntry>();

        for (var i = 0; i < topPlayers.Count; i++)
        {
            var player = topPlayers[i];
            var rank = i + 1;
            var displayName = $"{player.Ckey} ({player.CharName})";
            entries.Add(new ThunderdomeLeaderboardEntry(
                displayName,
                player.Kills,
                player.Deaths,
                player.KD,
                player.BestStreak,
                rank));
        }

        return entries;
    }

    private void InitializeLeaderboards(Entity<ThunderdomeRuleComponent> ent)
    {
        if (ent.Comp.ArenaMap == null)
            return;

        var leaderboards = new HashSet<Entity<ThunderdomeLeaderboardComponent>>();
        _lookup.GetEntitiesOnMap(ent.Comp.ArenaMap.Value, leaderboards);

        ent.Comp.CachedLeaderboards = leaderboards.ToList();

        foreach (var (lbUid, leaderboard) in leaderboards)
        {
            leaderboard.RuleEntity = ent;
        }

        RefreshLeaderboardOverrides(ent);
    }

    private void RefreshLeaderboardOverrides(Entity<ThunderdomeRuleComponent> rule)
    {
        if (rule.Comp.CachedLeaderboards.Count == 0)
            return;

        var arenaMap = rule.Comp.ArenaMap;

        foreach (var (lbUid, _) in rule.Comp.CachedLeaderboards)
        {
            foreach (var session in _playerManager.Sessions)
            {
                if (ShouldReceiveThunderdomeLeaderboard(session, arenaMap))
                    _pvsOverride.AddSessionOverride(lbUid, session);
                else
                    _pvsOverride.RemoveSessionOverride(lbUid, session);
            }
        }
    }

    private bool ShouldReceiveThunderdomeLeaderboard(ICommonSession session, MapId? arenaMap)
    {
        if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } player)
            return false;

        if (HasComp<ThunderdomePlayerComponent>(player) || HasComp<GhostComponent>(player))
            return true;

        return arenaMap != null && Transform(player).MapID == arenaMap;
    }

    private void OnThunderdomePlayerDamaged(Entity<ThunderdomePlayerComponent> ent, ref DamageChangedEvent args)
    {
        // Only track damage from other Thunderdome players
        if (args.Origin == null ||
            args.Origin == ent.Owner ||
            !Exists(args.Origin.Value) ||
            !HasComp<ThunderdomePlayerComponent>(args.Origin.Value))
            return;

        // Only track if damage increased (not healing)
        if (!args.DamageIncreased)
            return;

        ent.Comp.LastAttacker = args.Origin.Value;
        ent.Comp.LastDamagedByPlayer = _timing.CurTime;
    }

    private void CleanupOldGlobalStats()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(_retentionDays);
        var beforeCount = _globalStats.Count;

        var toRemove = _globalStats
            .Where(kvp => kvp.Value.LastActivity < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var userId in toRemove)
            _globalStats.Remove(userId);

        if (toRemove.Count > 0)
        {
            Log.Info($"Thunderdome: Cleaned up {toRemove.Count} inactive stats " +
                     $"({beforeCount} → {_globalStats.Count} total, retention: {_retentionDays} days)");
        }
    }

    private void OnLeaderboardInit(Entity<ThunderdomeLeaderboardComponent> ent, ref ComponentInit args)
    {
        // MapText is already on the entity from prototype
    }
}
