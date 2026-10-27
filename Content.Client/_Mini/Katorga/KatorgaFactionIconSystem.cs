// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Katorga.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Показывает метку фракции каторги рядом с заключённым, как метки сторон на войне станций.
/// </summary>
public sealed class KatorgaFactionIconSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatorgaFactionMemberComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<KatorgaFactionMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.Faction, out var faction) ||
            !_prototype.TryIndex(faction.Icon, out var icon))
        {
            return;
        }

        args.StatusIcons.Add(icon);
    }
}
