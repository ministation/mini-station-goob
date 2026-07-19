// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._DV.Salvage.Components;
using Content.Shared._Lavaland.UnclaimedOre;
using Content.Shared._Mini.DailyQuests;
using Content.Shared.Access.Systems;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._DV.Salvage.Systems;

public sealed class MiningPointsSystem : EntitySystem
{
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;

    private EntityQuery<MiningPointsComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<MiningPointsComponent>();

        SubscribeLocalEvent<MiningPointsLatheComponent, MaterialEntityInsertedEvent>(OnMaterialEntityInserted);
        Subs.BuiEvents<MiningPointsLatheComponent>(LatheUiKey.Key, subs =>
        {
            subs.Event<LatheClaimMiningPointsMessage>(OnClaimMiningPoints);
        });
    }

    #region Event Handlers

    private void OnMaterialEntityInserted(Entity<MiningPointsLatheComponent> ent, ref MaterialEntityInsertedEvent args)
    {
        // Server-authoritative. Event fires inside SharedMaterialStorageSystem.TryInsertMaterialEntity
        // before the server QueueDel's the ore, so UnclaimedOre is still readable here.
        // Do not require OreSiloClient.Silo — that left station processors at 0 until cargo linked them.
        if (_net.IsClient)
            return;

        if (!TryComp(args.Inserted, out UnclaimedOreComponent? unclaimedOre))
            return;

        if (!_query.TryComp(ent.Owner, out _))
            return;

        var points = (uint) Math.Max(0, Math.Floor(unclaimedOre.MiningPoints * args.Count));
        if (points > 0)
            AddPoints(ent.Owner, points);
    }

    private void OnClaimMiningPoints(Entity<MiningPointsLatheComponent> ent, ref LatheClaimMiningPointsMessage args)
    {
        if (_net.IsClient)
            return;

        var user = args.Actor;
        if (!_query.TryComp(ent.Owner, out var machinePoints) || machinePoints.Points == 0)
            return;

        if (GetPointComp(user) is not { } dest)
        {
            _popup.PopupEntity(Loc.GetString("lathe-menu-mining-points-claim-no-id"), ent.Owner, user);
            return;
        }

        if (!TransferAll((ent.Owner, machinePoints), dest))
            return;

        var claimed = new MiningPointsClaimedEvent(user);
        RaiseLocalEvent(ref claimed);
    }

    #endregion
    #region Public API
    /// <summary>
    /// if user can claim mining points
    /// <summary>
    public bool CanClaimPoints(EntityUid user) // Goobstation - borg Miningpoints
    {
        if (TryComp<MiningPointsComponent>(user, out _))
            return true;
        if (TryFindIdCard(user) != null)
            return true;

        return false;
    }

    /// <summary>
    /// returns Miningpoint component of user, if its directly atatched or on users Id card
    /// <summary>
    public Entity<MiningPointsComponent?>? GetPointComp(EntityUid user) // Goobstation - borg Miningpoints
    {
        if (TryComp<MiningPointsComponent>(user, out var comp))
            return (user, comp);
        return TryFindIdCard(user);
    }

    /// <summary>
    /// Tries to find the user's id card and gets its <see cref="MiningPointsComponent"/>.
    /// </summary>
    /// <remarks>
    /// Component is nullable for easy usage with the API due to Entity&lt;T&gt; not being usable for Entity&lt;T?&gt; arguments.
    /// </remarks>
    public Entity<MiningPointsComponent?>? TryFindIdCard(EntityUid user)
    {
        if (!_idCard.TryFindIdCard(user, out var idCard))
            return null;

        if (!_query.TryComp(idCard, out var comp))
            return null;

        return (idCard.Owner, comp);
    }

    /// <summary>
    /// Returns true if the user has at least some number of points on their ID card.
    /// </summary>
    public bool UserHasPoints(EntityUid user, uint points)
    {
        if (GetPointComp(user)?.Comp is not {} comp) // Goobstation - borg Miningpoints
            return false;

        return comp.Points >= points;
    }

    /// <summary>
    /// Removes points from a holder, returning true if it succeeded.
    /// </summary>
    public bool RemovePoints(Entity<MiningPointsComponent?> ent, uint amount)
    {
        if (!_query.Resolve(ent, ref ent.Comp) || amount > ent.Comp.Points)
            return false;

        ent.Comp.Points -= amount;
        Dirty(ent.Owner, ent.Comp);
        return true;
    }

    /// <summary>
    /// Add points to a holder.
    /// </summary>
    public bool AddPoints(Entity<MiningPointsComponent?> ent, uint amount)
    {
        if (!_query.Resolve(ent, ref ent.Comp))
            return false;

        ent.Comp.Points += amount;
        Dirty(ent.Owner, ent.Comp);
        return true;
    }

    /// <summary>
    /// Transfer a number of points from source to destination.
    /// Returns true if the transfer succeeded.
    /// </summary>
    public bool Transfer(Entity<MiningPointsComponent?> src, Entity<MiningPointsComponent?> dest, uint amount)
    {
        // don't make a sound or anything
        if (amount == 0)
            return true;

        if (!_query.Resolve(src, ref src.Comp) || !_query.Resolve(dest, ref dest.Comp))
            return false;

        if (!RemovePoints(src, amount))
            return false;

        AddPoints(dest, amount);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg"), src.Owner);
        return true;
    }

    /// <summary>
    /// Transfers all points from source to destination.
    /// Returns true if the transfer succeeded.
    /// </summary>
    public bool TransferAll(Entity<MiningPointsComponent?> src, Entity<MiningPointsComponent?> dest)
    {
        return _query.Resolve(src, ref src.Comp) && Transfer(src, dest, src.Comp.Points);
    }

    #endregion
}
