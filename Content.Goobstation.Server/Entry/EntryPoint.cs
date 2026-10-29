using Content.Goobstation.Server.Achievements;
using Content.Goobstation.Server.VoiceChat;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Goobstation.Server.Entry;

public sealed class EntryPoint : GameServer
{
    // Mini: voice chat (restored from Goobstation upstream, was deleted by CorvaxGoob)
    private VoiceChatManager _voiceManager = default!;
    private VoiceLogManager _voiceLogs = default!;
    // private ICommonCurrencyManager _curr = default!; // deleted by CorvaxGoob
    // private IJoinQueueManager _joinQueue = default!; // deleted by CorvaxGoob

    public override void Init()
    {
        base.Init();

        // ServerGoobContentIoC.Register(); // deleted by CorvaxGoob

        // Mini: register the managers that used to live in the deleted ServerGoobContentIoC
        IoCManager.Register<AchievementManager>();
        IoCManager.Register<VoiceChatManager>();
        IoCManager.Register<VoiceLogManager>();

        IoCManager.BuildGraph();

        IoCManager.Resolve<IPrototypeManager>().RegisterIgnore("newsEntry");

        _voiceManager = IoCManager.Resolve<VoiceChatManager>(); // Mini: voice chat
        _voiceManager.Initialize();

        _voiceLogs = IoCManager.Resolve<VoiceLogManager>();
        _voiceLogs.Initialize();

        // _joinQueue = IoCManager.Resolve<IJoinQueueManager>(); // deleted by CorvaxGoob
        // _curr = IoCManager.Resolve<ICommonCurrencyManager>(); // deleted by CorvaxGoob
    }

    public override void Update(ModUpdateLevel level, FrameEventArgs frameEventArgs)
    {
        base.Update(level, frameEventArgs);

        switch (level)
        {
            case ModUpdateLevel.PreEngine:
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // _curr.Shutdown(); // deleted by CorvaxGoob
        _voiceManager.Shutdown(); // Mini: voice chat
        _voiceLogs.Shutdown();
    }
}
