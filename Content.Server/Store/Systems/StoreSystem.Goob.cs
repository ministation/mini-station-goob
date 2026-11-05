using Content.Goobstation.Maths.FixedPoint;
using Content.Server.GameTicking;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    private const string TreeInterface = "StoreTreeBoundUserInterface";

    private void InitializeGoob()
    {
        SubscribeLocalEvent<StoreComponent, PolymorphedEvent>(OnPolymorphed);
    }

    private void OnPolymorphed(Entity<StoreComponent> ent, ref PolymorphedEvent args)
    {
        if (args.IsRevert)
            return;

        _polymorph.CopyPolymorphComponent<StoreComponent>(ent, args.NewEntity);
    }

    /// <summary>
    /// Sets the restock timestamp of a purchased listing.
    /// </summary>
    /// <remarks>
    /// <see cref="ListingData.RestockTime"/> is stored in station time (time since the round start),
    /// the same clock the client store UI compares against. Keep every calculation in that clock,
    /// otherwise the client countdown is shifted by the round start time (see issue #187).
    /// </remarks>
    private void OnPurchase(ListingData listing)
    {
        if (!_proto.TryIndex<ListingPrototype>(listing.ID, out var prototype))
            return;

        if (!listing.ResetRestockOnPurchase)
            return;

        // RestockAfterPurchase is the cooldown after a purchase; prototype.RestockTime is the
        // initial restock delay, which is the only duration available when the cooldown is omitted.
        // Never use listing.RestockTime as the fallback here: it is already a station time
        // timestamp, so adding it as a duration would shift the timer by the current round time.
        var restockDuration = listing.RestockAfterPurchase ?? prototype.RestockTime;
        var stationTime = _timing.CurTime.Subtract(_ticker.RoundStartTimeSpan);
        listing.RestockTime = stationTime + restockDuration;
    }

    private void EnsureTreeInterface(Entity<StoreComponent> ent)
    {
        if (ent.Comp.Tree == null)
            return;

        if (!_ui.TryGetInterfaceData(ent.Owner, StoreUiKey.Key, out var data) || data.ClientType == TreeInterface)
            return;

        _ui.SetUi(ent.Owner, StoreUiKey.Key, new InterfaceData(TreeInterface, data.InteractionRange, data.RequireInputValidation));
    }

    private bool TrySetTreeUiState(Entity<StoreComponent> ent, Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> balance)
    {
        if (ent.Comp.Tree is not { } treeId || !_proto.TryIndex(treeId, out var tree))
            return false;

        var listings = new HashSet<ListingDataWithCostModifiers>();
        foreach (var listing in ent.Comp.LastAvailableListings)
        {
            if (tree.Nodes.ContainsKey(listing.ID))
                listings.Add(listing);
        }

        var purchased = new HashSet<ProtoId<ListingPrototype>>();
        foreach (var listing in ent.Comp.FullListingsCatalog)
        {
            if (listing.PurchaseAmount > 0 && tree.Nodes.ContainsKey(listing.ID))
                purchased.Add(listing.ID);
        }

        _ui.SetUiState(ent.Owner, StoreUiKey.Key, new StoreTreeUpdateState(treeId, listings, purchased, balance));
        return true;
    }
}
