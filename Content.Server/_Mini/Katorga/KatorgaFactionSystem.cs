// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;

using Content.Server.Chat.Managers;
using Content.Shared.GameTicking;
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
///     Фракции каторги: заключённый с отрабатываемым баном выбирает фракцию в окне позднего
///     подключения, фракция определяет снаряжение и точку спавна. Отсчёт на выбор идёт с момента,
///     когда игрок открыл late join, после чего фракция назначается автоматически: в наименее
///     населённую. Убийство одноФракционника наказывается, чужоФракционника — вознаграждается
///     (правила килов в KatorgaSystem).
/// </summary>
public sealed class KatorgaFactionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // Фракция живёт до конца раунда: переподключение выбор не сбрасывает.
    private readonly Dictionary<NetUserId, ProtoId<KatorgaFactionPrototype>> _factions = new();
    private readonly Dictionary<NetUserId, TimeSpan> _deadlines = new();

    // Игрок с отрабатываемым баном в лобби: выбор фракции открыт ему в late join.
    private readonly HashSet<NetUserId> _eligible = new();

    private int _selectionSeconds;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, MiniCCVars.KatorgaFactionSelectionSeconds, v => _selectionSeconds = Math.Clamp(v, 5, 120), true);
        SubscribeNetworkEvent<KatorgaFactionStateRequestEvent>(OnStateRequest);
        SubscribeNetworkEvent<KatorgaFactionSelectEvent>(OnSelect);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_deadlines.Count == 0)
            return;

        foreach (var (userId, deadline) in _deadlines.ToArray())
        {
            if (!_player.TryGetSessionById(userId, out var session) || session.Status == SessionStatus.Disconnected)
            {
                _deadlines.Remove(userId);
                continue;
            }

            if (_timing.CurTime < deadline)
                continue;

            // Время вышло: назначаем сами, клиент получит новое состояние и перестанет ждать.
            AssignFaction(session);
        }
    }

    public ProtoId<KatorgaFactionPrototype>? GetFaction(NetUserId userId)
    {
        if (_factions.TryGetValue(userId, out var faction))
            return faction;

        return null;
    }

    /// <summary>Игрок с отрабатываемым баном в лобби: ему можно выбирать фракцию в late join.</summary>
    public void MakeEligible(ICommonSession session)
    {
        _eligible.Add(session.UserId);
    }

    /// <summary>Бан закончился или игрок ушёл: выбор фракции в late join больше не предлагаем.</summary>
    public void ClearEligible(NetUserId userId)
    {
        _eligible.Remove(userId);
        _deadlines.Remove(userId);
    }

    /// <summary>
    ///     Назначает фракцию в наименее населённую (среди фракций, у которых есть точки спавна)
    ///     и сообщает игроку. Повторный вызов ничего не меняет.
    /// </summary>
    public ProtoId<KatorgaFactionPrototype> AssignFaction(ICommonSession session)
    {
        if (_factions.TryGetValue(session.UserId, out var assigned))
        {
            _deadlines.Remove(session.UserId);
            return assigned;
        }

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
        SendState(session);

        return picked;
    }

    public bool TrySelectFaction(ICommonSession session, string factionId)
    {
        var userId = session.UserId;
        if (!_eligible.Contains(userId))
            return false;

        // Выбор ещё не открывался: сначала игрок должен открыть late join.
        if (!_factions.ContainsKey(userId) && !_deadlines.ContainsKey(userId))
            return false;

        if (!_prototype.TryIndex<KatorgaFactionPrototype>(factionId, out var faction) ||
            !HasSpawnPoints(faction.ID))
        {
            return false;
        }

        // Уже выбрал другую фракцию (или автоназначение уже произошло) — перебирать нельзя.
        var current = GetFaction(userId);
        if (current != null && current.Value != faction.ID)
            return false;

        if (current == null)
        {
            _factions[userId] = faction.ID;
            _deadlines.Remove(userId);
            _chat.DispatchServerMessage(session, Loc.GetString("katorga-faction-selected", ("faction", Loc.GetString(faction.Name))));
        }

        SendState(session);
        return true;
    }

    /// <summary>Снимает заключённого с учёта фракций: игрок отработал срок и выпущен.</summary>
    public void ClearFaction(NetUserId userId)
    {
        _factions.Remove(userId);
        _deadlines.Remove(userId);
        _eligible.Remove(userId);
    }

    private void OnStateRequest(KatorgaFactionStateRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_eligible.Contains(session.UserId))
            return;

        // Первое открытие late join запускает отсчёт: в лобби до начала раунда он бы
        // сгорал зря, и игрок не успевал ничего выбрать.
        if (!_factions.ContainsKey(session.UserId) && !_deadlines.ContainsKey(session.UserId))
            _deadlines[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(_selectionSeconds);

        SendState(session);
    }

    private void OnSelect(KatorgaFactionSelectEvent ev, EntitySessionEventArgs args)
    {
        TrySelectFaction(args.SenderSession, ev.FactionId);
    }

    /// <summary>
    ///     Отправляет клиенту варианты, остаток времени и выбранную фракцию.
    ///     Клиент считает остаток сам по своим часам, поэтому расхождение времени не мешает.
    /// </summary>
    private void SendState(ICommonSession session)
    {
        var options = _prototype.EnumeratePrototypes<KatorgaFactionPrototype>()
            .OrderBy(faction => faction.Order)
            .ThenBy(faction => faction.ID)
            .Select(faction => new KatorgaFactionChoice
            {
                Id = faction.ID,
                Name = Loc.GetString(faction.Name),
                Feature = Loc.GetString(faction.Feature),
            })
            .ToList();

        var seconds = 0;
        if (_deadlines.TryGetValue(session.UserId, out var deadline))
            seconds = Math.Max(0, (int) Math.Ceiling((deadline - _timing.CurTime).TotalSeconds));

        RaiseNetworkEvent(new KatorgaFactionStateEvent
        {
            Options = options,
            SecondsRemaining = seconds,
            Selected = _factions.TryGetValue(session.UserId, out var selected) ? selected.Id : null,
        }, session);
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
        _factions.Clear();
        _deadlines.Clear();
        _eligible.Clear();
    }
}
