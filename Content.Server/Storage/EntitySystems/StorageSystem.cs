// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Content.Shared.Hands;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Storage.EntitySystems;

public sealed partial class StorageSystem : SharedStorageSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StorageComponent, BeforeExplodeEvent>(OnExploded);
        SubscribeLocalEvent<StorageFillComponent, MapInitEvent>(OnStorageFillMapInit);
    }

    private void OnExploded(Entity<StorageComponent> ent, ref BeforeExplodeEvent args)
    {
        args.Contents.AddRange(ent.Comp.Container.ContainedEntities);
    }

    /// <inheritdoc />
    public override void PlayPickupAnimation(EntityUid uid, EntityCoordinates initialCoordinates, EntityCoordinates finalCoordinates,
        Angle initialRotation, EntityUid? user = null)
    {
        // Mini: при дисконнекте сессия ещё висит в PVS, а канал уже отключён, и движок
        // пишет ERRO "Tried to send message ... to disconnected channel". Мёртвые каналы отсекаем.
        var filter = Filter.Pvs(uid)
            .RemoveWhereAttachedEntity(e => e == user)
            .RemoveWhere(session => !session.Channel.IsConnected);
        RaiseNetworkEvent(new PickupAnimationEvent(GetNetEntity(uid), GetNetCoordinates(initialCoordinates), GetNetCoordinates(finalCoordinates), initialRotation), filter);
    }
}
