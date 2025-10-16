// SPDX-FileCopyrightText: 2020 Exp <theexp111@gmail.com>
// SPDX-FileCopyrightText: 2020 Metal Gear Sloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2020 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2020 zumorica <zddm@outlook.es>
// SPDX-FileCopyrightText: 2021 Acruid <shatter66@gmail.com>
// SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 ike709 <ike709@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Veritius <veritiusgaming@gmail.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Morb <14136326+Morb0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2024 Kot <1192090+koteq@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 avery <51971268+graevy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Content.Shared.Containers.ItemSlots;

namespace Content.Client.Communications.UI
{
    public sealed class CommunicationsConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        [ViewVariables]
        private CommunicationsConsoleMenu? _menu;

        [ViewVariables]
        public bool CanAnnounce { get; private set; }
        [ViewVariables]
        public bool CanBroadcast { get; private set; }

        [ViewVariables]
        public bool CanCall { get; private set; }

        [ViewVariables]
        public bool CanSetAlertLevel { get; private set; }

        [ViewVariables]
        public bool CountdownStarted { get; private set; }

        [ViewVariables]
        public bool AlertLevelSelectable { get; private set; }

        [ViewVariables]
        public string SelectedAlertLevel { get; private set; } = default!;

        [ViewVariables]
        public string CurrentLevel { get; private set; } = default!;

        [ViewVariables]
        private TimeSpan? _expectedCountdownTime;

        public int Countdown => _expectedCountdownTime == null ? 0 : Math.Max((int)_expectedCountdownTime.Value.Subtract(_gameTiming.CurTime).TotalSeconds, 0);

        // ERT
        [ViewVariables]
        public bool ERTCanCall { get; private set; }

        [ViewVariables]
        public bool ERTCountdownStarted { get; private set; }

        [ViewVariables]
        public bool ERTTeamSelectable { get; private set; }

        [ViewVariables]
        private string ERTSelectedTeam { get; set; } = default!;

        [ViewVariables]
        public bool IsFirstPrivilegedIdPresent { get; private set; }

        [ViewVariables]
        public bool IsSecondPrivilegedIdPresent { get; private set; }

        [ViewVariables]
        public bool IsFirstPrivilegedIdValid { get; private set; }

        [ViewVariables]
        public bool IsSecondPrivilegedIdValid { get; private set; }

        [ViewVariables]
        private TimeSpan? _expectedERTCountdownTime;

        public int ERTCountdown => _expectedERTCountdownTime == null ? 0 : Math.Max((int)_expectedERTCountdownTime.Value.Subtract(_gameTiming.CurTime).TotalSeconds, 0);

        public CommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = new CommunicationsConsoleMenu(this);
            _menu.OnClose += Close;
            _menu.OpenCentered();
        }

        public void UpdateFirstId()
        {
            SendMessage(new ItemSlotButtonPressedEvent(SharedCommunicationsConsoleComponent.FirstPrivilegedSlotId));
        }

        public void UpdateSecondId()
        {
            SendMessage(new ItemSlotButtonPressedEvent(SharedCommunicationsConsoleComponent.SecondPrivilegedSlotId));
        }

        public void AlertLevelSelected(string level)
        {
            SelectedAlertLevel = level;
            SendMessage(new CommunicationsConsoleSelectAlertLevelMessage());
        }

        public void AlertLevelSetButtonPressed()
        {
            if (AlertLevelSelectable)
            {
                SendMessage(new CommunicationsConsoleSetAlertLevelMessage(SelectedAlertLevel));
            }
        }

        public void EmergencyShuttleButtonPressed()
        {
            if (CountdownStarted)
                RecallShuttle();
            else
                CallShuttle();
        }

        public void AnnounceButtonPressed(string message)
        {
            var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
            var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
            SendMessage(new CommunicationsConsoleAnnounceMessage(msg));
        }

        public void BroadcastButtonPressed(string message)
        {
            SendMessage(new CommunicationsConsoleBroadcastMessage(message));
        }

        public void CallERTButtonPressed(string message)
        {
            if (ERTCountdownStarted)
                RecallERT();
            else
                CallERT(message);
        }

        private void CallERT(string message)
        {
            var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
            var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);

            SendMessage(new CommunicationsConsoleCallERTMessage(ERTSelectedTeam, msg));
        }

        private void RecallERT()
        {
            SendMessage(new CommunicationsConsoleRecallERTMessage());
        }

        public void ERTTeamSelected(string level)
        {
            ERTSelectedTeam = level;
            SendMessage(new CommunicationsConsoleSelectERTMessage());
        }

        public void CallShuttle()
        {
            SendMessage(new CommunicationsConsoleCallEmergencyShuttleMessage());
        }

        public void RecallShuttle()
        {
            SendMessage(new CommunicationsConsoleRecallEmergencyShuttleMessage());
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not CommunicationsConsoleInterfaceState commsState)
                return;

            CanAnnounce = commsState.CanAnnounce;
            CanBroadcast = commsState.CanBroadcast;
            CanCall = commsState.CanCall;
            _expectedCountdownTime = commsState.ExpectedCountdownEnd;
            CountdownStarted = commsState.CountdownStarted;
            AlertLevelSelectable = commsState.AlertLevels != null && !float.IsNaN(commsState.CurrentAlertDelay) && commsState.CurrentAlertDelay <= 0;
            CurrentLevel = commsState.CurrentAlert;

            ERTCanCall = commsState.ERTCanCall;
            _expectedERTCountdownTime = commsState.ERTCountdownTime;
            ERTCountdownStarted = commsState.ERTCountdownStarted;

            IsFirstPrivilegedIdPresent = commsState.IsFirstPrivilegedIdPresent;
            IsSecondPrivilegedIdPresent = commsState.IsSecondPrivilegedIdPresent;
            IsFirstPrivilegedIdValid = commsState.IsFirstPrivilegedIdValid;
            IsSecondPrivilegedIdValid = commsState.IsSecondPrivilegedIdValid;

            if (string.IsNullOrEmpty(SelectedAlertLevel))
            {
                if (commsState.AlertLevels != null && commsState.AlertLevels.Count > 0)
                {
                    SelectedAlertLevel = commsState.AlertLevels[0];
                }
            }

            if (string.IsNullOrEmpty(ERTSelectedTeam))
            {
                if (commsState.ERTList != null && commsState.ERTList.Count > 0)
                {
                    ERTSelectedTeam = commsState.ERTList[0];
                }
            }

            if (_menu != null)
            {
                _menu.UpdateCountdown();
                _menu.UpdateAlertLevels(commsState.AlertLevels, CurrentLevel, SelectedAlertLevel);
                _menu.AlertLevelSetButton.Disabled = !AlertLevelSelectable;

                _menu.EmergencyShuttleButton.Disabled = !CanCall;
                _menu.AnnounceButton.Disabled = !CanAnnounce;

                _menu.UpdateERTTeams(commsState.ERTList, ERTSelectedTeam);
                _menu.UpdateFirstId(IsFirstPrivilegedIdPresent, IsFirstPrivilegedIdValid);
                _menu.UpdateSecondId(IsSecondPrivilegedIdPresent, IsSecondPrivilegedIdValid);

                _menu.BroadcastButton.Disabled = !CanBroadcast;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;

            _menu?.Dispose();
        }
    }
}
