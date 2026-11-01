// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Necropolis;
using Robust.Client.GameObjects;

namespace Content.Client._Lavaland.Necropolis;

/// <summary>
/// Renders the gate door animation from server appearance data.
/// </summary>
public sealed class NecropolisGateVisualizerSystem : VisualizerSystem<NecropolisGateComponent>
{
    protected override void OnAppearanceChange(
        EntityUid uid,
        NecropolisGateComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !args.Sprite.LayerMapTryGet("door", out var layer))
            return;

        if (!AppearanceSystem.TryGetData(uid, NecropolisGateVisuals.State, out NecropolisGateVisualState state, args.Component))
            state = NecropolisGateVisualState.Closed;

        switch (state)
        {
            case NecropolisGateVisualState.Opening:
                args.Sprite.LayerSetVisible(layer, true);
                args.Sprite.LayerSetState(layer, "door_opening");
                break;
            case NecropolisGateVisualState.Open:
                args.Sprite.LayerSetVisible(layer, false);
                break;
            default:
                args.Sprite.LayerSetVisible(layer, true);
                args.Sprite.LayerSetState(layer, "door");
                break;
        }
    }
}
