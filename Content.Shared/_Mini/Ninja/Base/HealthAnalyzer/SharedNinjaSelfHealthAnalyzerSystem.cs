// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;

namespace Content.Shared._Mini.Ninja.Base.HealthAnalyzer;

public abstract partial class SharedNinjaSelfHealthAnalyzerSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaSelfHealthAnalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NinjaSelfHealthAnalyzerComponent, GetItemActionsEvent>(OnGetItemActions);
    }

    private void OnMapInit(Entity<NinjaSelfHealthAnalyzerComponent> ent, ref MapInitEvent args)
    {
        Actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnGetItemActions(Entity<NinjaSelfHealthAnalyzerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }
}
