// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Silicons.Borgs.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// User interface used by borgs to select their type.
/// </summary>
/// <seealso cref="BorgSelectTypeMenu"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
/// <seealso cref="BorgSwitchableTypeUiKey"/>
[UsedImplicitly]
public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgSelectTypeMenu? _menu;

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        // Ministation-Start: Typan Borg Selection and Mimicry
        _menu = new BorgSelectTypeMenu(Owner);
        _menu.OpenCentered();
        _menu.OnClose += Close;
        _menu.ConfirmedBorgType += (prototype, subtype) => SendPredictedMessage(new BorgSelectTypeMessage(prototype, subtype));
        // Ministation-End: Typan Borg Selection and Mimicry
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // Ministation-Start: Typan Borg Selection and Mimicry
        _menu?.Dispose();
        // Ministation-End: Typan Borg Selection and Mimicry
    }
}
