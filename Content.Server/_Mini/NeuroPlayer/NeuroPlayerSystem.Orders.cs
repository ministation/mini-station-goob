// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._Mini.NeuroPlayer;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Disposal.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.NeuroPlayer;

/// <summary>
/// Мини: короткие приказы игроков ("иди за мной", "стой", "убери мусор") и антизастревание у шлюзов.
/// Приказы разбирает <see cref="NeuroOrderParser"/> без LLM, поэтому работают сразу и бесплатно.
/// </summary>
public sealed partial class NeuroPlayerSystem
{
    /// <summary>Как часто бот пересматривает приказ.</summary>
    private const float OrderThinkSeconds = 0.75f;

    /// <summary>Дальше этого цель приказа считается потерянной.</summary>
    private const float OrderMaxDistance = 80f;

    /// <summary>Ближе этого к цели-человеку бот останавливается и ждет.</summary>
    private const float FollowStopDistance = 2.5f;

    /// <summary>Насколько цель должна уйти от запланированной точки, чтобы пересобрать маршрут.</summary>
    private const float OrderReplanDistance = 2f;

    private const float TrashSearchRadius = 30f;
    private const float DisposalSearchRadius = 40f;

    /// <summary>С этого расстояния мусор можно взять, а утиль принять.</summary>
    private const float OrderReachDistance = 1.2f;

    private const float DoorCheckSeconds = 0.5f;
    private const float DoorActivateRange = 1.8f;

    private const float StuckCheckSeconds = 1.5f;
    private const float StuckMoveEpsilon = 0.2f;
    private const int StuckLimit = 3;
    private const float StuckPauseSeconds = 2.5f;

    private static readonly TimeSpan OrderDuration = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan OrderStageTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OrderVisitWindow = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan StuckWindow = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan OrderReplanSeconds = TimeSpan.FromSeconds(2);

    private static readonly ProtoId<TagPrototype> TrashTag = "Trash";

    private static readonly string[] FollowAgree =
    [
        "Хорошо, иду за вами.",
        "Ладно, показывайте дорогу.",
        "Иду, иду.",
    ];

    private static readonly string[] StopAgree =
    [
        "Хорошо, стою.",
        "Ладно, жду здесь.",
        "Как скажете, никуда не иду.",
    ];

    private static readonly string[] CleanAgree =
    [
        "Сейчас уберу.",
        "Ладно, соберу мусор.",
        "Уже иду за уборкой.",
    ];

    private static readonly string[] NoTrashLines =
    [
        "Мусора поблизости не вижу.",
        "А где мусор? Ничего не вижу.",
    ];

    private const string OrderTrashDelivered = "Мусор в утиль.";

    /// <summary>Накопитель проверки шлюзов (0.5 с), общий на всех ботов.</summary>
    private float _doorAccumulator;

    /// <summary>Мусор -> бот, который его уже забрал, чтобы двое не гнались за одним предметом.</summary>
    private readonly Dictionary<EntityUid, EntityUid> _trashClaims = new();

