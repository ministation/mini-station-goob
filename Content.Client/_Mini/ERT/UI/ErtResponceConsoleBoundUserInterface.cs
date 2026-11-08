// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using System.Linq;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared._Mini.ERT;

namespace Content.Client._Mini.ERT.UI
{
    [UsedImplicitly]
    public sealed class ErtResponceConsoleBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private ErtResponceConsoleWindow? _window;

        public ErtResponceConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
            : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<ErtResponceConsoleWindow>();

            _window.ResponceTeamButton.OnPressed += _ =>
                SendMessage(new ErtResponceConsoleUiButtonPressedMessage(
                    ErtResponceConsoleUiButton.ResponceErt,
                    team: GenSelectedAvailableTeam(),
                    callReason: GetCallReason()
                ));

        }

        private string? GetCallReason()
        {
            if (_window == null)
                return null;

            var reason = _window.CallReasonEdit.Text;
            return string.IsNullOrWhiteSpace(reason) ? null : reason;
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            _window?.Populate((ErtResponceConsoleBoundUserInterfaceState)state);
        }

        private string? GenSelectedAvailableTeam()
        {
            if (_window == null)
                return null;

            var item = _window.AvailableTeamsList.GetSelected().FirstOrDefault();
            return item?.Metadata as string;
        }


    }
}
