// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Content.Server._Mini.Katorga.Components;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Ghost;
using Content.Server.KillTracking;
using Content.Server.Materials;
using Content.Server.Mind;
using Content.Server.Parallax;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Katorga;
using Content.Shared._Mini.Katorga.Components;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Materials;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Mini.Katorga;

/// <summary>
///     Каторга: альтернатива обычному серверному бану. Игроки с активными временными банами
///     могут зайти на сервер, но попадают на карту каторги и сокращают срок, добывая руду
///     и сдавая её в приёмник. Перманентные баны отработке не подлежат: их не пускает
///     ConnectionManager и система их не рассматривает как отрабатываемые.
///     Концепция заимствована у сервера Amour (frosty-dev/ss14-amour, система gulag),
///     но реализована под бан-систему Mini Station.
/// </summary>
public sealed class KatorgaSystem : EntitySystem
{
    private static readonly ResPath KatorgaMapPath = new("/Maps/_Mini/katorga.yml");
    private static readonly ResPath KatorgaFreeMapPath = new("/Maps/_Mini/katorga_free.yml");
    private const string KatorgaBiomeId = "KatorgaBiome";
    private const string KatorgaFaunaId = "KatorgaFauna";

    // Снаряжение заключённого: комплект утилизатора и сканер в кармане.
    private static readonly ProtoId<StartingGearPrototype> InmateGear = "KatorgaInmateGear";

