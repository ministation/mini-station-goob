// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Lock;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.TypanWar;

/// <summary>
/// Unlocks war armory lockers tagged with <see cref="TypanWarArmoryTag"/> when combat begins.
/// </summary>
public sealed class TypanWarArmorySystem : EntitySystem
{
    public static readonly ProtoId<TagPrototype> TypanWarArmoryTag = "TypanWarArmory";

    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TypanWarStartedEvent>(OnWarStarted);
    }

    private void OnWarStarted(TypanWarStartedEvent ev)
    {
        UnlockArmoriesOnStation(ev.NtStation);
        UnlockArmoriesOnStation(ev.TypanStation);
    }

    private void UnlockArmoriesOnStation(EntityUid station)
    {
        var query = EntityQueryEnumerator<LockComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lockComp, out _))
        {
            if (!lockComp.Locked || !_tag.HasTag(uid, TypanWarArmoryTag))
                continue;

            if (_station.GetOwningStation(uid) != station)
                continue;

            _lock.Unlock(uid, null, lockComp);
        }
    }
}
