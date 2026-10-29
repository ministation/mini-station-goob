// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Reflection;

namespace Content.Client.Eui
{
    public sealed class EuiManager
    {
        [Dependency] private readonly IClientNetManager _net = default!;
        [Dependency] private readonly IReflectionManager _refl = default!;
        [Dependency] private readonly IDynamicTypeFactory _dtf = default!;

        private readonly Dictionary<uint, EuiData> _openUis = new();

        public void Initialize()
        {
            _net.RegisterNetMessage<MsgEuiCtl>(RxMsgCtl);
            _net.RegisterNetMessage<MsgEuiState>(RxMsgState);
            _net.RegisterNetMessage<MsgEuiMessage>(RxMsgMessage);
            _net.Disconnect += NetOnDisconnect;
        }

        private void NetOnDisconnect(object? sender, NetDisconnectedArgs e)
        {
            foreach (var openUi in _openUis)
            {
                openUi.Value.Eui.Closed();
            }
            _openUis.Clear();
        }

        private void RxMsgMessage(MsgEuiMessage message)
        {
            var ui = _openUis[message.Id].Eui;
            ui.HandleMessage(message.Message);
        }

        private void RxMsgState(MsgEuiState message)
        {
            var ui = _openUis[message.Id].Eui;
            ui.HandleState(message.State);
        }

        private void RxMsgCtl(MsgEuiCtl message)
        {
            // Will always close the window first when getting a control message
            if (_openUis.TryGetValue(message.Id, out var openEui))
            {
                openEui.Eui.Closed();
                _openUis.Remove(message.Id);
            }

            if (message.Type != MsgEuiCtl.CtlType.Open)
                return;

            // Will open/re-open the window if the server wants the eui opened.
            var euiType = FindExactEuiType(message.OpenType, _refl.FindAllTypes())
                ?? _refl.LooseGetType(message.OpenType);
            var instance = _dtf.CreateInstance<BaseEui>(euiType);
            instance.Initialize(this, message.Id);
            instance.Opened();
            _openUis.Add(message.Id, new EuiData(instance));
        }

        /// <summary>
        /// Finds the client EUI type whose name exactly matches the type name sent by the server.
        /// </summary>
        /// <remarks>
        /// The server only sends <see cref="Type.Name"/>, hence an exact match is what it means.
        /// <see cref="IReflectionManager.LooseGetType"/> (used as a fallback) matches by suffix, which can return
        /// an unrelated EUI whose name merely ends with the requested one: for example "DeconvertedEui"
        /// (revolutionary deconversion) also matches "CosmicDeconvertedEui" (cosmic cult deconversion).
        /// </remarks>
        internal static Type? FindExactEuiType(string name, IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                if (type.Name == name && !type.IsAbstract && typeof(BaseEui).IsAssignableFrom(type))
                    return type;
            }

            return null;
        }

        private sealed class EuiData
        {
            public readonly BaseEui Eui;

            public EuiData(BaseEui eui)
            {
                Eui = eui;
            }
        }
    }
}