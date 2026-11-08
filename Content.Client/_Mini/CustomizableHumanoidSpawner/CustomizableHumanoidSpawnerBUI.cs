// SPDX-License-Identifier: LicenseRef-MiniStation-1.0



using Content.Shared._Mini.CustomizableHumanoidSpawner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.CustomizableHumanoidSpawner;

[UsedImplicitly]
public sealed class CustomizableHumanoidSpawnerBUI(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private CustomizableHumanoidSpawnerUI? _ui;

    protected override void Open()
    {
        base.Open();
        _ui = this.CreateWindow<CustomizableHumanoidSpawnerUI>();
        _ui.OnConfirm += Send;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_ui == null || state is not CustomizableHumanoidSpawnerBuiState msg)
            return;

        _ui.SetData(msg);
    }

    private void Send(
        bool useRandom,
        int characterIndex,
        string customName,
        bool useCustomDescription,
        string customDescription)
    {
        SendMessage(new CustomizableHumanoidSpawnerMessage(
            useRandom,
            characterIndex,
            customName,
            useCustomDescription,
            customDescription));
    }
}
