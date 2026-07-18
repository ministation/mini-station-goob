using System.Linq;
using Content.Goobstation.Shared.Xenobiology.Components;
using Content.Server.Chat.Systems;
using Content.Shared._Arcane.CCVars;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Log;

namespace Content.Server._Arcane.Cleaning;

public sealed partial class AutoCleaningSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    private ISawmill _sawmill = default!;

    private bool _autoCleaningEnabled = false;
    private bool _isActive = false;
    private bool _isWarned = false;
    // Instance (not static): static timers survive TestPair recycle and wipe Trash entities mid-EntityTest.
    private TimeSpan _nextUpdate = TimeSpan.MaxValue;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WarningWaiting = TimeSpan.FromSeconds(30);
    private static readonly HashSet<ProtoId<TagPrototype>> CleaningTags = ["Trash", "Cartridge"];
    private static readonly HashSet<ProtoId<TagPrototype>> DisallowedTags = ["Cigarette", "CigPack", "Syringe", "LightTube", "LightBulb", "LightTubeCrystalRed", "LightTubeCrystalBlue", "LightTubeCrystalGreen"];

    private const int MaxCleanPerCycle = 500;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("autocleaning");

        Subs.CVar(_cfg, ACCVars.AutoCleaningEnabled, SetAutoCleaningEnabled, true);

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        _nextUpdate = _timing.CurTime + UpdateInterval;
        _isActive = true;
        _isWarned = false;

        _sawmill.Info("AutoCleaning system activated for the round");
    }

    private void OnRoundEnded(RoundEndedEvent args)
    {
        _isActive = false;
        _isWarned = false;
        _nextUpdate = TimeSpan.MaxValue;

        _sawmill.Info("AutoCleaning system deactivated (round ended)");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_isActive || !_autoCleaningEnabled)
            return;

        if (_nextUpdate < _timing.CurTime)
        {
            if (_isWarned)
            {
                ProccessCleaning();
                return;
            }

            _nextUpdate = _timing.CurTime + WarningWaiting;
            _isWarned = true;

            _chat.DispatchGlobalAnnouncement(Loc.GetString("cent-com-cleaning-warning", ("seconds", WarningWaiting.Seconds)), colorOverride: Color.Aqua);

            _sawmill.Info($"AutoCleaning warning sent. Cleaning in {WarningWaiting.Seconds} seconds");
        }
    }

    private void ProccessCleaning()
    {
        _nextUpdate = _timing.CurTime + UpdateInterval;
        _isWarned = false;

        _sawmill.Info($"Starting floor cleaning cycle (next in {UpdateInterval.TotalMinutes} min)...");

        var cleanedCount = CleanFloorItems();

        if (cleanedCount > 0)
        {
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("cent-com-cleaning-announce", ("count", cleanedCount)),
                colorOverride: Color.Aqua
            );

            _sawmill.Info($"Cleaning completed: {cleanedCount} items removed from floor");
        }
        else
        {
            _sawmill.Info("Cleaning completed: no items to remove");
        }
    }

    private int CleanFloorItems()
    {
        var deletedCount = 0;
        var skippedContainer = 0;
        var skippedAnchored = 0;
        var skippedDisallowed = 0;

        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var tag, out var transform))
        {
            if (!tag.Tags.Intersect(CleaningTags).Any())
                continue;

            if (tag.Tags.Intersect(DisallowedTags).Any())
            {
                skippedDisallowed++;
                continue;
            }

            if (transform.ParentUid != transform.GridUid && transform.ParentUid != transform.MapUid)
            {
                skippedContainer++;
                continue;
            }

            if (transform.Anchored)
            {
                skippedAnchored++;
                continue;
            }

            if (HasComp<ActorComponent>(uid)
                || (TryComp<MindContainerComponent>(uid, out var mindContainer) && mindContainer.HasMind)
                || HasComp<MobStateComponent>(uid))
            {
                continue;
            }

            if (HasComp<SlimeExtractComponent>(uid))
                continue;

            QueueDel(uid);
            deletedCount++;

            if (deletedCount >= MaxCleanPerCycle)
                break;
        }

        _sawmill.Debug($"Cleaning stats: deleted={deletedCount}, skipped (container={skippedContainer}, anchored={skippedAnchored}, disallowed={skippedDisallowed})");

        if (deletedCount >= MaxCleanPerCycle)
            _sawmill.Warning($"Reached max clean limit ({MaxCleanPerCycle}). Remaining items will be cleaned next cycle.");

        return deletedCount;
    }

    private void SetAutoCleaningEnabled(bool value)
    {
        _autoCleaningEnabled = value;
        _sawmill.Info($"AutoCleaning enabled set to: {value}");
    }
}