    /// <summary>
    /// Мини: хук из HandleSpeech до MatchesName/TryRespond. Возвращает true, если реплика была
    /// приказом и бот на нее ответил (LLM тогда не тратится).
    /// </summary>
    private bool TryHandleOrder(
        EntityUid bot,
        NeuroPlayerComponent comp,
        NeuroPersonaPrototype persona,
        EntityUid speaker,
        string message,
        bool viaRadio,
        float distance)
    {
        if (comp.DangerUntil != null || !NeuroOrderParser.TryParse(message, out var kind))
            return false;

        // Обращение по имени слышно и по рации; безымянный приказ - только вплотную и только от
        // ближайшего бота, иначе на "пошли со мной" сорвутся все шесть.
        var named = MatchesName(persona.Name, message);
        if (!named)
        {
            if (viaRadio || distance < 0f || distance > DirectReplyDistance)
                return false;

            if (!IsNearestBotToSpeaker(bot, speaker))
                return false;
        }

        var now = _timing.CurTime;
        switch (kind)
        {
            case NeuroOrderKind.Stop:
                ResetOrder(bot, comp);
                SayOrderReply(bot, _random.Pick(StopAgree), viaRadio);
                return true;

            case NeuroOrderKind.Follow:
                ResetOrder(bot, comp, idle: false);
                comp.Order = NeuroOrderKind.Follow;
                comp.OrderTarget = speaker;
                comp.OrderUntil = now + OrderDuration;
                comp.NextOrderThink = now;
                comp.LastOrderPoint = null;
                comp.LastOrderThink = now;
                SayOrderReply(bot, _random.Pick(FollowAgree), viaRadio);
                return true;

            case NeuroOrderKind.CleanTrash:
                ResetOrder(bot, comp, idle: false);
                comp.Order = NeuroOrderKind.CleanTrash;
                comp.OrderUntil = now + OrderDuration;
                comp.OrderStageUntil = now + OrderStageTimeout;
                comp.NextOrderThink = now;
                SayOrderReply(bot, _random.Pick(CleanAgree), viaRadio);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Проверка антизастревания: соглашаются только ближайший к спикеру живой бот.</summary>
    private bool IsNearestBotToSpeaker(EntityUid bot, EntityUid speaker)
    {
        var speakerPos = _transform.GetWorldPosition(speaker);
        var best = bot;
        var bestDistance = float.MaxValue;

        foreach (var other in _bots)
        {
            if (!IsBotUp(other) || !TryComp<NeuroPlayerComponent>(other, out var otherComp))
                continue;

            if (otherComp.DangerUntil != null)
                continue;

            var distance = (_transform.GetWorldPosition(other) - speakerPos).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = other;
        }

        return best == bot;
    }

    /// <summary>Мини: тик приказов и антизастревания, вызывается из Update.</summary>
    private void TickOrders(float frameTime)
    {
        var now = _timing.CurTime;

        _doorAccumulator += frameTime;
        var doorPass = false;
        if (_doorAccumulator >= DoorCheckSeconds)
        {
            _doorAccumulator = 0f;
            doorPass = true;
        }

        if (doorPass)
            PruneTrashClaims();

        foreach (var bot in _bots)
        {
            if (!IsBotUp(bot) || !TryComp<NeuroPlayerComponent>(bot, out var comp))
                continue;

            // Бегство и драка важнее приказов: их план не трогаем.
            if (comp.DangerUntil != null || comp.HostileUntil != null)
            {
                comp.StuckTries = 0;
                continue;
            }

            var xform = Transform(bot);

            if (doorPass)
                TryOpenNearbyDoor(bot, xform);

            TickStuck(bot, comp, now, xform);

            if (now >= comp.NextOrderThink)
            {
                comp.NextOrderThink = now + TimeSpan.FromSeconds(OrderThinkSeconds);
                TickOrder(bot, comp, now);
            }
        }
    }

    private void TickOrder(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now)
    {
        if (comp.Order == NeuroOrderKind.None)
            return;

        if (comp.OrderUntil != TimeSpan.Zero && now > comp.OrderUntil)
        {
            ResetOrder(bot, comp);
            return;
        }

        if (comp.StuckPauseUntil != TimeSpan.Zero && now < comp.StuckPauseUntil)
            return;

        switch (comp.Order)
        {
            case NeuroOrderKind.Follow:
                ThinkFollow(bot, comp, now);
                break;
            case NeuroOrderKind.CleanTrash:
                ThinkCleanTrash(bot, comp, now);
                break;
            default:
                ResetOrder(bot, comp);
                break;
        }
    }

    /// <summary>Follow: держать дистанцию 2.5 тайла, не отставая от человека больше, чем на 80.</summary>
    private void ThinkFollow(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now)
    {
        if (comp.OrderTarget is not { } target || !IsFollowTargetValid(target, bot) ||
            !TryComp<HTNComponent>(bot, out var htn))
        {
            ResetOrder(bot, comp);
            return;
        }

        var targetXform = Transform(target);
        var botPos = _transform.GetWorldPosition(bot);
        var targetPos = _transform.GetWorldPosition(targetXform);
        var distance = (targetPos - botPos).Length();

        if (distance > OrderMaxDistance)
        {
            ResetOrder(bot, comp);
            return;
        }

        if (distance <= FollowStopDistance)
        {
            // Догнали: ждем, пока человек отойдет, и не пересобираем план.
            comp.LastOrderPoint = null;
            if (htn.RootTask.Task != "IdleCompound")
            {
                comp.VisitUntil = null;
                RestoreIdle(bot);
            }
            return;
        }

        if (!ShouldReplan(bot, comp, targetPos, now))
            return;

        MoveToOrderPoint(bot, comp, targetXform.Coordinates.Offset(_random.NextVector2(0.6f)), now);
    }

    /// <summary>CleanTrash: найти мусор, донести до утиля, сказать об этом игроку.</summary>
    private void ThinkCleanTrash(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now)
    {
        if (comp.OrderStageUntil != TimeSpan.Zero && now > comp.OrderStageUntil)
        {
            ResetOrder(bot, comp);
            return;
        }

        if (comp.OrderItem is { } carried)
        {
            ThinkTrashDelivery(bot, comp, now, carried);
            return;
        }

        ThinkTrashPickup(bot, comp, now);
    }

    private void ThinkTrashPickup(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now)
    {
        if (comp.OrderTarget is not { } trash || !IsCleanableTrash(trash))
        {
            if (comp.OrderTarget is { } stale)
            {
                _trashClaims.Remove(stale);
                comp.OrderTarget = null;
            }

            var found = FindTrash(bot);
            if (found == null)
            {
                SayOrderReply(bot, _random.Pick(NoTrashLines), viaRadio: false);
                ResetOrder(bot, comp);
                return;
            }

            comp.OrderTarget = found;
            comp.LastOrderPoint = null;
            comp.OrderStageUntil = now + OrderStageTimeout;
            trash = found.Value;
        }

        var trashPos = _transform.GetWorldPosition(trash);
        var botPos = _transform.GetWorldPosition(bot);
        if ((trashPos - botPos).Length() <= OrderReachDistance)
        {
            if (_hands.TryPickupAnyHand(bot, trash))
            {
                comp.OrderItem = trash;
                comp.OrderTarget = null;
                comp.OrderStageUntil = now + OrderStageTimeout;
                comp.LastOrderPoint = null;
                return;
            }

            SayOrderReply(bot, _random.Pick(NoTrashLines), viaRadio: false);
            _trashClaims.Remove(trash);
            ResetOrder(bot, comp);
            return;
        }

        if (!ShouldReplan(bot, comp, trashPos, now))
            return;

        MoveToOrderPoint(bot, comp, Transform(trash).Coordinates, now);
    }

    private void ThinkTrashDelivery(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now, EntityUid carried)
    {
        if (!Exists(carried) || _container.IsEntityInContainer(carried))
        {
            ResetOrder(bot, comp);
            return;
        }

        if (comp.OrderTarget is not { } disposalUid || !Exists(disposalUid) ||
            !TryComp<DisposalUnitComponent>(disposalUid, out var disposal))
        {
            if (!TryFindDisposal(bot, out disposalUid, out disposal))
            {
                // Мусор есть, а выбросить некуда: тихо отпускаем, чтобы не обещать игроку лишнего.
                _hands.TryDrop(bot, carried, checkActionBlocker: false);
                ResetOrder(bot, comp);
                return;
            }

            comp.OrderTarget = disposalUid;
            comp.OrderStageUntil = now + OrderStageTimeout;
            comp.LastOrderPoint = null;
        }

        var disposalPos = _transform.GetWorldPosition(disposalUid);
        var botPos = _transform.GetWorldPosition(bot);
        if ((disposalPos - botPos).Length() <= OrderReachDistance)
        {
            if (_hands.TryDropIntoContainer(bot, carried, disposal.Container, checkActionBlocker: false))
            {
                SayOrderReply(bot, OrderTrashDelivered, viaRadio: false);
                _trashClaims.Remove(carried);
                comp.OrderItem = null;
                ResetOrder(bot, comp, dropItem: false);
            }
            else
            {
                _hands.TryDrop(bot, carried, checkActionBlocker: false);
                ResetOrder(bot, comp);
            }
            return;
        }

        if (!ShouldReplan(bot, comp, disposalPos, now))
            return;

        MoveToOrderPoint(bot, comp, Transform(disposalUid).Coordinates, now);
    }

    /// <summary>
    /// Пора ли пересобирать маршрут. Пока бот реально идет (есть NPCSteeringComponent), план не
    /// трогаем: иначе SetRootTask каждые 2 секунды отменял бы путь и бот дергался на месте.
    /// </summary>
    private bool ShouldReplan(EntityUid bot, NeuroPlayerComponent comp, Vector2 targetPos, TimeSpan now)
    {
        if (comp.LastOrderPoint == null)
            return true;

        if ((targetPos - comp.LastOrderPoint.Value).Length() > OrderReplanDistance)
            return true;

        return now - comp.LastOrderThink > OrderReplanSeconds && !TryComp<NPCSteeringComponent>(bot, out _);
    }

    private void MoveToOrderPoint(EntityUid bot, NeuroPlayerComponent comp, EntityCoordinates point, TimeSpan now)
    {
        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        htn.Blackboard.SetValue("NeuroPoint", point);
        SetRootTask(htn, "NeuroVisitCompound");
        comp.VisitUntil = now + OrderVisitWindow;
        comp.LastOrderPoint = _transform.ToWorldPosition(point);
        comp.LastOrderThink = now;
    }

    /// <summary>Цель Follow: жива, на той же карте, не в контейнере и все еще под игроком.</summary>
    private bool IsFollowTargetValid(EntityUid target, EntityUid bot)
    {
        if (!Exists(target))
            return false;

        if (Transform(target).MapID != Transform(bot).MapID)
            return false;

        if (_container.IsEntityInContainer(target))
            return false;

        if (!_mobState.IsAlive(target))
            return false;

        // Гост или дисконнект: тело больше никуда не ведет.
        return HasComp<ActorComponent>(target);
    }

    /// <summary>Сброс приказа: отпустить мусор, вернуть бота к обычной жизни.</summary>
    private void ResetOrder(EntityUid bot, NeuroPlayerComponent comp, bool idle = true, bool dropItem = true)
    {
        if (comp.OrderItem is { } item)
        {
            if (dropItem && Exists(item))
                _hands.TryDrop(bot, item, checkActionBlocker: false);
            _trashClaims.Remove(item);
        }

        comp.Order = NeuroOrderKind.None;
        comp.OrderTarget = null;
        comp.OrderItem = null;
        comp.OrderUntil = TimeSpan.Zero;
        comp.OrderStageUntil = TimeSpan.Zero;
        comp.NextOrderThink = TimeSpan.Zero;
        comp.LastOrderPoint = null;
        comp.LastOrderThink = TimeSpan.Zero;

        if (!idle)
            return;

        comp.VisitUntil = null;
        comp.PoiAccumulator = 0f;
        if (IsBotUp(bot))
            RestoreIdle(bot);
    }

    private void SayOrderReply(EntityUid bot, string phrase, bool viaRadio)
    {
        if (viaRadio)
            phrase = ";" + phrase;

        _chat.TrySendInGameICMessage(bot, phrase, InGameICChatType.Speak, hideChat: false, hideLog: true);
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"Say from neuro player {Name(bot)}: {phrase}");
    }

    /// <summary>Ближайший мусор: тег Trash, не закреплен, не в контейнере, не моб и не бот.</summary>
    private EntityUid? FindTrash(EntityUid bot)
    {
        var botPos = _transform.GetWorldPosition(bot);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var (uid, _) in _lookup.GetEntitiesInRange<TagComponent>(Transform(bot).Coordinates, TrashSearchRadius))
        {
            if (!IsCleanableTrash(uid))
                continue;

            // Предмет уже забрал другой живой бот.
            if (_trashClaims.TryGetValue(uid, out var owner) && owner != bot && IsBotUp(owner))
                continue;

            var distance = (_transform.GetWorldPosition(uid) - botPos).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        if (best is { } trash)
            _trashClaims[trash] = bot;

        return best;
    }

    private bool IsCleanableTrash(EntityUid uid)
    {
        if (!Exists(uid) || !_tags.HasTag(uid, TrashTag))
            return false;

        // Закрепленный мусор (ресайклер и подобное) не унести.
        if (Transform(uid).Anchored)
            return false;

        if (_container.IsEntityInContainer(uid))
            return false;

        // Мышь тоже помечена Trash: живых не убираем.
        if (HasComp<MobStateComponent>(uid) || HasComp<NeuroPlayerComponent>(uid))
            return false;

        return true;
    }

    private bool TryFindDisposal(EntityUid bot, out EntityUid disposalUid, out DisposalUnitComponent disposal)
    {
        var botPos = _transform.GetWorldPosition(bot);
        disposalUid = default;
        disposal = default!;
        var bestDistance = float.MaxValue;
        var found = false;

        foreach (var (uid, comp) in _lookup.GetEntitiesInRange<DisposalUnitComponent>(
                     Transform(bot).Coordinates, DisposalSearchRadius))
        {
            var distance = (_transform.GetWorldPosition(uid) - botPos).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            disposalUid = uid;
            disposal = comp;
            found = true;
        }

        return found;
    }

    private void PruneTrashClaims()
    {
        if (_trashClaims.Count == 0)
            return;

        List<EntityUid>? stale = null;
        foreach (var (trash, owner) in _trashClaims)
        {
            if (Exists(trash) && Exists(owner) && IsBotUp(owner) && _bots.Contains(owner))
                continue;

            (stale ??= new List<EntityUid>()).Add(trash);
        }

        if (stale == null)
            return;

        foreach (var trash in stale)
            _trashClaims.Remove(trash);
    }

    /// <summary>
    /// Открыть дверь рядом: закрытую или denying, к которой у бота есть доступ. Закрытая дверь
    /// перестает быть тупиком, и бот не топчется перед ней.
    /// </summary>
    private bool TryOpenNearbyDoor(EntityUid bot, TransformComponent xform)
    {
        foreach (var (doorUid, door) in _lookup.GetEntitiesInRange<DoorComponent>(xform.Coordinates, DoorActivateRange))
        {
            // Открытую, открывающуюся, заваренную и уже взламываемую не трогаем.
            if (door.State is DoorState.Open or DoorState.Opening or DoorState.Welded or DoorState.Emagging)
                continue;

            if (TryComp<DoorBoltComponent>(doorUid, out var bolts) && bolts.BoltsDown)
                continue;

            if (!_doors.HasAccess(doorUid, bot, door))
                continue;

            _interaction.InteractionActivate(bot, doorUid);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Антизастревание: раз в 1.5 с сравнить позицию с прошлой проверкой. Не сдвинулся почти
    /// никуда - сначала пробуем открыть дверь рядом, потом уводим NeuroPoint в случайную точку,
    /// а после трех неудач подряд даем боту 2.5 с покоя, чтобы он не долбился в стену.
    /// </summary>
    private void TickStuck(EntityUid bot, NeuroPlayerComponent comp, TimeSpan now, TransformComponent xform)
    {
        var pos = _transform.GetWorldPosition(xform);

        if (now < comp.StuckPauseUntil ||
            (comp.Order == NeuroOrderKind.None && comp.VisitUntil == null) ||
            !TryComp<NPCSteeringComponent>(bot, out _))
        {
            comp.LastStuckPos = pos;
            comp.LastStuckCheck = now;
            comp.StuckTries = 0;
            return;
        }

        if (now - comp.LastStuckCheck < TimeSpan.FromSeconds(StuckCheckSeconds))
            return;

        var moved = (pos - comp.LastStuckPos).Length();
        comp.LastStuckPos = pos;
        comp.LastStuckCheck = now;

        if (moved >= StuckMoveEpsilon)
        {
            comp.StuckTries = 0;
            return;
        }

        if (comp.StuckTries == 0)
            comp.StuckFirstAt = now;
        comp.StuckTries++;

        if (!comp.StuckLogged)
        {
            comp.StuckLogged = true;
            _sawmill.Debug($"Neuro player {Name(bot)}: не может пройти, ищу обход.");
        }

        // Сначала дверь: чаще всего бот упирается именно в нее.
        if (TryOpenNearbyDoor(bot, xform))
            return;

        if (comp.StuckTries >= StuckLimit && now - comp.StuckFirstAt <= StuckWindow)
        {
            comp.StuckTries = 0;
            comp.StuckPauseUntil = now + TimeSpan.FromSeconds(StuckPauseSeconds);
            RestoreIdle(bot);
            return;
        }

        if (!TryComp<HTNComponent>(bot, out var htn))
            return;

        // Случайная точка неподалеку от бота: другой путь в обход препятствия.
        var reroute = xform.Coordinates.Offset(_random.NextVector2(3f));
        htn.Blackboard.SetValue("NeuroPoint", reroute);
        SetRootTask(htn, "NeuroVisitCompound");
        comp.VisitUntil = now + OrderVisitWindow;
        comp.LastOrderPoint = _transform.ToWorldPosition(reroute);
        comp.LastOrderThink = now;
    }
}
