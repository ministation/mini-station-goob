// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
