// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Station.Systems;
using Content.Shared._Mini.TypanWar;
using Content.Shared.Lock;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using System.Numerics;

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
        SpawnWarArmories();
        UnlockArmoriesOnStation(ev.NtStation);
        UnlockArmoriesOnStation(ev.TypanStation);
    }

    /// <summary>
    /// Maps carry no mapper-marked war armories, so a stocked locker is spawned next to each drop shuttle console.
    /// </summary>
    private void SpawnWarArmories()
    {
        var query = EntityQueryEnumerator<TypanWarDropShuttleConsoleComponent, TransformComponent>();
        while (query.MoveNext(out _, out var console, out var xform))
        {
            var proto = console.Side == TypanWarSide.Nanotrasen
                ? "TypanWarArmoryLockerNt"
                : "TypanWarArmoryLockerTypan";

            Spawn(proto, xform.Coordinates.Offset(new Vector2(0, -1)));
        }
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
