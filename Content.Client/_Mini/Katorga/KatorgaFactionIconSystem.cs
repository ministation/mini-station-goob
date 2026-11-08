// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
