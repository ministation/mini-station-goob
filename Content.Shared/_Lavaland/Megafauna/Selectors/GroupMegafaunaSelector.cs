// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Random.Helpers;

namespace Content.Shared._Lavaland.Megafauna.Selectors;

/// <summary>
/// Invokes an attack from one of the child action selectors, based on the weight of the children
/// </summary>
public sealed partial class GroupMegafaunaSelector : MegafaunaSelector
{
    [DataField(required: true)]
    public List<MegafaunaSelector> Children = new();

    protected override float InvokeImplementation(MegafaunaCalculationBaseArgs args)
    {
        var children = new Dictionary<MegafaunaSelector, float>(Children.Count);
        foreach (var child in Children)
        {
            // Don't include invalid groups
            if (!child.CheckConditions(args))
                continue;

            children.Add(child, child.Weight);
        }

        if (children.Count == 0)
            return FailDelay;

        var pick = args.Random.Pick(children);
        return pick.Invoke(args);
    }
}