    private static readonly TimeSpan SafeguardInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);

    // Сколько раз повторяем фоновую проверку бана, прежде чем не пустить игрока в раунд.
    private const int MaxBanCheckAttempts = 3;
    private static readonly TimeSpan BanCheckRetryDelay = TimeSpan.FromSeconds(5);

    [Dependency] private readonly AdminSystem _adminSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly KatorgaFactionSystem _factionSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly UserDbDataManager _userDbData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    private MapId? _katorgaMap;
    private EntityUid? _katorgaMapUid;

    // Точки спавна по фракции: ключ "" - общие точки (подходят любой фракции).
    private readonly Dictionary<string, List<EntityCoordinates>> _spawnPoints = new();
    private readonly Dictionary<NetUserId, double> _points = new();

    // Ускорение срока: пока заключённый жив на карте каторги, срок течёт быстрее.
    // Накопленные секунды раз в минуту списываются с бана в базе (молча, без сообщений).
    private double _sentenceMultiplier = 1.0;
    private readonly Dictionary<NetUserId, double> _accelPending = new();

    // Кэш отрабатываемого бана по игроку. Единственное место, где каторга смотрит в базу —
    // асинхронная проверка при входе в лобби и перед спавном; все игровые пути (спавн, наблюдение,
    // гост-роли) читают только кэш. Синхронных запросов к БД в игровом потоке нет: если к моменту
    // спавна проверка ещё не закончилась, спавн откладывается до её результата.
    private readonly Dictionary<NetUserId, BanDef> _banCache = new();
    private readonly HashSet<NetUserId> _banCacheChecked = new();
    private readonly HashSet<NetUserId> _banChecksInFlight = new();
    private readonly Dictionary<NetUserId, PendingSpawn> _pendingSpawns = new();

    private sealed class PendingSpawn
    {
        public EntityUid Station;
        public string? JobId;
        public bool LateJoin;
        public bool Silent;
        public int Attempts;
        public TimeSpan RetryAt;
    }

    // Текущий срок (мгновенный взгляд сервера): уменьшается сразу при сдаче руды, в базу списывается пачками.
    private readonly Dictionary<NetUserId, DateTimeOffset> _expiry = new();

    private TimeSpan _nextSafeguard = TimeSpan.Zero;
    private TimeSpan _nextFlush = TimeSpan.Zero;
    private double _pointsToTime;
    private int _fishRewardMinutes;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("katorga");
        _cfg.OnValueChanged(MiniCCVars.KatorgaPointsToTime, v => _pointsToTime = v, true);
        _cfg.OnValueChanged(MiniCCVars.KatorgaSentenceMultiplier, v => _sentenceMultiplier = Math.Clamp(v, 1.0, 3.0), true);
        _cfg.OnValueChanged(MiniCCVars.KatorgaFishRewardMinutes, v => _fishRewardMinutes = v, true);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
        SubscribeLocalEvent<KatorgaOreProcessorComponent, MaterialEntityInsertedEvent>(OnOreInserted);
        SubscribeLocalEvent<FishingRodComponent, FishCaughtWithRodEvent>(OnFishCaught);
        // После штатной инициализации работ: заключённый есть на любой станции и без лимита.
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized, after: new[] { typeof(StationJobsSystem) });
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);

        _net.RegisterNetMessage<KatorgaBanStatusMessage>();

        // После реконнекта бан перепроверяется заново: игрок мог получить бан, уже находясь в игре.
        _userDbData.AddOnPlayerDisconnect(OnPlayerDisconnected);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessPendingBanChecks();

        if (!Enabled)
            return;

        var now = _timing.CurTime;

        if (now >= _nextSafeguard)
        {
            _nextSafeguard = now + SafeguardInterval;
            Safeguard();
        }

        if (now >= _nextFlush)
        {
            _nextFlush = now + FlushInterval;
            FlushAcceleration();
            FlushPoints();
        }

        // Ускорение срока: пока заключённый жив на карте каторги, срок течёт быстрее.
        // Сразу отражаем на живом счётчике, в базу списываем раз в минуту.
        if (_sentenceMultiplier > 1.0)
        {
            var bonus = TimeSpan.FromSeconds(frameTime * (_sentenceMultiplier - 1.0));
            var query = EntityQueryEnumerator<KatorgaBoundComponent, TransformComponent, MobStateComponent>();
            while (query.MoveNext(out var uid, out _, out var xform, out var mobState))
            {
                if (xform.MapID != _katorgaMap || mobState.CurrentState == MobState.Dead)
                    continue;

                if (!_playerManager.TryGetSessionByEntity(uid, out var session) ||
                    !_expiry.TryGetValue(session.UserId, out var expires))
                {
                    continue;
                }

                _expiry[session.UserId] = expires - bonus;
                _accelPending[session.UserId] = _accelPending.GetValueOrDefault(session.UserId) + bonus.TotalSeconds;
            }
        }
    }

    private bool Enabled => _cfg.GetCVar(MiniCCVars.KatorgaEnabled);

    #region События раунда

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _katorgaMap = null;
        _katorgaMapUid = null;
        _spawnPoints.Clear();
        _accelPending.Clear();
        // Кэш банов здесь НЕ чистим: игроки уже прошли лобби и их баны проверены там.
        // RoundStartingEvent приходит прямо перед спавном, и очистка заставляла бы
        // заново ходить в БД на игровом тике (это и вешало сервер).

        if (!Enabled)
            return;

        // Файлы карт хранят только гриды (как карты станций), поэтому грузим их на свежую карту:
        // TryLoadGrid + биом + InitializeMap в конце, по образцу SetupArrivalsStation.
        _mapSystem.CreateMap(out var mapId, runMapInit: false);
        var mapUid = _mapSystem.GetMap(mapId);
        _katorgaMap = mapId;
        _katorgaMapUid = mapUid;

        var options = DeserializationOptions.Default with { PauseMaps = false };

        // Лагерь Горняков - основная карта каторги, без неё каторга недоступна.
        if (!_mapLoader.TryLoadGrid(mapId, KatorgaMapPath, out var minersGrid, options))
        {
            _sawmill.Error("Каторга: не удалось загрузить карту " + KatorgaMapPath + ", каторга в этом раунде недоступна");
            _mapSystem.DeleteMap(mapId);
            _katorgaMap = null;
            _katorgaMapUid = null;
            return;
        }

        _metaData.SetEntityName(minersGrid.Value, Loc.GetString("katorga-grid-miners"));

        // Лагерь Вольных - второй гряд, при неудаче раунд продолжается с одним лагерем.
        EntityUid? freemenGrid = null;
        if (_mapLoader.TryLoadGrid(mapId, KatorgaFreeMapPath, out var freeGrid, options, offset: new System.Numerics.Vector2(120f, 0f)))
        {
            freemenGrid = freeGrid.Value;
            _metaData.SetEntityName(freemenGrid.Value, Loc.GetString("katorga-grid-freemen"));
        }
        else
        {
            _sawmill.Error("Каторга: не удалось загрузить карту " + KatorgaFreeMapPath + ", второй лагерь недоступен");
        }

        _metaData.SetEntityName(mapUid, "Katorga");

        if (_prototype.TryIndex<BiomeTemplatePrototype>(KatorgaBiomeId, out var biome))
        {
            _biome.EnsurePlanet(mapUid, biome);
        }
        else
        {
            _sawmill.Error("Каторга: не найден биом " + KatorgaBiomeId);
        }

        // Фауна вокруг лагерей: настройки из прототипа спавнера.
        if (_prototype.TryIndex<KatorgaFaunaSpawnerPrototype>(KatorgaFaunaId, out var faunaSpawner))
        {
            var population = EnsureComp<KatorgaFaunaPopulationComponent>(mapUid);
            population.InitialRemaining = faunaSpawner.InitialCount;
            population.Bases.Add(minersGrid.Value.Owner);
            if (freemenGrid is { } freeUid)
                population.Bases.Add(freeUid);
            population.NextSpawnTime = _timing.CurTime + TimeSpan.FromSeconds(10);
        }
        else
        {
            _sawmill.Error("Каторга: не найден спавнер фауны " + KatorgaFaunaId);
        }

        // Mapinit после биома: атмосфера, машины и генерация астероида стартуют вместе с картой.
        _mapSystem.InitializeMap(mapId);

        var query = EntityQueryEnumerator<KatorgaSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var spawn, out var xform))
        {
            if (xform.MapID != _katorgaMap)
                continue;

            var key = spawn.Faction?.Id ?? "";
            if (!_spawnPoints.TryGetValue(key, out var list))
                _spawnPoints[key] = list = new List<EntityCoordinates>();

            list.Add(xform.Coordinates);
        }

        var totalSpawns = _spawnPoints.Values.Sum(list => list.Count);
        _sawmill.Info("Каторга: карты загружены, точек спавна: " + totalSpawns);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        // Накопленные очки нужно списать до очистки состояния, иначе отработка пропадёт.
        FlushAcceleration();
        FlushPoints();
        _points.Clear();
        _expiry.Clear();

        _katorgaMap = null;
        _katorgaMapUid = null;
        _spawnPoints.Clear();
        _accelPending.Clear();

        // Кэш раундовый: чистим в конце раунда, а не перед спавном. Игроки заново
        // проверяются при входе в лобби следующего раунда.
        _banCache.Clear();
        _banCacheChecked.Clear();
        _pendingSpawns.Clear();
    }

    #endregion

    #region Отправка на каторгу

    private void OnJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!Enabled)
            return;

        // Проверка банов уходит в фон: игроки без бана не должны ждать (и тем более висеть) из-за каторги.
        StartBanCheck(ev.PlayerSession);
    }

    /// <summary>
    ///     Заключённый есть на каждой станции и без лимита: в late join он единственная должность
    ///     для временно забаненного, а количество одновременно отбывающих не ограничено.
    /// </summary>
    private void OnStationInitialized(StationInitializedEvent ev)
    {
        if (!Enabled || !HasComp<StationJobsComponent>(ev.Station))
            return;

        _stationJobs.MakeJobUnlimited(ev.Station, KatorgaJobs.Prisoner);
    }

    /// <summary>
    ///     Должность заключённого не выдают обычным игрокам: клиентская команда joingame может
    ///     назвать любой job id, поэтому проверяем бан до того, как игрок превратится в заключённого.
    /// </summary>
    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Jobs is not { } jobs || !jobs.Contains(KatorgaJobs.Prisoner))
            return;

        // Каторга выключена: заключённого не существует, роль недоступна.
        if (!Enabled)
        {
            ev.Cancelled = true;
            return;
        }

        // Забанен - его всё равно отправит на каторгу OnBeforeSpawn. Ещё не проверен - пусть
        // спавн отложится (TryDeferPlayerSpawn) и решит после ответа базы.
        if (IsConfined(ev.Player) || !_banCacheChecked.Contains(ev.Player.UserId))
            return;

        ev.Cancelled = true;
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!Enabled)
            return;

        var userId = ev.Player.UserId;
        if (!_banCache.TryGetValue(userId, out var ban))
        {
            // Сюда не должны попадать без готового кэша: GameTicker откладывает такой спавн
            // до ответа БД (TryDeferPlayerSpawn). Если проверка всё же не завершилась,
            // не выпускаем непроверенного игрока в раунд.
            if (!_banCacheChecked.Contains(userId))
            {
                _sawmill.Error("Каторга: спавн игрока " + userId + " без проверки бана, вход отклонён");
                ev.Handled = true;
                ev.Player.Channel.Disconnect(Loc.GetString("katorga-ban-check-failed"));
            }

            return;
        }

        // Отработанный (истёкший) бан не сажает: между проверками срок мог дотечь до конца.
        if (ban.ExpirationTime <= DateTimeOffset.UtcNow)
        {
            _banCache.Remove(userId);
            _expiry.Remove(userId);
            return;
        }

        ev.Handled = true;
        SendToKatorga(ev.Player, ban);
    }

    // Страховка: если заключённый оказался не на карте каторги, возвращаем его на место.
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!Enabled)
            return;

        if (_katorgaMap == null)
            return;

        var userId = ev.Player.UserId;

        // Игрок уже в мире: отложенный спавн больше не нужен.
        _pendingSpawns.Remove(userId);

        if (!_banCache.TryGetValue(userId, out var ban))
        {
            // Кэш пуст: игрок мог переподключиться в своё старое тело уже с баном,
            // либо каторгу включили после его входа. Проверяем в фоне, результат применится
            // на главном потоке: забаненного отправят на каторгу.
            if (!_banCacheChecked.Contains(userId))
                StartBanCheck(ev.Player);

            return;
        }

        if (ban.ExpirationTime <= DateTimeOffset.UtcNow)
        {
            _banCache.Remove(userId);
            _expiry.Remove(userId);
            return;
        }

        // Живой остаток срока (уменьшается сдачей руды) важнее снапшота из кэша.
        _expiry.TryAdd(ev.Player.UserId, ban.ExpirationTime!.Value);

        if (HasComp<GhostComponent>(ev.Entity))
            return;

        var xform = Transform(ev.Entity);
        if (xform.MapID == _katorgaMap)
            return;

        SendToKatorga(ev.Player, ban);
    }

    private void Safeguard()
    {
        if (_katorgaMap == null)
            return;

        var query = EntityQueryEnumerator<KatorgaBoundComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != _katorgaMap)
            {
                ProtoId<KatorgaFactionPrototype>? faction = null;
                if (_playerManager.TryGetSessionByEntity(uid, out var escaped))
                    faction = _factionSystem.GetFaction(escaped.UserId) ?? _factionSystem.AssignFaction(escaped);

                SendEntityToKatorga(uid, faction);
                continue;
            }

            if (!_playerManager.TryGetSessionByEntity(uid, out var session))
                continue;

            // Срок отработан (или потерян) — выпускаем: снимаем компонент заключённого,
            // после чего снова доступны гостование, гост-роли и наблюдение.
            if (!_expiry.TryGetValue(session.UserId, out var expires) ||
                expires <= DateTimeOffset.UtcNow)
            {
                ReleaseInmate(uid, session);
                continue;
            }

            RefreshInmateHudTick(uid, comp);
        }
    }

    private void ReleaseInmate(EntityUid uid, ICommonSession session)
    {
        RemComp<KatorgaBoundComponent>(uid);
        _banCache.Remove(session.UserId);
        _expiry.Remove(session.UserId);
        _factionSystem.ClearFaction(session.UserId);
        NotifyBanStatus(session, null);

        _chatManager.DispatchServerMessage(session, Loc.GetString("katorga-released"));
        _sawmill.Info("Каторга: " + session.Name + " отработал срок и выпущен");
    }

    public void SendToKatorga(ICommonSession session, BanDef ban)
    {
        // Всегда пишем срок текущего бана: прежнее значение могло остаться от старого бана.
        _expiry[session.UserId] = ban.ExpirationTime!.Value;
        _banCache[session.UserId] = ban;

        if (_katorgaMapUid == null || _katorgaMap == null)
        {
            _sawmill.Error("Каторга: карта недоступна, игрок " + session.Name + " отключён");
            session.Channel.Disconnect(Loc.GetString("katorga-map-missing"));
            return;
        }

        // Фракция выбирается в late join; если игрок не выбрал (или не успел), назначаем сами.
        var faction = _factionSystem.GetFaction(session.UserId) ?? _factionSystem.AssignFaction(session);

        if (session.AttachedEntity is { } attached && !TerminatingOrDeleted(attached))
        {
            // Переподключение в уже заключённое тело: не раздеваем и не телепортируем заново,
            // иначе снаряжение падает на пол, а персонаж оказывается на другой точке спавна.
            if (HasComp<KatorgaBoundComponent>(attached) && Transform(attached).MapID == _katorgaMap)
            {
                EnsureComp<KillTrackerComponent>(attached);
            }
            else
            {
                SendEntityToKatorga(attached, faction);
            }
        }
        else
        {
            SpawnKatorgaPlayer(session, faction);
        }

        NotifyBanStatus(session, ban);

        // Остаток срока берём из живого счётчика: за время между проверками руда могла списать часть срока.
        var expires = _expiry.TryGetValue(session.UserId, out var live) ? live : ban.ExpirationTime!.Value;
        var remaining = expires - DateTimeOffset.UtcNow;
        _chatManager.DispatchServerMessage(session, Loc.GetString("katorga-greeting", ("time", KatorgaTime.Format(remaining))));

        if (_sentenceMultiplier > 1.0)
        {
            _chatManager.DispatchServerMessage(session,
                Loc.GetString("katorga-acceleration-note", ("multiplier", _sentenceMultiplier.ToString("0.##"))));
        }

        RefreshInmateHud(session, resetTotal: true);
    }

    private void SendEntityToKatorga(EntityUid entity, ProtoId<KatorgaFactionPrototype>? faction)
    {
        // Снимаем экипировку и выбрасываем содержимое рук: заключённый приходит на каторгу с пустыми руками.
        if (_inventory.TryGetContainerSlotEnumerator(entity, out var slots))
        {
            var toRemove = new List<(EntityUid Item, string Slot)>();
            while (slots.NextItem(out var item, out var slot))
            {
                if (slot.Name != null)
                    toRemove.Add((item, slot.Name));
            }

            foreach (var (item, slot) in toRemove)
            {
                if (_inventory.TryUnequip(entity, slot, silent: true, force: true))
                    _physics.ApplyAngularImpulse(item, ThrowingSystem.ThrowAngularImpulse);
            }
        }

        if (TryComp<HandsComponent>(entity, out var hands))
        {
            foreach (var hand in _hands.EnumerateHands((entity, hands)).ToList())
            {
                _hands.TryDrop((entity, hands), hand, checkActionBlocker: false, doDropInteraction: false);
            }
        }

        _transform.SetCoordinates(entity, GetSpawnPosition(faction));
        _transform.AttachToGridOrMap(entity);

        EnsureComp<KatorgaBoundComponent>(entity);
        EnsureComp<KillTrackerComponent>(entity);
        EquipInmate(entity, faction);
    }

    private void SpawnKatorgaPlayer(ICommonSession session, ProtoId<KatorgaFactionPrototype> faction)
    {
        var profile = _preferences.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;

        var mind = _mind.CreateMind(session.UserId, profile?.Name);
        _mind.SetUserId(mind, session.UserId);

        var mob = _stationSpawning.SpawnPlayerMob(GetSpawnPosition(faction), null, profile, null);
        _mind.TransferTo(mind, mob);

        EnsureComp<KatorgaBoundComponent>(mob);
        EnsureComp<KillTrackerComponent>(mob);
        EquipInmate(mob, faction);
    }

    /// <summary>
    ///     Выдаёт заключённому базовый комплект утилизатора и снаряжение фракции.
    /// </summary>
    private void EquipInmate(EntityUid mob, ProtoId<KatorgaFactionPrototype>? faction)
    {
        _stationSpawning.EquipStartingGear(mob, InmateGear);

        if (_prototype.TryIndex(faction, out var factionProto))
        {
            var member = EnsureComp<KatorgaFactionMemberComponent>(mob);
            member.Faction = factionProto.ID;
            Dirty(mob, member);
            _stationSpawning.EquipStartingGear(mob, factionProto.Gear);
        }
    }

    private EntityCoordinates GetSpawnPosition(ProtoId<KatorgaFactionPrototype>? faction)
    {
        if (faction != null &&
            _spawnPoints.TryGetValue(faction.Value.Id, out var factionPoints) &&
            factionPoints.Count > 0)
        {
            return _random.Pick(factionPoints);
        }

        // У фракции нет своих точек (второй лагерь не загрузился): общие или любые точки.
        var any = _spawnPoints.Values.SelectMany(list => list).ToList();
        if (any.Count > 0)
            return _random.Pick(any);

        if (_katorgaMapUid != null)
            return new EntityCoordinates(_katorgaMapUid.Value, System.Numerics.Vector2.Zero);

        return EntityCoordinates.Invalid;
    }

    #endregion

    #region Изоляция заключённых

    /// <summary>
    ///     Находится ли игрок на каторге (есть отрабатываемый временный бан).
    ///     Читает только кэш: без обращений к базе и блокировок главного потока.
    /// </summary>
    public bool IsConfined(ICommonSession session)
    {
        return Enabled &&
               _banCache.TryGetValue(session.UserId, out var ban) &&
               ban.ExpirationTime > DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Находится ли игрок на каторге и при этом не админ (админам можно гостоваться и наблюдать).
    /// </summary>
    public bool IsConfinedNonAdmin(ICommonSession session)
    {
        return IsConfined(session) && !_adminManager.IsAdmin(session);
    }

    /// <summary>
    ///     Онлайн-игрок только что получил бан. Если бан временный и каторга включена,
    ///     вместо кика отправляем его отбывать срок. Пермабаны и админов не трогаем.
    /// </summary>
    public bool TryConfineOnBan(ICommonSession player, BanDef ban)
    {
        if (!Enabled || ban.ExpirationTime == null || _adminManager.IsAdmin(player))
            return false;

        SendToKatorga(player, ban);
        return true;
    }

    /// <summary>
    ///     Отправляет заключённого на каторгу, если у него есть отрабатываемый бан (по данным кэша).
    /// </summary>
    public bool TryConfine(ICommonSession session)
    {
        if (!Enabled ||
            !_banCache.TryGetValue(session.UserId, out var ban) ||
            ban.ExpirationTime <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        SendToKatorga(session, ban);
        return true;
    }

    /// <summary>
    ///     Заключённым запрещено гостоваться: мёртвое тело лежит на каторге,
    ///     новое тело можно получить переподключением. Админы не ограничены.
    /// </summary>
    private void OnGhostAttempt(GhostAttemptHandleEvent ev)
    {
        if (!Enabled || ev.Handled)
            return;

        if (ev.Mind.UserId == null || !_playerManager.TryGetSessionById(ev.Mind.UserId, out var session))
            return;

        if (!IsConfined(session) || _adminManager.IsAdmin(session))
            return;

        ev.Handled = true;
        ev.Result = false;
        _chatManager.DispatchServerMessage(session, Loc.GetString("katorga-ghost-blocked"));
    }

    #endregion

    #region Проверка банов

    private void OnPlayerDisconnected(ICommonSession session)
    {
        // Следующий вход/аттач перепроверит баны заново. Кэш и отложенный спавн
        // прошлой сессии не должны пережить переподключение.
        var userId = session.UserId;
        _banCache.Remove(userId);
        _banCacheChecked.Remove(userId);
        _pendingSpawns.Remove(userId);
        _factionSystem.ClearEligible(userId);
    }

    /// <summary>
    ///     Сообщает клиенту остаток бана для лобби. Нулевой остаток значит "бана нет":
    ///     клиент вернёт кнопку персонализации и обычный late join.
    /// </summary>
    private void NotifyBanStatus(ICommonSession session, BanDef? ban)
    {
        var remaining = TimeSpan.Zero;
        if (ban?.ExpirationTime is { } expires)
        {
            remaining = expires - DateTimeOffset.UtcNow;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
        }

        _net.ServerSendMessage(new KatorgaBanStatusMessage { Remaining = remaining }, session.Channel);
    }

    /// <summary>
    ///     Вызывается GameTicker перед спавном. Если бан игрока ещё не проверен, откладывает
    ///     спавн и запускает проверку в фоне. Возвращает true, если спавн нужно прервать:
    ///     он продолжится из ApplyBanCheck после ответа БД.
    /// </summary>
    public bool TryDeferPlayerSpawn(
        ICommonSession session,
        EntityUid station,
        string? jobId,
        bool lateJoin,
        bool silent)
    {
        if (!Enabled)
            return false;

        var userId = session.UserId;
        if (_banCacheChecked.Contains(userId) || _banCache.ContainsKey(userId))
            return false;

        if (!_pendingSpawns.TryGetValue(userId, out var pending))
        {
            pending = new PendingSpawn();
            _pendingSpawns[userId] = pending;
        }

        pending.Station = station;
        pending.JobId = jobId;
        pending.LateJoin = lateJoin;
        pending.Silent = silent;

        // Если предыдущая попытка упала с ошибкой БД, ждём RetryAt, иначе стартуем сразу.
        if (pending.RetryAt <= _timing.CurTime)
            StartBanCheck(session);

        return true;
    }

    private void StartBanCheck(ICommonSession session)
    {
        if (!_banChecksInFlight.Add(session.UserId))
            return;

        _ = RefreshBanCacheAsync(session);
    }

    /// <summary>
    ///     Повторяет проверки, которые упали с ошибкой БД. Работает на главном потоке,
    ///     поэтому отложенные спавны и кэш не трогаются из фоновых потоков.
    /// </summary>
    private void ProcessPendingBanChecks()
    {
        if (_pendingSpawns.Count == 0)
            return;

        // Каторгу выключили, пока игроки ждали проверку: задерживать спавн больше незачем.
        if (!Enabled)
        {
            ReleasePendingSpawns();
            return;
        }

        var now = _timing.CurTime;
        List<NetUserId>? due = null;
        foreach (var (userId, pending) in _pendingSpawns)
        {
            if (pending.RetryAt > now || _banChecksInFlight.Contains(userId))
                continue;

            (due ??= new List<NetUserId>()).Add(userId);
        }

        if (due == null)
            return;

        foreach (var userId in due)
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
            {
                _pendingSpawns.Remove(userId);
                continue;
            }

            StartBanCheck(session);
        }
    }

    /// <summary>
    ///     Продолжает отложенные спавны, когда каторга выключена: проверка бана больше не нужна,
    ///     обычные правила входа отработали ещё на подключении.
    /// </summary>
    private void ReleasePendingSpawns()
    {
        var pending = _pendingSpawns.ToList();
        _pendingSpawns.Clear();

        foreach (var (userId, spawn) in pending)
        {
            if (!_playerManager.TryGetSessionById(userId, out var session))
                continue;

            EntityManager.System<GameTicker>().MakeJoinGame(
                session,
                spawn.Station,
                spawn.JobId,
                spawn.Silent,
                spawn.LateJoin);
        }
    }

    /// <summary>
    ///     Асинхронно проверяет баны игрока и обновляет кэш. Единственное обращение каторги к базе:
    ///     вызывается при входе в лобби и, если кэш ещё не готов, перед спавном. Сама проверка идёт
    ///     в фоне, результат применяется на главном потоке: игровой поток не блокируется.
    /// </summary>
    private async Task RefreshBanCacheAsync(ICommonSession session)
    {
        var userId = session.UserId;
        BanDef? binding = null;
        Exception? error = null;

        try
        {
            // Данные канала читаем на главном потоке, до ухода в фон.
            var address = session.Channel.RemoteEndPoint.Address;
            var hwId = session.Channel.UserData.HWId.Length == 0
                ? null
                : (ImmutableArray<byte>?)session.Channel.UserData.HWId;
            var modernHwIds = session.Channel.UserData.ModernHWIds;

            binding = await LookupBindingBanAsync(address, userId, hwId, modernHwIds);
        }
        catch (Exception e)
        {
            error = e;
        }

        _taskManager.RunOnMainThread(() => ApplyBanCheck(userId, binding, error));
    }

    /// <summary>
    ///     Применяет результат фоновой проверки на главном потоке. Ошибки БД не роняют раунд:
    ///     отложенный спавн повторяется несколько раз, после чего непроверенный игрок
    ///     не допускается в раунд.
    /// </summary>
    private void ApplyBanCheck(NetUserId userId, BanDef? binding, Exception? error)
    {
        _banChecksInFlight.Remove(userId);

        if (error != null)
        {
            _sawmill.Error("Каторга: не удалось проверить баны игрока " + userId + ": " + error);

            if (!_pendingSpawns.TryGetValue(userId, out var retry))
                return;

            // Каторгу выключили, пока шла проверка: больше не задерживаем спавн.
            if (!Enabled)
            {
                if (_pendingSpawns.Remove(userId, out var disabledPending) &&
                    _playerManager.TryGetSessionById(userId, out var disabledPlayer))
                {
                    EntityManager.System<GameTicker>().MakeJoinGame(
                        disabledPlayer,
                        disabledPending.Station,
                        disabledPending.JobId,
                        disabledPending.Silent,
                        disabledPending.LateJoin);
                }

                return;
            }

            retry.Attempts++;
            if (retry.Attempts < MaxBanCheckAttempts)
            {
                retry.RetryAt = _timing.CurTime + BanCheckRetryDelay;
                return;
            }

            _pendingSpawns.Remove(userId);
            if (_playerManager.TryGetSessionById(userId, out var failed))
            {
                _chatManager.DispatchServerMessage(failed, Loc.GetString("katorga-ban-check-failed"));
                failed.Channel.Disconnect(Loc.GetString("katorga-ban-check-failed"));
            }

            return;
        }

        // Пока шёл запрос, игрок мог выйти.
        if (!_playerManager.TryGetSessionById(userId, out var player))
        {
            _pendingSpawns.Remove(userId);
            return;
        }

        _banCacheChecked.Add(userId);

        if (binding == null || binding.ExpirationTime <= DateTimeOffset.UtcNow)
        {
            _banCache.Remove(userId);
            _expiry.Remove(userId);
        }
        else
        {
            _banCache[userId] = binding;
            // Срок текущего бана: перечитывание ресинхронизирует живой счётчик с базой.
            _expiry[userId] = binding.ExpirationTime!.Value;
        }

        NotifyBanStatus(player, binding);

        // Ждёт в лобби с отрабатываемым баном: выбор фракции каторги откроется в late join.
        // Пока окно не открыто, отсчёт не идёт: иначе он сгорал бы ещё до начала раунда.
        if (binding != null && binding.ExpirationTime > DateTimeOffset.UtcNow &&
            !EntityManager.System<GameTicker>().UserHasJoinedGame(player))
        {
            _factionSystem.MakeEligible(player);
        }
        else
        {
            _factionSystem.ClearEligible(userId);
        }

        // Если игрок ждал проверку перед спавном, продолжаем прерванный спавн.
        // Повторный вход в SpawnPlayer увидит готовый кэш: забаненный уйдёт на каторгу,
        // остальные заспавнятся обычным путём.
        if (_pendingSpawns.Remove(userId, out var pending))
        {
            EntityManager.System<GameTicker>().MakeJoinGame(
                player,
                pending.Station,
                pending.JobId,
                pending.Silent,
                pending.LateJoin);

            return;
        }

        if (!Enabled || binding == null || binding.ExpirationTime <= DateTimeOffset.UtcNow)
            return;

        // Уже в раунде (зашёл мимо лобби или бан выдали во время игры), отправляем отбывать.
        if (player.AttachedEntity is { } attached && !TerminatingOrDeleted(attached) &&
            !HasComp<GhostComponent>(attached))
        {
            SendToKatorga(player, binding);
        }
        else
        {
            _chatManager.DispatchServerMessage(player, Loc.GetString("katorga-lobby-message"));
        }
    }

    /// <summary>
    ///     Ищет активный серверный бан игрока, который можно отработать на каторге.
    ///     Возвращает null, если банов нет или среди них есть перманентный:
    ///     пермабаны отработке не подлежат, их отсекает ещё ConnectionManager.
    ///     Если временных банов несколько, отрабатывается самый долгий (связывающий) срок.
    /// </summary>
    private async Task<BanDef?> LookupBindingBanAsync(
        IPAddress? address,
        NetUserId userId,
        ImmutableArray<byte>? hwId,
        ImmutableArray<ImmutableArray<byte>>? modernHwIds)
    {
        var bans = await _db.GetBansAsync(
            address,
            userId,
            hwId,
            modernHwIds,
            includeUnbanned: false,
            BanType.Server);

        BanDef? binding = null;
        foreach (var b in bans)
        {
            // Перманентный бан отработке не подлежит.
            if (b.ExpirationTime == null)
                return null;

            if (binding == null || b.ExpirationTime.Value > binding.ExpirationTime!.Value)
                binding = b;
        }

        return binding;
    }

    #endregion

    #region Изменение срока

    private void OnKillReported(ref KillReportedEvent ev)
    {
        if (!Enabled)
            return;

        // Фауна каторги: убийство сокращает срок тому, кто убил.
        if (TryComp<KatorgaFaunaComponent>(ev.Entity, out var fauna))
        {
            if (!ev.Suicide && ev.Primary is KillPlayerSource faunaKiller)
                ApplyFaunaReward(faunaKiller.PlayerId, fauna.RewardMinutes);
            return;
        }

        if (!HasComp<KatorgaBoundComponent>(ev.Entity))
            return;

        // Суицид не наказываем: заключённый сам распоряжается своим сроком.
        if (ev.Suicide)
            return;

        if (ev.Primary is not KillPlayerSource source)
            return;

        // Правила фракций: убийство одноФракционника - штраф, чужоФракционника - награда.
        // Убийца не заключённый (админ на карте) - обычный штраф, как и раньше.
        if (_playerManager.TryGetSessionById(source.PlayerId, out var killerSession) &&
            killerSession.AttachedEntity is { } killer &&
            HasComp<KatorgaBoundComponent>(killer) &&
            TryComp<KatorgaFactionMemberComponent>(killer, out var killerFaction) &&
            TryComp(ev.Entity, out KatorgaFactionMemberComponent? victimFaction) &&
            killerFaction.Faction != victimFaction.Faction)
        {
            var rewardMinutes = _cfg.GetCVar(MiniCCVars.KatorgaCrossKillRewardMinutes);
            if (rewardMinutes > 0)
                ApplySentenceReward(source.PlayerId, TimeSpan.FromMinutes(rewardMinutes), "katorga-cross-kill-reward");

            return;
        }

        var minutes = _cfg.GetCVar(MiniCCVars.KatorgaKillPenaltyMinutes);
        if (minutes <= 0)
            return;

        _ = ChangeBanTimeAsync(source.PlayerId, TimeSpan.FromMinutes(minutes), penalty: true);
    }

    /// <summary>
    ///     Награда за убитую тварь: сокращает живой счётчик срока и списывает в базу.
    /// </summary>
    private void ApplyFaunaReward(NetUserId userId, int minutes)
    {
        ApplySentenceReward(userId, TimeSpan.FromMinutes(minutes), "katorga-fauna-reward");
    }

    /// <summary>
    ///     Сокращает срок наградой: живой счётчик сразу, база - фоном. Ничего не делает,
    ///     если игрок сейчас не отрабатывает бан.
    /// </summary>
    private void ApplySentenceReward(NetUserId userId, TimeSpan reduced, string messageKey)
    {
        if (reduced <= TimeSpan.Zero || !_expiry.ContainsKey(userId))
            return;

        _expiry[userId] -= reduced;

        if (_playerManager.TryGetSessionById(userId, out var session))
            AddWorkedOff(session, reduced);

        _ = ChangeBanTimeAsync(userId, -reduced, penalty: false, messageKey: messageKey);
    }

    private void OnOreInserted(EntityUid uid, KatorgaOreProcessorComponent component, ref MaterialEntityInsertedEvent args)
    {
        if (!Enabled)
            return;

        if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        if (!TryComp<MaterialStorageComponent>(uid, out var storage))
            return;

        double points = 0;
        foreach (var (materialId, volume) in storage.Storage)
        {
            if (!_prototype.TryIndex<MaterialPrototype>(materialId, out var materialProto))
                continue;

            var sheetVolume = _materialStorage.GetSheetVolume(materialProto);
            if (sheetVolume <= 0)
                continue;

            points += materialProto.Price * (double)volume / sheetVolume;
            _materialStorage.TrySetMaterialAmount(uid, materialId, 0, storage);
        }

        if (points <= 0)
            return;

        _points[session.UserId] = _points.GetValueOrDefault(session.UserId) + points;

        var reduced = ConvertPointsToTime(points);
        if (_expiry.ContainsKey(session.UserId))
            _expiry[session.UserId] -= reduced;
        AddWorkedOff(session, reduced);

        _popup.PopupEntity(
            Loc.GetString("katorga-time-reduced", ("time", KatorgaTime.Format(ConvertPointsToTime(points)))),
            uid,
            args.User,
            PopupType.Medium);
    }

    /// <summary>
    ///     Рыбалка на каторге тоже отрабатывает срок: каждая пойманная рыба его сокращает.
    /// </summary>
    private void OnFishCaught(Entity<FishingRodComponent> ent, ref FishCaughtWithRodEvent args)
    {
        if (!Enabled || _fishRewardMinutes <= 0)
            return;

        if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        var reduced = TimeSpan.FromMinutes(_fishRewardMinutes);
        if (!_expiry.ContainsKey(session.UserId))
            return;

        ApplySentenceReward(session.UserId, reduced, "katorga-fish-reward");

        _popup.PopupEntity(
            Loc.GetString("katorga-time-reduced", ("time", KatorgaTime.Format(reduced))),
            args.User,
            args.User,
            PopupType.Medium);
    }

    /// <summary>
    ///     Списывает накопленное ускорение срока: живой счётчик уже уменьшен,
    ///     в базу уходит молча, без сообщений игроку.
    /// </summary>
    private void FlushAcceleration()
    {
        if (_accelPending.Count == 0)
            return;

        var snapshot = new Dictionary<NetUserId, double>(_accelPending);
        _accelPending.Clear();

        foreach (var (userId, seconds) in snapshot)
        {
            if (seconds < 1)
                continue;

            _ = ChangeBanTimeAsync(userId, TimeSpan.FromSeconds(-seconds), penalty: false, silent: true);
        }
    }

    /// <summary>
    ///     Списывает накопленные очки в базу: уменьшает срок бана каждому отрабатывающему.
    /// </summary>
    private void FlushPoints()
    {
        if (_points.Count == 0)
            return;

        var snapshot = new Dictionary<NetUserId, double>(_points);
        _points.Clear();

        foreach (var (userId, earned) in snapshot)
        {
            var seconds = ConvertPointsToTime(earned);
            if (seconds < TimeSpan.FromSeconds(1))
                continue;

            _ = ChangeBanTimeAsync(userId, -seconds, penalty: false);
        }
    }

    /// <summary>
    ///     Меняет срок самого долгого активного временного бана игрока. Перманентные баны не трогает.
    /// </summary>
    private async Task ChangeBanTimeAsync(
        NetUserId userId,
        TimeSpan delta,
        bool penalty,
        bool silent = false,
        string? messageKey = null)
    {
        try
        {
            var bans = await _db.GetBansAsync(null, userId, null, null, includeUnbanned: false, BanType.Server);

            BanDef? binding = null;
            foreach (var b in bans)
            {
                // Перманентные баны отработке не подлежат.
                if (b.ExpirationTime == null)
                    continue;

                if (binding == null || b.ExpirationTime.Value > binding.ExpirationTime!.Value)
                    binding = b;
            }

            if (binding?.ExpirationTime == null)
                return;

            var newExpiration = binding.ExpirationTime.Value + delta;
            if (newExpiration < DateTimeOffset.UtcNow)
                newExpiration = DateTimeOffset.UtcNow;

            // Штраф увеличивает срок в базе: живой счётчик нужно подтянуть.
            // Награды и ускорение уже уменьшили счётчик заранее, переприсвоение им не нужно.
            if (penalty && _expiry.ContainsKey(userId))
                _expiry[userId] = newExpiration;

            await _db.SetBanExpirationAsync(binding.Id!.Value, newExpiration);

            _sawmill.Info(
                "Каторга: бан " + binding.Id + " игрока " + userId + " изменён на " + delta.TotalSeconds.ToString("N0")
                + " с, новый срок: " + newExpiration.ToString("u"));

            if (silent || !_playerManager.TryGetSessionById(userId, out var session))
                return;

            if (penalty)
            {
                _chatManager.DispatchServerMessage(session,
                    Loc.GetString("katorga-penalty-message", ("time", KatorgaTime.Format(delta))));
            }
            else
            {
                _chatManager.DispatchServerMessage(session,
                    Loc.GetString(messageKey ?? "katorga-time-reduced", ("time", KatorgaTime.Format(-delta))));
            }

            if (penalty)
            {
                _chatManager.SendAdminAlert(Loc.GetString(
                    "katorga-penalty-admin",
                    ("user", userId.ToString()),
                    ("time", KatorgaTime.Format(delta))));
            }
        }
        catch (Exception e)
        {
            _sawmill.Error("Каторга: не удалось изменить срок бана игроку " + userId + ": " + e);
        }
    }

    #region Худ заключённого

    /// <summary>
    ///     Полностью пересобирает данные худа: при прибытии на каторгу.
    /// </summary>
    private void RefreshInmateHud(ICommonSession session, bool resetTotal)
    {
        if (session.AttachedEntity is not { } mob || Deleted(mob))
            return;

        if (!_expiry.TryGetValue(session.UserId, out var expires))
            return;

        EnsureComp<KatorgaBoundComponent>(mob, out var comp);

        var remaining = expires - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        if (resetTotal || comp.SentenceTotal == TimeSpan.Zero)
            comp.SentenceTotal = remaining;

        comp.SentenceRemaining = remaining;
        Dirty(mob, comp);
    }

    /// <summary>
    ///     Периодическое обновление худа: срок течёт сам по себе, даже если руду не копают.
    /// </summary>
    private void RefreshInmateHudTick(EntityUid uid, KatorgaBoundComponent comp)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;

        if (!_expiry.TryGetValue(session.UserId, out var expires))
            return;

        var remaining = expires - DateTimeOffset.UtcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        if ((comp.SentenceRemaining - remaining).Duration() <= TimeSpan.FromSeconds(5))
            return;

        comp.SentenceRemaining = remaining;
        Dirty(uid, comp);
    }

    /// <summary>
    ///     Отмечает отработку рудой в худе заключённого.
    /// </summary>
    private void AddWorkedOff(ICommonSession session, TimeSpan reduced)
    {
        if (session.AttachedEntity is not { } mob || Deleted(mob))
            return;

        EnsureComp<KatorgaBoundComponent>(mob, out var comp);
        comp.WorkedOff += reduced;
        comp.SentenceRemaining = comp.SentenceRemaining > reduced
            ? comp.SentenceRemaining - reduced
            : TimeSpan.Zero;
        Dirty(mob, comp);
    }

    #endregion

    private TimeSpan ConvertPointsToTime(double points)
    {
        return TimeSpan.FromSeconds(points / Math.Max(_pointsToTime, 0.0001));
    }

    #endregion
}
