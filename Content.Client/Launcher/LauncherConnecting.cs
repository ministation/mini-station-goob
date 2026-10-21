// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Net;

namespace Content.Client.Launcher
{
    public sealed class LauncherConnecting : Robust.Client.State.State
    {
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IClientNetManager _clientNetManager = default!;
        [Dependency] private readonly IGameController _gameController = default!;
        [Dependency] private readonly IBaseClient _baseClient = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IClipboardManager _clipboard = default!;
        [Dependency] private readonly ILogManager _logManager = default!;

        // Joining a server starts with an HTTPS request to the central auth server. On some
        // networks (broken IPv6 routes, DPI middleboxes) that TLS connection is reset
        // sporadically and the whole join is aborted with "The SSL connection could not be
        // established". A fresh attempt usually gets through, so retry a few times instead of
        // dropping the player back to the "connection failed" screen on the first hiccup.
        private const int MaxTransientConnectRetries = 3;
        private static readonly TimeSpan TransientConnectRetryDelay = TimeSpan.FromSeconds(2);

        private LauncherConnectingGui? _control;
        private ISawmill _sawmill = default!;

        private Page _currentPage;
        private string? _connectFailReason;
        private int _transientConnectRetries;
        private int _connectAttemptGeneration;
        private bool _shuttingDown;

        public string? Address => _gameController.LaunchState.Ss14Address ?? _gameController.LaunchState.ConnectAddress;

        public DnsEndPoint? ConnectEndpoint => _gameController.LaunchState.ConnectEndpoint;

        public string? ConnectFailReason
        {
            get => _connectFailReason;
            private set
            {
                _connectFailReason = value;
                ConnectFailReasonChanged?.Invoke(value);
            }
        }

        public string? LastDisconnectReason => _baseClient.LastDisconnectReason;

        public Page CurrentPage
        {
            get => _currentPage;
            private set
            {
                _currentPage = value;
                PageChanged?.Invoke(value);
            }
        }

        public ClientConnectionState ConnectionState => _clientNetManager.ClientConnectState;

        public event Action<Page>? PageChanged;
        public event Action<string?>? ConnectFailReasonChanged;
        public event Action<ClientConnectionState>? ConnectionStateChanged;
        public event Action<NetConnectFailArgs>? ConnectFailed;

        protected override void Startup()
        {
            _transientConnectRetries = 0;
            _shuttingDown = false;

            _control = new LauncherConnectingGui(this, _random, _prototypeManager, _cfg, _clipboard);

            _sawmill = _logManager.GetSawmill("launcher-ui");

            _userInterfaceManager.StateRoot.AddChild(_control);

            _clientNetManager.ConnectFailed += OnConnectFailed;
            _clientNetManager.ClientConnectStateChanged += OnConnectStateChanged;

            CurrentPage = Page.Connecting;
        }

        protected override void Shutdown()
        {
            _shuttingDown = true;

            _control?.Dispose();

            _clientNetManager.ConnectFailed -= OnConnectFailed;
            _clientNetManager.ClientConnectStateChanged -= OnConnectStateChanged;
        }

        private void OnConnectFailed(object? _, NetConnectFailArgs args)
        {
            if (args.RedialFlag)
            {
                // We've just *attempted* to connect and we've been told we need to redial, so do it.
                // Result deliberately discarded.
                Redial();
            }
            else if (TryTransientRetry(args.Reason))
            {
                return;
            }

            ConnectFailReason = args.Reason;
            CurrentPage = Page.ConnectFailed;
            ConnectFailed?.Invoke(args);
        }

        /// <summary>
        ///     Retries the connect attempt when it failed because of a transient network problem
        ///     (TLS/connection reset while talking to the auth server), instead of giving up
        ///     immediately. The engine resets the connection state right after raising
        ///     <see cref="IClientNetManager.ConnectFailed"/>, so the retry has to be deferred.
        /// </summary>
        private bool TryTransientRetry(string reason)
        {
            if (_shuttingDown
                || _transientConnectRetries >= MaxTransientConnectRetries
                || !IsTransientNetworkReason(reason))
            {
                return false;
            }

            var endpoint = _gameController.LaunchState.ConnectEndpoint;
            if (endpoint == null)
                return false;

            _transientConnectRetries++;
            var generation = _connectAttemptGeneration;
            _sawmill.Warning(
                $"Connect attempt failed with a transient network error ({reason}), retrying " +
                $"{_transientConnectRetries}/{MaxTransientConnectRetries}");

            CurrentPage = Page.Connecting;
            Timer.Spawn(TransientConnectRetryDelay, () =>
            {
                // The player might have retried manually or left the screen while we were waiting.
                if (_shuttingDown
                    || generation != _connectAttemptGeneration
                    || _clientNetManager.ClientConnectState != ClientConnectionState.NotConnecting)
                {
                    return;
                }

                try
                {
                    _baseClient.ConnectToServer(endpoint);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Transient connect retry failed: {e}");
                }
            });

            return true;
        }

        private static bool IsTransientNetworkReason(string reason)
        {
            return reason.Contains("SSL connection could not be established", StringComparison.OrdinalIgnoreCase)
                   || reason.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase)
                   || reason.Contains("connection was forcibly closed", StringComparison.OrdinalIgnoreCase)
                   || reason.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
                   || reason.Contains("established connection was aborted", StringComparison.OrdinalIgnoreCase);
        }

        private void OnConnectStateChanged(ClientConnectionState state)
        {
            if (state == ClientConnectionState.Connected)
                _transientConnectRetries = 0;

            ConnectionStateChanged?.Invoke(state);
        }

        public void RetryConnect()
        {
            if (_gameController.LaunchState.ConnectEndpoint != null)
            {
                _transientConnectRetries = 0;
                _connectAttemptGeneration++;
                _baseClient.ConnectToServer(_gameController.LaunchState.ConnectEndpoint);
                CurrentPage = Page.Connecting;
            }
        }

        public bool Redial()
        {
            try
            {
                if (_gameController.LaunchState.Ss14Address != null)
                {
                    _gameController.Redial(_gameController.LaunchState.Ss14Address);
                    return true;
                }
                else
                {
                    _sawmill.Info($"Redial not possible, no Ss14Address");
                }
            }
            catch (Exception ex)
            {
                _sawmill.Error($"Redial exception: {ex}");
            }
            return false;
        }

        public void Exit()
        {
            _gameController.Shutdown("Exit button pressed");
        }

        public void SetDisconnected()
        {
            CurrentPage = Page.Disconnected;
        }

        public enum Page : byte
        {
            Connecting,
            ConnectFailed,
            Disconnected,
        }
    }
}