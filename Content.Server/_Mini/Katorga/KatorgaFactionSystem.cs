// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;

using Content.Server.Chat.Managers;
using Content.Shared.GameTicking;
using Content.Server.EUI;
using Content.Server._Mini.Katorga.Components;
using Content.Shared._Mini.Katorga;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Configuration;
using Content.Shared._Mini.MiniCCVars;
using Robust.Shared.Prototypes;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Katorga;

/// <summary>
///     Фракции каторги: заключённый выбирает фракцию в лобби (EUI, с таймером),
///     фракция определяет снаряжение и точку спавна. Убийство одноФракционника
///     наказывается, чужоФракционника — вознаграждается (правила килов в KatorgaSystem).
///     Если игрок не выбрал фракцию за отведённое время или ушёл в раунд раньше,
///     фракция назначается автоматически: в наименее населённую.
/// </summary>
public sealed class KatorgaFactionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // Фракция живёт до конца раунда: переподключение выбор не сбрасывает.
    private readonly Dictionary<NetUserId, ProtoId<KatorgaFactionPrototype>> _factions = new();
    private readonly Dictionary<NetUserId, TimeSpan> _deadlines = new();
    private readonly Dictionary<NetUserId, KatorgaFactionEui> _euis = new();

    private int _selectionSeconds;
    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MiniCCVars.KatorgaFactionSelectionSeconds, v => _selectionSeconds = Math.Clamp(v, 5, 120), true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_deadlines.Count == 0 || _timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(1);

        foreach (var (userId, deadline) in _deadlines.ToArray())
        {
            if (!_player.TryGetSessionById(userId, out var session) || session.Status == SessionStatus.Disconnected)
            {
                _deadlines.Remove(userId);
                continue;
            }

            if (_timing.CurTime < deadline)
            {
                if (_euis.TryGetValue(userId, out var eui) && !eui.IsShutDown)
                    eui.StateDirty();
                continue;
            }

            // Время вышло: назначаем сами и закрываем окно.
            AssignFaction(session);
            if (_euis.Remove(userId, out var expired) && !expired.IsShutDown)
                expired.Close();
        }
    }

    public ProtoId<KatorgaFactionPrototype>? GetFaction(NetUserId userId)
    {
        if (_factions.TryGetValue(userId, out var faction))
            return faction;

        return null;
    }

    /// <summary>
    ///     Открывает окно выбора фракции в лобби. Выбор уже сделан — ничего не делает.
    /// </summary>
    public void BeginSelection(ICommonSession session)
    {
        var userId = session.UserId;
        if (_factions.ContainsKey(userId) || _deadlines.ContainsKey(userId))
            return;

        if (_euis.TryGetValue(userId, out var existing) && !existing.IsShutDown)
            return;

        _deadlines[userId] = _timing.CurTime + TimeSpan.FromSeconds(_selectionSeconds);
        var eui = new KatorgaFactionEui(this, session);
        _euis[userId] = eui;
        _eui.OpenEui(eui, session);
    }

    /// <summary>
    ///     Назначает фракцию в наименее населённую (среди фракций, у которых есть точки спавна)
    ///     и сообщает игроку. Повторный вызов ничего не меняет.
    /// </summary>
    public ProtoId<KatorgaFactionPrototype> AssignFaction(ICommonSession session)
    {
        if (_factions.TryGetValue(session.UserId, out var assigned))
            return assigned;

        var available = new List<ProtoId<KatorgaFactionPrototype>>();
        foreach (var faction in _prototype.EnumeratePrototypes<KatorgaFactionPrototype>())
        {
            if (HasSpawnPoints(faction))
                available.Add(faction.ID);
        }

        if (available.Count == 0)
        {
            // Карты каторги нет или спавны не найдены: берём первую фракцию, чтобы
            // игрок не выпадал из системы. Спавн упадёт на общую точку, если есть.
            foreach (var faction in _prototype.EnumeratePrototypes<KatorgaFactionPrototype>())
            {
                available.Add(faction.ID);
                break;
            }
        }

        var counts = new Dictionary<ProtoId<KatorgaFactionPrototype>, int>();
        foreach (var (userId, faction) in _factions)
        {
            if (_player.TryGetSessionById(userId, out _))
                counts[faction] = counts.GetValueOrDefault(faction) + 1;
        }

        var minimum = available.Min(faction => counts.GetValueOrDefault(faction));
        var candidates = available.Where(faction => counts.GetValueOrDefault(faction) == minimum).ToList();
        var picked = _random.Pick(candidates);

        _factions[session.UserId] = picked;
        _deadlines.Remove(session.UserId);

        var proto = _prototype.Index(picked);
        _chat.DispatchServerMessage(session, Loc.GetString("katorga-faction-assigned", ("faction", Loc.GetString(proto.Name))));

        return picked;
    }

    public bool TrySelectFaction(ICommonSession session, string factionId)
    {
        var userId = session.UserId;
        if (!_factions.TryGetValue(userId, out var selected) &&
            !_deadlines.ContainsKey(userId))
        {
            return false;
        }

        if (!_prototype.TryIndex<KatorgaFactionPrototype>(factionId, out var faction) ||
            !HasSpawnPoints(faction.ID))
        {
            return false;
        }

        // Уже выбрал другую фракцию (или автоназначение уже произошло) — перебирать нельзя.
        if (_factions.TryGetValue(userId, out var existing) && existing != faction.ID)
            return false;

        if (selected != faction.ID)
        {
            _factions[userId] = faction.ID;
            _deadlines.Remove(userId);
            _chat.DispatchServerMessage(session, Loc.GetString("katorga-faction-selected", ("faction", Loc.GetString(faction.Name))));
        }

        if (_euis.Remove(userId, out var eui) && !eui.IsShutDown)
            eui.Close();

        return true;
    }

    public KatorgaFactionEuiState GetEuiState(ICommonSession session)
    {
        var options = _prototype.EnumeratePrototypes<KatorgaFactionPrototype>()
            .OrderBy(faction => faction.Order)
            .ThenBy(faction => faction.ID)
            .Select(faction => new KatorgaFactionOption(
                faction.ID,
                Loc.GetString(faction.Name),
                Loc.GetString(faction.Feature),
                faction.Color))
            .ToList();

        var seconds = 0;
        if (_deadlines.TryGetValue(session.UserId, out var deadline))
            seconds = Math.Max(0, (int) Math.Ceiling((deadline - _timing.CurTime).TotalSeconds));

        return new KatorgaFactionEuiState
        {
            Options = options,
            SecondsRemaining = seconds,
        };
    }

    internal void OnEuiClosed(ICommonSession session, KatorgaFactionEui eui)
    {
        if (_euis.GetValueOrDefault(session.UserId) == eui)
            _euis.Remove(session.UserId);
    }

    /// <summary>Снимает заключённого с учёта фракций: игрок отработал срок и выпущен.</summary>
    public void ClearFaction(NetUserId userId)
    {
        _factions.Remove(userId);
        _deadlines.Remove(userId);
        if (_euis.Remove(userId, out var eui) && !eui.IsShutDown)
            eui.Close();
    }

    private bool HasSpawnPoints(ProtoId<KatorgaFactionPrototype> faction)
    {
        var query = EntityQueryEnumerator<KatorgaSpawnPointComponent>();
        while (query.MoveNext(out var spawn))
        {
            if (spawn.Faction == faction)
                return true;
        }

        return false;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var eui in _euis.Values.ToArray())
        {
            if (!eui.IsShutDown)
                eui.Close();
        }

        _factions.Clear();
        _deadlines.Clear();
        _euis.Clear();
    }
}
