
using Content.Goobstation.Server.Voice;
using Content.Goobstation.Common.ServerCurrency;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Entry;

public sealed class EntryPoint : GameServer
{
    // Mini: voice chat (restored from Goobstation upstream, was deleted by CorvaxGoob)
    private IVoiceChatServerManager _voiceManager = default!;
    // private ICommonCurrencyManager _curr = default!; // deleted by CorvaxGoob
    // private IJoinQueueManager _joinQueue = default!; // deleted by CorvaxGoob

    public override void Init()
    {
        base.Init();

        // ServerGoobContentIoC.Register(); // deleted by CorvaxGoob

        // Mini: register voice chat before the graph is built
        IoCManager.Register<IVoiceChatServerManager, VoiceChatServerManager>();

        IoCManager.BuildGraph();

        _voiceManager = IoCManager.Resolve<IVoiceChatServerManager>(); // Mini: voice chat

        // _joinQueue = IoCManager.Resolve<IJoinQueueManager>(); // deleted by CorvaxGoob
        // _curr = IoCManager.Resolve<ICommonCurrencyManager>(); // deleted by CorvaxGoob
    }

    public override void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
    {
        base.Update(level, frameEventArgs);

        switch (level)
        {
            case ModUpdateLevel.PreEngine:
                _voiceManager.Update(); // Mini: voice chat relay
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _voiceManager.Shutdown(); // Mini: voice chat
    }
}
