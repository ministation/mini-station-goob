using System.Collections.Immutable;
using System.Linq;
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
using Content.Server.Station.Systems;
using Content.Shared._Mini.Katorga.Components;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Materials;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
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
    private const string KatorgaBiomeId = "KatorgaBiome";

    private static readonly TimeSpan SafeguardInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMinutes(1);

    [Dependency] private readonly AdminSystem _adminSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    private MapId? _katorgaMap;
    private EntityUid? _katorgaMapUid;
    private readonly List<EntityCoordinates> _spawnPoints = new();
    private readonly Dictionary<NetUserId, double> _points = new();

    // Текущий срок (мгновенный взгляд сервера): уменьшается сразу при сдаче руды, в базу списывается пачками.
    private readonly Dictionary<NetUserId, DateTimeOffset> _expiry = new();

    private TimeSpan _nextSafeguard = TimeSpan.Zero;
    private TimeSpan _nextFlush = TimeSpan.Zero;
    private double _pointsToTime;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("katorga");
        _cfg.OnValueChanged(MiniCCVars.KatorgaPointsToTime, v => _pointsToTime = v, true);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnJoinedLobby);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
        SubscribeLocalEvent<KatorgaOreProcessorComponent, MaterialEntityInsertedEvent>(OnOreInserted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

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
            FlushPoints();
        }
    }

    private bool Enabled => _cfg.GetCVar(MiniCCVars.KatorgaEnabled);

    #region События раунда

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _katorgaMap = null;
        _katorgaMapUid = null;
        _spawnPoints.Clear();

        if (!Enabled)
            return;

        var opts = new MapLoadOptions
        {
            DeserializationOptions = DeserializationOptions.Default with { PauseMaps = false },
            ExpectedCategory = FileCategory.Map
        };

        if (!_mapLoader.TryLoadGeneric(KatorgaMapPath, out var result, opts) || !result.Maps.TryFirstOrNull(out var map))
        {
            _sawmill.Error("Каторга: не удалось загрузить карту " + KatorgaMapPath + ", каторга в этом раунде недоступна");
            return;
        }

        _katorgaMapUid = map.Value.Owner;
        _katorgaMap = map.Value.Comp.MapId;
        _metaData.SetEntityName(_katorgaMapUid.Value, "Katorga");

        if (_prototype.TryIndex<BiomeTemplatePrototype>(KatorgaBiomeId, out var biome))
        {
            _biome.EnsurePlanet(_katorgaMapUid.Value, biome);
        }
        else
        {
            _sawmill.Error("Каторга: не найден биом " + KatorgaBiomeId);
        }

        var query = EntityQueryEnumerator<KatorgaSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == _katorgaMap)
                _spawnPoints.Add(xform.Coordinates);
        }

        _sawmill.Info("Каторга: карта загружена, точек спавна: " + _spawnPoints.Count);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        // Накопленные очки нужно списать до очистки состояния, иначе отработка пропадёт.
        FlushPoints();
        _points.Clear();
        _expiry.Clear();

        _katorgaMap = null;
        _katorgaMapUid = null;
        _spawnPoints.Clear();
    }

    #endregion

    #region Отправка на каторгу

    private void OnJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (!Enabled)
            return;

        if (!TryGetKatorgaBan(ev.PlayerSession, out _))
            return;

        _chatManager.DispatchServerMessage(ev.PlayerSession, Loc.GetString("katorga-lobby-message"));
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (!Enabled)
            return;

        if (!TryGetKatorgaBan(ev.Player, out var ban))
            return;

        _expiry[ev.Player.UserId] = ban.ExpirationTime!.Value;
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

        if (!TryGetKatorgaBan(ev.Player, out var ban))
            return;

        _expiry[ev.Player.UserId] = ban.ExpirationTime!.Value;

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
                SendEntityToKatorga(uid);
                continue;
            }

            RefreshInmateHudTick(uid, comp);
        }
    }

    public void SendToKatorga(ICommonSession session, BanDef ban)
    {
        _expiry[session.UserId] = ban.ExpirationTime!.Value;

        if (_katorgaMapUid == null || _katorgaMap == null)
        {
            _sawmill.Error("Каторга: карта недоступна, игрок " + session.Name + " отключён");
            session.Channel.Disconnect(Loc.GetString("katorga-map-missing"));
            return;
        }

        if (session.AttachedEntity is { } attached && !TerminatingOrDeleted(attached))
        {
            SendEntityToKatorga(attached);
        }
        else
        {
            SpawnKatorgaPlayer(session);
        }

        var remaining = (ban.ExpirationTime ?? DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow;
        _chatManager.DispatchServerMessage(session, Loc.GetString("katorga-greeting", ("time", FormatTime(remaining))));

        RefreshInmateHud(session, resetTotal: true);
    }

    private void SendEntityToKatorga(EntityUid entity)
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

        _transform.SetCoordinates(entity, GetSpawnPosition());
        _transform.AttachToGridOrMap(entity);

        EnsureComp<KatorgaBoundComponent>(entity);
        EnsureComp<KillTrackerComponent>(entity);
    }

    private void SpawnKatorgaPlayer(ICommonSession session)
    {
        var profile = _preferences.GetPreferences(session.UserId).SelectedCharacter as HumanoidCharacterProfile;

        var mind = _mind.CreateMind(session.UserId, profile?.Name);
        _mind.SetUserId(mind, session.UserId);

        var mob = _stationSpawning.SpawnPlayerMob(GetSpawnPosition(), null, profile, null);
        _mind.TransferTo(mind, mob);

        EnsureComp<KatorgaBoundComponent>(mob);
        EnsureComp<KillTrackerComponent>(mob);
    }

    private EntityCoordinates GetSpawnPosition()
    {
        if (_spawnPoints.Count > 0)
            return _random.Pick(_spawnPoints);

        if (_katorgaMapUid != null)
            return new EntityCoordinates(_katorgaMapUid.Value, System.Numerics.Vector2.Zero);

        return EntityCoordinates.Invalid;
    }

    #endregion

    #region Изоляция заключённых

    /// <summary>
    ///     Находится ли игрок на каторге (есть отрабатываемый временный бан).
    /// </summary>
    public bool IsConfined(ICommonSession session)
    {
        return Enabled && TryGetKatorgaBan(session, out _);
    }

    /// <summary>
    ///     Находится ли игрок на каторге и при этом не админ (админам можно гостоваться и наблюдать).
    /// </summary>
    public bool IsConfinedNonAdmin(ICommonSession session)
    {
        return IsConfined(session) && !_adminManager.IsAdmin(session);
    }

        /// <summary>
    ///     Отправляет заключённого на каторгу, если у него есть отрабатываемый бан.
    /// </summary>
    public bool TryConfine(ICommonSession session)
    {
        if (!Enabled || !TryGetKatorgaBan(session, out var ban))
            return false;

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

    /// <summary>
    ///     Ищет активный серверный бан игрока, который можно отработать на каторге.
    ///     Возвращает false, если банов нет или среди них есть перманентный:
    ///     пермабаны отработке не подлежат, их отсекает ещё ConnectionManager.
    ///     Если временных банов несколько, отрабатывается самый долгий (связывающий) срок.
    /// </summary>
    public bool TryGetKatorgaBan(ICommonSession session, out BanDef ban)
    {
        ban = null!;

        var hwId = session.Channel.UserData.HWId.Length == 0
            ? null
            : (ImmutableArray<byte>?)session.Channel.UserData.HWId;
        var modernHwIds = session.Channel.UserData.ModernHWIds;

        var bans = _db.GetBansAsync(
            session.Channel.RemoteEndPoint.Address,
            session.UserId,
            hwId,
            modernHwIds,
            includeUnbanned: false,
            BanType.Server).GetAwaiter().GetResult();

        if (bans.Count == 0)
            return false;

        BanDef? binding = null;
        foreach (var b in bans)
        {
            // Перманентный бан отработке не подлежит.
            if (b.ExpirationTime == null)
                return false;

            if (binding == null || b.ExpirationTime.Value > binding.ExpirationTime!.Value)
                binding = b;
        }

        if (binding?.ExpirationTime == null)
            return false;

        ban = binding;
        return true;
    }

    #endregion

    #region Изменение срока

    private void OnKillReported(ref KillReportedEvent ev)
    {
        if (!Enabled)
            return;

        if (!HasComp<KatorgaBoundComponent>(ev.Entity))
            return;

        // Суицид не наказываем: заключённый сам распоряжается своим сроком.
        if (ev.Suicide)
            return;

        if (ev.Primary is not KillPlayerSource source)
            return;

        var minutes = _cfg.GetCVar(MiniCCVars.KatorgaKillPenaltyMinutes);
        if (minutes <= 0)
            return;

        _ = ChangeBanTimeAsync(source.PlayerId, TimeSpan.FromMinutes(minutes), penalty: true);
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
            Loc.GetString("katorga-time-reduced", ("time", FormatTime(ConvertPointsToTime(points)))),
            uid,
            args.User,
            PopupType.Medium);
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
    private async Task ChangeBanTimeAsync(NetUserId userId, TimeSpan delta, bool penalty)
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

            if (penalty && _expiry.ContainsKey(userId))
                _expiry[userId] = newExpiration;

            await _db.SetBanExpirationAsync(binding.Id!.Value, newExpiration);

            _sawmill.Info(
                "Каторга: бан " + binding.Id + " игрока " + userId + " изменён на " + delta.TotalSeconds.ToString("N0")
                + " с, новый срок: " + newExpiration.ToString("u"));

            if (_playerManager.TryGetSessionById(userId, out var session))
            {
                if (penalty)
                {
                    _chatManager.DispatchServerMessage(session,
                        Loc.GetString("katorga-penalty-message", ("time", FormatTime(delta))));
                }
                else
                {
                    _chatManager.DispatchServerMessage(session,
                        Loc.GetString("katorga-time-reduced", ("time", FormatTime(-delta))));
                }
            }

            if (penalty)
            {
                _chatManager.SendAdminAlert(Loc.GetString(
                    "katorga-penalty-admin",
                    ("user", userId.ToString()),
                    ("time", FormatTime(delta))));
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

    private static string FormatTime(TimeSpan time)
    {
        time = time.Duration();
        if (time.TotalDays >= 1)
            return string.Format("{0} д {1} ч", (int)time.TotalDays, time.Hours);
        if (time.TotalHours >= 1)
            return string.Format("{0} ч {1} мин", (int)time.TotalHours, time.Minutes);
        if (time.TotalMinutes >= 1)
            return string.Format("{0} мин {1} с", (int)time.TotalMinutes, time.Seconds);
        return string.Format("{0} с", (int)time.TotalSeconds);
    }

    #endregion
}
