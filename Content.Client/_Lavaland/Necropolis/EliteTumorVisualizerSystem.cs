// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Necropolis;
using Robust.Client.GameObjects;

namespace Content.Client._Lavaland.Necropolis;

/// <summary>
/// Renders the tumor growth states.
/// </summary>
public sealed class EliteTumorVisualizerSystem : VisualizerSystem<EliteTumorComponent>
{
    protected override void OnAppearanceChange(
        EntityUid uid,
        EliteTumorComponent comp,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData(uid, EliteTumorVisuals.State, out EliteTumorVisualState state, args.Component))
            state = EliteTumorVisualState.Dormant;

        var target = state switch
        {
            EliteTumorVisualState.Advanced => "advanced_tumor",
            EliteTumorVisualState.Popped => "tumor_popped",
            _ => "tumor",
        };

        args.Sprite.LayerSetState(0, target);
    }
}
