// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.TypanWar;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Shows NT / Syndicate faction markers above war combatants for all viewers.
/// Icons render on the left, opposite job icons on the right.
/// </summary>
public sealed class TypanWarFactionIconSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<FactionIconPrototype> NtIcon = "TypanWarNtFaction";
    private static readonly ProtoId<FactionIconPrototype> TypanIcon = "TypanWarTypanFaction";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TypanWarFactionComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<TypanWarFactionComponent> ent, ref GetStatusIconsEvent args)
    {
        var iconId = ent.Comp.Side == TypanWarSide.Nanotrasen ? NtIcon : TypanIcon;

        if (_prototype.TryIndex(iconId, out var icon))
            args.StatusIcons.Add(icon);
    }
}
