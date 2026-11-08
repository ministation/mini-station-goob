// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Administration.Systems;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Administration.Systems;

namespace Content.Server._Lavaland.Megafauna.Systems;

public sealed class MegafaunaRejuvenateSystem : EntitySystem
{
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MegafaunaRejuvenateComponent, MegafaunaShutdownEvent>(OnMegafaunaShutdown);
    }

    private void OnMegafaunaShutdown(Entity<MegafaunaRejuvenateComponent> ent, ref MegafaunaShutdownEvent args)
        => _rejuvenate.PerformRejuvenate(ent);
}
