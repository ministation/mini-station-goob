using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.Ghost;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.SSDIndicator;
using Content.Shared.Warps;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

// Не менять
namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    // Прототип-fallback департамента. Если у сущности нет департамента, он будет отображаться как этот
    private static readonly ProtoId<DepartmentPrototype> UnknownDepartmentPrototype = "Specific";

    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    /// <summary>
    /// Создает список всех точек телепорта локаций
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpPlace> GetLocationWarps()
    {
        var query = AllEntityQuery<WarpPointComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var meta, out var xform))
        {
            // Mini: не отдаём в панель то, куда телепорт заведомо сломан.
            // Точку, которую сам варп исключает (Blacklist), и точку без карты (nullspace/лобби)
            // гость либо не должен видеть, либо телепорт туда оставит его в пустоте.
            if (_whitelist.IsWhitelistPass(component.Blacklist, uid) || !IsWarpReachable(uid, xform))
                continue;

            var warp = new GhostWarpPlace(
                GetNetEntity(uid),
                component.Location ?? meta.EntityName,
                component.Location ?? meta.EntityDescription);

            yield return warp;
        }
    }

    /// <summary>
    /// Создает список всех игроков для отображения в панели телепорта
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpPlayer> GetPlayerWarps()
    {
        var query = AllEntityQuery<MindContainerComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mind, out var meta, out var xform))
        {
            var isGhost = HasComp<GhostComponent>(uid);
            var isMob = HasComp<MobStateComponent>(uid);

            if (!IsEntityPanelRelevant(uid, isGhost, isMob))
                continue;

            // Mini: цель без карты (удалённое тело, только что вышедший игрок) - телепорт в nullspace.
            if (!IsWarpReachable(uid, xform))
                continue;

            var playerMind = mind.Mind ?? mind.LastMindStored;
            TryGetPlayerJobInfo(playerMind, out var playerJobId, out var playerDepartmentId);

            var hasAnyMind = playerMind != null;

            // Проверка на стандартных мобов, в которых нет игрока
            // Они могут проходить через IsEntityPanelRelevant(), то по сути никогда не являлись игроками
            var isSimpleMob = isMob && !hasAnyMind;
            if (isSimpleMob)
                continue;

            var isDead = _mobState.IsDead(uid);
            var isLeft = TryComp<SSDIndicatorComponent>(uid, out var indicator) && indicator.IsSSD && !isDead && hasAnyMind;

            var warp = new GhostWarpPlayer(
                GetNetEntity(uid),
                meta.EntityName,
                playerJobId,
                playerDepartmentId,
                isGhost,
                isLeft,
                isDead
            );

            yield return warp;
        }
    }

    /// <summary>
    /// Mini: можно ли вообще телепортироваться к сущности. Сущности в nullspace (удаляемые тела,
    /// остатки лобби) висят без карты - SetCoordinates туда оставляет призрака в пустоте.
    /// </summary>
    private bool IsWarpReachable(EntityUid uid, TransformComponent xform)
    {
        return !TerminatingOrDeleted(uid) && xform.MapUid != null;
    }


    private bool TryGetPlayerJobInfo(EntityUid? mind,
        [NotNullWhen(true)] out string? job, out string department)
    {
        department = _prototypeManager.Index(UnknownDepartmentPrototype).ID;
        job = null;

        if (!_jobs.MindTryGetJob(mind, out var jobPrototype))
            return false;

        // Профессия известна и без департамента: раньше такая роль снова показывалась как
        // «неизвестно кто», хотя название работы и иконка у неё есть. Департамент - это только
        // группировка в списке, поэтому он падает в Specific, а не съедает JobId.
        job = jobPrototype.ID;
        department = _jobs.TryGetDepartment(jobPrototype.ID, out var departmentPrototype)
            ? departmentPrototype.ID
            : _prototypeManager.Index(UnknownDepartmentPrototype).ID;

        return true;
    }

    /// <summary>
    /// Создает список всех игроков с ролью антагониста для отображения в панели призрака
    /// </summary>
    /// <returns>Созданный список</returns>
    private IEnumerable<GhostWarpGlobalAntagonist> GetAntagonistWarps()
    {
        var query = AllEntityQuery<GhostPanelAntagonistMarkerComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var meta, out var xform))
        {
            // Пустые оболочки без активного игрока не показываем в телепорте.
            if (!TryComp<MindContainerComponent>(uid, out var mindContainer) || !mindContainer.HasMind)
                continue;

            // Mini: антагонист без карты - тот же сломанный телепорт в пустоту.
            if (!IsWarpReachable(uid, xform))
                continue;

            var warp = new GhostWarpGlobalAntagonist(
                GetNetEntity(uid),
                meta.EntityName,
                component.Name,
                component.Description,
                component.Priority
            );

            yield return warp;
        }
    }

    /// <summary>
    /// Проверяет, является ли живая сущность подходящей для нахождения в панели телепорта призрака
    /// </summary>
    private bool IsEntityPanelRelevant(EntityUid uid, bool isGhost, bool isMob)
    {
        return !HasComp<GhostPanelAntagonistMarkerComponent>(uid) &&
            (isMob || isGhost
                   || HasComp<BorgChassisComponent>(uid)
                   || HasComp<StationAiHeldComponent>(uid));
    }
}
