// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Modifies max zoom when added and automatically zooms out.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(MaxZoomMutationSystem))]
public sealed partial class MaxZoomMutationComponent : Component
{
    /// <summary>
    /// What to scale MaxZoom by.
    /// </summary>
    [DataField]
    public float Modifier = 1.25f;
}
