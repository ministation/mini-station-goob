// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;

namespace Content.Shared._Mini.Ninja.Base.Visor;

public abstract partial class SharedNinjaVisionModuleSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaVisionModuleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaVisionModuleComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnMapInit(Entity<NinjaVisionModuleComponent> ent, ref MapInitEvent args)
    {
        Actions.AddAction(ent.Owner, ref ent.Comp.CycleActionEntity, ent.Comp.CycleAction);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<NinjaVisionModuleComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.CycleActionEntity);
    }
}
