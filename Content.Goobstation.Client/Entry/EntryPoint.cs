// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Sara Aldrete's Top Guy <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Client.Voice;
using Content.Goobstation.Client.JoinQueue;
using Content.Goobstation.Common.ServerCurrency;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Entry;

public sealed class EntryPoint : GameClient
{
    // Mini: voice chat (restored from Goobstation upstream, was deleted by CorvaxGoob)
    [Dependency] private readonly IVoiceChatManager _voiceManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    // [Dependency] private readonly JoinQueueManager _joinQueue = default!; // deleted by CorvaxGoob
    // [Dependency] private readonly PollManager _pollManager = default!; // deleted by CorvaxGoob
    // [Dependency] private readonly ICommonCurrencyManager _currMan = default!; // deleted by CorvaxGoob

    public override void Init()
    {
        // ContentGoobClientIoC.Register(); CorvaxGoob

        // Mini: register voice chat before the graph is built
        IoCManager.Register<IVoiceChatManager, VoiceChatClientManager>();

        IoCManager.BuildGraph();
        IoCManager.InjectDependencies(this);

        // Mini: push-to-talk binding
        _inputManager.SetInputCommand(
            ContentKeyFunctions.VoiceChatPushToTalk,
            InputCmdHandler.FromDelegate(
                enabled: _ => _voiceManager.SetTransmitting(true),
                disabled: _ => _voiceManager.SetTransmitting(false)));
    }

    public override void PostInit()
    {
        base.PostInit();

        _voiceManager.Initalize(); // Mini: voice chat
        // _joinQueue.Initialize(); // deleted by CorvaxGoob
        // _pollManager.Initialize(); // deleted by CorvaxGoob
        // _currMan.Initialize(); // deleted by CorvaxGoob
    }

    public override void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
    {
        base.Update(level, frameEventArgs);

        switch (level)
        {
            case ModUpdateLevel.FramePreEngine:
                _voiceManager.Update(); // Mini: voice chat
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // _currMan.Shutdown(); // deleted by CorvaxGoob
        _voiceManager.Shutdown(); // Mini: voice chat
    }
}
