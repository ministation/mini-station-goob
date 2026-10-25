// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Base.AutoDust;

/// <summary>
/// Registers and hands out the auto-dust toggle action of the suit.
/// </summary>
public abstract partial class SharedNinjaAutoDustSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAutoDustComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaAutoDustComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnMapInit(Entity<NinjaAutoDustComponent> ent, ref MapInitEvent args)
    {
        Actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<NinjaAutoDustComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }
}
