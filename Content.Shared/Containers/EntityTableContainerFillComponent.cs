// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Shared.Containers;

/// <summary>
/// Version of <see cref="ContainerFillComponent"/> that utilizes <see cref="EntityTableSelector"/>
/// </summary>
[RegisterComponent, Access(typeof(ContainerFillSystem))]
public sealed partial class EntityTableContainerFillComponent : Component
{
    [DataField]
    public Dictionary<string, EntityTableSelector> Containers = new();

    /// <summary>
    /// Whether to sort the contents of the table by size before inserting.
    /// Helps with fitting items into containers.
    /// </summary>
    [DataField]
    public bool Sort;
}