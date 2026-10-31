// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using System.Numerics;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared.EntityTable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Server side of the fishing treasure chest: opens the lid, flickers a random prize above it,
/// then hands the prize to whoever clicks the chest again.
/// </summary>
public sealed partial class FishingTreasureChestSystem : EntitySystem
{
    private const float CycleInterval = 0.1f;

    private static readonly SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/woodenclosetopen.ogg");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Effects/woodenclosetclose.ogg");
    private static readonly SoundSpecifier GrantSound = new SoundPathSpecifier("/Audio/Effects/chime.ogg");

    [Dependency] private EntityTableSystem _tables = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    private readonly Dictionary<EntityUid, EntityUid> _prizes = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextAction = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextCycle = new();
    private readonly Dictionary<EntityUid, Dictionary<NetUserId, int>> _pulls = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingTreasureChestComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FishingTreasureChestComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnActivate(Entity<FishingTreasureChestComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!_net.IsServer || args.Handled)
            return;

        args.Handled = true;

        switch (ent.Comp.State)
        {
            case FishingTreasureChestState.Closed:
                TryOpen(ent, args.User);
                break;
            case FishingTreasureChestState.Open:
                TakePrize(ent, args.User);
                break;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FishingTreasureChestComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            switch (comp.State)
            {
                case FishingTreasureChestState.Choosing:
                    if (now >= GetNext(_nextAction, uid))
                        RevealPrize((uid, comp));
                    else if (now >= GetNext(_nextCycle, uid))
                        CyclePrize((uid, comp));
                    break;
                case FishingTreasureChestState.Open:
                    if (now >= GetNext(_nextAction, uid))
                        CloseChest((uid, comp), false);
                    break;
            }
        }
    }

    private void TryOpen(Entity<FishingTreasureChestComponent> ent, EntityUid user)
    {
        if (ent.Comp.UsesLeft <= 0 || _timing.CurTime < GetNext(_nextAction, ent.Owner))
            return;

        if (_player.TryGetSessionByEntity(user, out var session) &&
            GetPulls(ent.Owner).GetValueOrDefault(session.UserId) >= ent.Comp.PullsPerPlayer)
        {
            _popup.PopupEntity(Loc.GetString("fishing-treasure-chest-refuses"), ent, user);
            return;
        }

        if (PickPrize(ent.Comp) is not { } prize)
            return;

        var prizeEntity = Spawn(ent.Comp.PrizePrototype, Transform(ent).Coordinates);
        _xform.SetParent(prizeEntity, ent.Owner);
        _xform.SetLocalPositionNoLerp(prizeEntity, new Vector2(0f, 0.6f));
        _prizes[ent.Owner] = prizeEntity;

        ent.Comp.State = FishingTreasureChestState.Choosing;
        ent.Comp.Prize = prize;
        Dirty(ent, ent.Comp);
        SetPrize(prizeEntity, prize);

        _audio.PlayPvs(OpenSound, ent.Owner);
        _nextAction[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.ChoosingTime);
        _nextCycle[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(CycleInterval);
    }

    private void CyclePrize(Entity<FishingTreasureChestComponent> ent)
    {
        if (PickPrize(ent.Comp) is not { } prize)
            return;

        ent.Comp.Prize = prize;
        Dirty(ent, ent.Comp);

        if (_prizes.TryGetValue(ent.Owner, out var prizeEntity))
            SetPrize(prizeEntity, prize);

        _nextCycle[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(CycleInterval);
    }

    private void RevealPrize(Entity<FishingTreasureChestComponent> ent)
    {
        if (PickPrize(ent.Comp) is { } prize)
        {
            ent.Comp.Prize = prize;
            if (_prizes.TryGetValue(ent.Owner, out var prizeEntity))
                SetPrize(prizeEntity, prize);
        }

        ent.Comp.State = FishingTreasureChestState.Open;
        Dirty(ent, ent.Comp);

        _audio.PlayPvs(GrantSound, ent.Owner);
        _nextAction[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.PresentTime);
        _nextCycle.Remove(ent.Owner);
    }

    private void TakePrize(Entity<FishingTreasureChestComponent> ent, EntityUid user)
    {
        if (ent.Comp.Prize is not { } prize)
            return;

        var item = Spawn(prize, Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(user, item);

        _popup.PopupEntity(Loc.GetString("fishing-treasure-chest-pull", ("item", Name(item))), user, user);
        _audio.PlayPvs(GrantSound, ent.Owner);

        if (_player.TryGetSessionByEntity(user, out var session))
        {
            var pulls = GetPulls(ent.Owner);
            pulls[session.UserId] = pulls.GetValueOrDefault(session.UserId) + 1;
        }

        RaiseLocalEvent(user, new FishingTreasureChestPullEvent());

        ent.Comp.UsesLeft--;

        if (ent.Comp.UsesLeft <= 0)
            BreakChest(ent);
        else
            CloseChest(ent, true);
    }

    private void CloseChest(Entity<FishingTreasureChestComponent> ent, bool playSound)
    {
        if (_prizes.Remove(ent.Owner, out var prizeEntity) && !TerminatingOrDeleted(prizeEntity))
            QueueDel(prizeEntity);

        ent.Comp.State = FishingTreasureChestState.Closed;
        ent.Comp.Prize = null;
        Dirty(ent, ent.Comp);

        _nextAction[ent.Owner] = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Cooldown);
        _nextCycle.Remove(ent.Owner);

        if (playSound)
            _audio.PlayPvs(CloseSound, ent.Owner);
    }

    private void BreakChest(Entity<FishingTreasureChestComponent> ent)
    {
        if (_prizes.Remove(ent.Owner, out var prizeEntity) && !TerminatingOrDeleted(prizeEntity))
            QueueDel(prizeEntity);

        _nextAction.Remove(ent.Owner);
        _nextCycle.Remove(ent.Owner);
        _pulls.Remove(ent.Owner);

        _audio.PlayPvs(CloseSound, ent.Owner);
        QueueDel(ent.Owner);
    }

    private EntProtoId? PickPrize(FishingTreasureChestComponent comp)
    {
        var spawns = _tables.GetSpawns(_proto.Index(comp.Table), _random.GetRandom()).ToList();
        return spawns.Count == 0 ? (EntProtoId?) null : _random.Pick(spawns);
    }

    private void SetPrize(EntityUid prizeEntity, EntProtoId? prize)
    {
        if (!TryComp<FishingTreasureChestPrizeComponent>(prizeEntity, out var comp))
            return;

        comp.Prize = prize;
        Dirty(prizeEntity, comp);
    }

    private Dictionary<NetUserId, int> GetPulls(EntityUid chest)
    {
        if (_pulls.TryGetValue(chest, out var pulls))
            return pulls;

        pulls = new Dictionary<NetUserId, int>();
        _pulls[chest] = pulls;
        return pulls;
    }

    private static TimeSpan GetNext(Dictionary<EntityUid, TimeSpan> timers, EntityUid uid)
    {
        return timers.TryGetValue(uid, out var next) ? next : TimeSpan.Zero;
    }

    private void OnShutdown(Entity<FishingTreasureChestComponent> ent, ref ComponentShutdown args)
    {
        if (_prizes.Remove(ent.Owner, out var prizeEntity) && !TerminatingOrDeleted(prizeEntity))
            QueueDel(prizeEntity);

        _nextAction.Remove(ent.Owner);
        _nextCycle.Remove(ent.Owner);
        _pulls.Remove(ent.Owner);
    }
}
