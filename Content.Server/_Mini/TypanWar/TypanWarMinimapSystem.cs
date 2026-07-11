// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Actions;
using Content.Shared._Mini.TypanWar;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

public sealed class TypanWarMinimapSystem : EntitySystem
{
    private static readonly EntProtoId MinimapAction = "ActionTypanWarMinimap";

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarMinimapActionEvent>(_ => { });
    }

    public void EnsureMinimapAction(EntityUid uid)
    {
        if (!_prototypes.HasIndex(MinimapAction))
        {
            Log.Error("Typan war minimap action prototype is missing: {Prototype}", MinimapAction);
            return;
        }

        var comp = EnsureComp<TypanWarMinimapComponent>(uid);

        if (comp.ActionEntity != null && Exists(comp.ActionEntity))
            return;

        _actions.AddAction(uid, ref comp.ActionEntity, MinimapAction);
    }

    public void RemoveMinimapAction(EntityUid uid)
    {
        if (!TryComp<TypanWarMinimapComponent>(uid, out var comp))
            return;

        if (comp.ActionEntity is { } action)
            _actions.RemoveAction(uid, action);

        RemComp<TypanWarMinimapComponent>(uid);
    }
}
