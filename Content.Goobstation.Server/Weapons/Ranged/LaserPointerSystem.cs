// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NPC.Components;
using Content.Shared._Goobstation.Weapons.SmartGun;
using Content.Shared.Wieldable.Components;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.Weapons.Ranged;

public sealed class LaserPointerSystem : SharedLaserPointerSystem
{
    [Dependency] private readonly PvsOverrideSystem _override = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private EntityUid? _managerUid;

    protected override void PvsOverride(EntityUid entity)
    {
        base.PvsOverride(entity);
        _managerUid = entity;
        RefreshLaserManagerOverrides();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var npcCombatQuery = GetEntityQuery<NPCRangedCombatComponent>();
        var query = EntityQueryEnumerator<LaserPointerComponent, WieldableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pointer, out var wieldable, out var xform))
        {
            if (!wieldable.Wielded)
                continue;

            if (npcCombatQuery.HasComp(xform.ParentUid) ||
                Timing.CurTime - pointer.LastNetworkEventTime < pointer.MaxDelayBetweenNetworkEvents)
                continue;

            AddOrRemoveLine(GetNetEntity(uid), pointer, wieldable, xform, null, null);
        }

        RefreshLaserManagerOverrides();
    }

    private void RefreshLaserManagerOverrides()
    {
        if (_managerUid is not { } manager || !TryComp<LaserPointerManagerComponent>(manager, out var managerComp))
            return;

        var recipients = new HashSet<ICommonSession>();

        if (managerComp.Data.Count > 0)
        {
            foreach (var netPointer in managerComp.Data.Keys)
            {
                var pointer = GetEntity(netPointer);
                if (pointer == EntityUid.Invalid)
                    continue;

                foreach (var session in Filter.Pvs(pointer).Recipients)
                {
                    recipients.Add(session);
                }
            }
        }

        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame)
                continue;

            if (recipients.Contains(session))
                _override.AddSessionOverride(manager, session);
            else
                _override.RemoveSessionOverride(manager, session);
        }
    }
}
