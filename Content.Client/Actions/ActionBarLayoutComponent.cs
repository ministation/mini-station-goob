// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Client.Actions;

/// <summary>
/// Client-side hotbar arrangement for this specific entity.
/// Survives jaunt/aghost for the same body; never applies to another character.
/// </summary>
[RegisterComponent]
public sealed partial class ActionBarLayoutComponent : Component
{
    [DataField, ViewVariables]
    public bool IsPaged;

    [DataField, ViewVariables]
    public int CurrentPage;

    /// <summary>
    /// Pages → slots. Empty slot = null proto.
    /// </summary>
    [DataField, ViewVariables]
    public List<List<ActionBarSlotData>> Pages = new();

    [ViewVariables]
    {
        get
        {
            var count = 0;
            foreach (var page in Pages)
            {
                foreach (var slot in page)
                {
                    if (slot.ProtoId != null)
                        count++;
                }
            }

            return count;
        }
    }
}

[DataDefinition]
public sealed partial class ActionBarSlotData
{
    [DataField]
    public string? ProtoId;

    [DataField]
    public string? ContainerProtoId;

    public bool IsEmpty => ProtoId == null;
}
