// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared._Mini.ERT;

namespace Content.Client._Mini.ERT.UI
{
    [UsedImplicitly]
    public sealed class ErtComputerShuttleBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private ErtComputerShuttleWindow? _window;

        public ErtComputerShuttleBoundUserInterface(EntityUid owner, Enum uiKey)
            : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<ErtComputerShuttleWindow>();

            _window.StartEvacuationButton.OnPressed += _ =>
                SendMessage(new ErtComputerShuttleUiButtonPressedMessage(
                    ErtComputerShuttleUiButton.Evacuation
                ));

            _window.CancelEvacuationButton.OnPressed += _ =>
                SendMessage(new ErtComputerShuttleUiButtonPressedMessage(
                    ErtComputerShuttleUiButton.CancelEvacuation
                ));

        }



    }
}
