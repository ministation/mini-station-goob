// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.ModSuits.Components;
using Content.Goobstation.Shared.ModSuits.Systems;
using Robust.Shared.Network;

namespace Content.Goobstation.Shared.ModSuits.Modules;

public sealed partial class ModOpenUiModuleSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ModSuitSystem _modSuit = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnUsed(Entity<ModOpenUiModuleComponent> ent, ref ModModuleUsedEvent args)
    {
        if (!TryComp<ModSuitComponent>(args.Suit, out var suit))
            return;

        var target = ent.Comp.Slot is { } slot ? _modSuit.GetPart((args.Suit, suit), slot) : args.Suit;
        if (target is not { } uid || !_ui.HasUi(uid, ent.Comp.Key))
            return;

        args.Handled = true;

        // Module use is predicted on the client, so a predicted open here would send a second
        // OpenBoundInterfaceMessage on top of the server's own open. The duplicate entry in
        // UserInterfaceUserComponent.OpenInterfaces then survives closing or deleting the suit,
        // and PVS reports it every tick.
        if (_net.IsClient)
            return;

        _ui.OpenUi(uid, ent.Comp.Key, args.User);
    }
}
