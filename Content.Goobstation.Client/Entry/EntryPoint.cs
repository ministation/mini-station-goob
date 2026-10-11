// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Sara Aldrete's Top Guy <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// Mini: voice chat only exists in DEVELOPMENT builds. It references raw Lidgren/NAudio/Concentus
// types, which fail the wizden engine's client sandbox typecheck, so packaged (FULL_RELEASE)
// clients crash on connect. It can come back once players get a launcher that ships our engine.
#if DEVELOPMENT
using Content.Goobstation.Client.Voice;
#endif
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
#if DEVELOPMENT
    // Mini: voice chat (restored from Goobstation upstream, was deleted by CorvaxGoob)
    [Dependency] private readonly IVoiceChatManager _voiceManager = default!;
#endif
    [Dependency] private readonly IInputManager _inputManager = default!;
    // [Dependency] private readonly JoinQueueManager _joinQueue = default!; // deleted by CorvaxGoob
    // [Dependency] private readonly PollManager _pollManager = default!; // deleted by CorvaxGoob
    // [Dependency] private readonly ICommonCurrencyManager _currMan = default!; // deleted by CorvaxGoob

    public override void Init()
    {
        // ContentGoobClientIoC.Register(); CorvaxGoob

#if DEVELOPMENT
        // Mini: register voice chat before the graph is built
        IoCManager.Register<IVoiceChatManager, VoiceChatClientManager>();
#endif

        IoCManager.BuildGraph();
        IoCManager.InjectDependencies(this);

#if DEVELOPMENT
        // Mini: push-to-talk binding
        _inputManager.SetInputCommand(
            ContentKeyFunctions.VoiceChatPushToTalk,
            InputCmdHandler.FromDelegate(
                enabled: _ => _voiceManager.SetTransmitting(true),
                disabled: _ => _voiceManager.SetTransmitting(false)));
#endif
    }

    public override void PostInit()
    {
        base.PostInit();

#if DEVELOPMENT
        _voiceManager.Initalize(); // Mini: voice chat
#endif
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
#if DEVELOPMENT
                _voiceManager.Update(); // Mini: voice chat
#endif
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // _currMan.Shutdown(); // deleted by CorvaxGoob
#if DEVELOPMENT
        _voiceManager.Shutdown(); // Mini: voice chat
#endif
    }
}
