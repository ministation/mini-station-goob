// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing/fishing_equipment.dm, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing;
using JetBrains.Annotations;

namespace Content.Client._Mini.Fishing.Capsule;

[UsedImplicitly]
public sealed partial class FishingSpotCapsuleBoundUserInterface : BoundUserInterface
{
    private FishingSpotCapsuleRadialMenu? _window;

    public FishingSpotCapsuleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FishingSpotCapsuleRadialMenu>();
        _window.OnSelected += sourceId =>
        {
            SendMessage(new FishingSpotCapsuleSelectMessage(sourceId));
            _window.Close();
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FishingSpotCapsuleBoundUserInterfaceState capsuleState)
            _window?.SetOptions(capsuleState.Options);
    }
}
