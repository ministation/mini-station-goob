// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Access.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Ghost;

/// <summary>
/// Карта из пресета ghost-роли выдаётся с базовой профессией (например ERTLeader),
/// а настоящую профессию (ERTLeaderAmber и т.п.) mind получает уже после спавна.
/// Синхронизируем должность и иконку карты с профессией ghost-роли, иначе на карте
/// остаётся старая иконка базовой профессии.
/// </summary>
public sealed class GhostRoleJobCardSystem : EntitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        // GhostTakeoverAvailableComponent уже занят GhostRoleSystem, второй обработчик того же события не зарегистрировать.
        SubscribeLocalEvent<GhostRoleComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(EntityUid uid, GhostRoleComponent role, MindAddedMessage args)
    {
        if (role.JobProto is not { } jobId
            || !_prototypes.TryIndex(jobId, out JobPrototype? job)
            || !_idCard.TryFindIdCard(uid, out var card))
            return;

        _idCard.TryChangeJobTitle(card.Owner, job.LocalizedName, card.Comp);
        if (_prototypes.TryIndex(job.Icon, out JobIconPrototype? icon))
            _idCard.TryChangeJobIcon(card.Owner, icon, card.Comp);
    }
}
