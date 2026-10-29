// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Goobstation.Client.Fishing.Overlays;

/// <summary>
/// Draws the fishing line from the rod to the float.
/// </summary>
public sealed class FishingOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly TransformSystem _transform;

    private static readonly Color LineColor = Color.FromHex("#ddd9c7aa");

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FishingOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = _entManager.EntitySysManager.GetEntitySystem<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(4f);

        var query = _entManager.AllEntityQueryEnumerator<FishingLureComponent, TransformComponent>();
        while (query.MoveNext(out _, out var lure, out var lureXform))
        {
            if (lureXform.MapID != args.MapId)
                continue;

            if (lure.Rod is not { } rod || !_entManager.TryGetComponent<TransformComponent>(rod, out var rodXform))
                continue;

            var start = _transform.GetWorldPosition(lureXform);
            var end = _transform.GetWorldPosition(rodXform);

            if (!bounds.Contains(start) && !bounds.Contains(end))
                continue;

            handle.DrawLine(start, end, LineColor);
        }
    }
}