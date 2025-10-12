using Content.Client._Mini.Latejoin;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Goobstation.Common.ServerCurrency;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed class LobbyState : Robust.Client.State.State
    {
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IVoteManager _voteManager = default!;
        /* CorvaxGoob-Coins-start
        [Dependency] private readonly ServerCurrencySystem _serverCur = default!; // Goobstation - server currency
        [Dependency] private readonly LinkAccountManager _linkAccount = default!; // RMC - Patreon
        CorvaxGoob-Coins-end */
        [Dependency] private readonly IPrototypeManager _protoMan = default!; // Goobstation - credits
        [Dependency] private readonly ClientsidePlaytimeTrackingManager _playtimeTracking = default!;

        private ISawmill _sawmill = default!; // Goobstation
        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            //_contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            //_contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;
            _sawmill = Logger.GetSawmill("lobby");

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            // Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
            //     ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
            //     : lobbyNameCvar;

            //var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            //Lobby.RightSide.SetWidth = width;

            UpdateLobbyUi();

            // Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            //Lobby.CharacterPreview.PatronPerks.OnPressed += OnPatronPerksPressed; CorvaxGoob-Coins
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            // _serverCur.BalanceChange += UpdatePlayerBalance; // Goobstation - Goob Coin
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            //_contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;
            // _serverCur.BalanceChange -= UpdatePlayerBalance; // Goobstation - Goob Coin

            _voteManager.ClearPopupContainer();

            //Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            //Lobby.CharacterPreview.PatronPerks.OnPressed -= OnPatronPerksPressed; CorvaxGoob-Coins
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            new NFLateJoinGui().OpenCentered(); // Edit
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            //Lobby!.CharacterPreview.PatronPerks.Visible = _linkAccount.CanViewPatronPerks(); CorvaxGoob-Coins

            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready" : "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ObserveButton.Disabled = true;
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }
        }

            // UpdatePlayerBalance(); // Goobstation - Goob Coin

        //     var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
        //     if (minutesToday > 60)
        //     {
        //         Lobby!.PlaytimeComment.Visible = true;

        //         var hoursToday = Math.Round(minutesToday / 60f, 1);

        //         var chosenString = minutesToday switch
        //         {
        //             < 180 => "lobby-state-playtime-comment-normal",
        //             < 360 => "lobby-state-playtime-comment-concerning",
        //             < 720 => "lobby-state-playtime-comment-grasstouchless",
        //             _ => "lobby-state-playtime-comment-selfdestructive"
        //         };

        //         Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
        //     }
        //     else
        //         Lobby!.PlaytimeComment.Visible = false;
        // }

        // private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        // {
        //     if (ev.SoundtrackFilename == null)
        //     {
        //         Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
        //     }
        //     else if (
        //         ev.SoundtrackFilename != null
        //         && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
        //         )
        //     {
        //         var lobbyStream = lobbySongResource.AudioStream;

        //         var title = string.IsNullOrEmpty(lobbyStream.Title)
        //             ? Loc.GetString("lobby-state-song-unknown-title")
        //             : lobbyStream.Title;

        //         var artist = string.IsNullOrEmpty(lobbyStream.Artist)
        //             ? Loc.GetString("lobby-state-song-unknown-artist")
        //             : lobbyStream.Artist;

        //         var markup = Loc.GetString("lobby-state-song-text",
        //             ("songTitle", title),
        //             ("songArtist", artist));

        //         Lobby!.LobbySong.SetMarkup(markup);
        //     }
        // }


        private void UpdateLobbyBackground()
        {
            if (_gameTicker.LobbyBackground != null)
            {
                Lobby!.Background.SetRSI(_resourceCache.GetResource<RSIResource>(_gameTicker.LobbyBackground).RSI);
            }
            else
            {
                Lobby!.Background.Texture = null;
            }

        }


        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        //private void UpdatePlayerBalance() // Goobstation - Goob Coin
        //{
        //    Lobby!.Balance.Text = _serverCur.Stringify(_serverCur.GetBalance());
        //}
    }
}
