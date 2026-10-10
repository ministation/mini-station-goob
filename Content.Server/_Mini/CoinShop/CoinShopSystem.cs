// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Mini.AntagTokens;
using Content.Server.Database;
using Content.Shared._Mini.CoinShop;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mini.CoinShop;

/// <summary>
/// Coin shop: cosmetic clothing, OOC nickname colors and a lootbox.
/// Ownership is stored in player_antag_token rows (keys "cosmetic:{id}", "ooc-color:{id}", "ooc-color-expire"),
/// spending goes through AntagTokenSystem so the in-memory balance stays authoritative.
/// </summary>
public sealed class CoinShopSystem : EntitySystem
{
    public const int LootboxCost = 25;
    private const int OocColorDays = 30;
    private const int DuplicateRefundPercent = 40;
    private const int DuplicateRefundMin = 5;
    private const string CosmeticKeyPrefix = "cosmetic:";
    private const string CosmeticSelectedSuffix = ":selected";
    private const string OocColorKeyPrefix = "ooc-color:";
    private const string OocColorExpireKey = "ooc-color-expire";

    private static readonly int[] RarityWeights = [40, 35, 20, 5];

    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;

    private readonly Dictionary<NetUserId, HashSet<string>> _ownedCosmetics = new();
    private readonly Dictionary<NetUserId, string?> _selectedCosmetics = new();
    private readonly Dictionary<NetUserId, string?> _oocColorId = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CoinShopOpenRequestEvent>(OnOpen);
        SubscribeNetworkEvent<CoinShopBuyCosmeticRequestEvent>(OnBuyCosmetic);
        SubscribeNetworkEvent<CoinShopBuyOocColorRequestEvent>(OnBuyOocColor);
        SubscribeNetworkEvent<CoinShopSelectCosmeticRequestEvent>(OnSelectCosmetic);
        SubscribeNetworkEvent<CoinShopLootboxRequestEvent>(OnLootbox);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        _userDb.AddOnLoadPlayer(LoadPlayerData);
        _userDb.AddOnPlayerDisconnect(OnDisconnect);
    }

    private async Task LoadPlayerData(ICommonSession player, CancellationToken cancel)
    {
        var tokens = await _db.GetPlayerAntagTokens(player.UserId.UserId, cancel);

        var owned = new HashSet<string>();
        string? selected = null;
        string? colorId = null;
        var expireDay = 0;

        foreach (var token in tokens)
        {
            if (token.Amount <= 0)
                continue;

            if (token.TokenId.StartsWith(CosmeticKeyPrefix))
            {
                var key = token.TokenId[CosmeticKeyPrefix.Length..];
                if (key.EndsWith(CosmeticSelectedSuffix))
                    selected = key[..^CosmeticSelectedSuffix.Length];
                else
                    owned.Add(key);
            }
            else if (token.TokenId.StartsWith(OocColorKeyPrefix))
            {
                colorId = token.TokenId[OocColorKeyPrefix.Length..];
            }
            else if (token.TokenId == OocColorExpireKey)
            {
                expireDay = token.Amount;
            }
        }

        _ownedCosmetics[player.UserId] = owned;
        _selectedCosmetics[player.UserId] = selected;
        _oocColorId[player.UserId] = colorId;

        if (colorId != null &&
            expireDay > CoinOocColorCache.Today &&
            _prototypes.TryIndex<CoinOocColorPrototype>(colorId, out var colorProto))
        {
            CoinOocColorCache.Set(player.UserId, colorProto.Color, expireDay);
        }
        else
        {
            CoinOocColorCache.Remove(player.UserId);
        }
    }

    private void OnDisconnect(ICommonSession player)
    {
        _ownedCosmetics.Remove(player.UserId);
        _selectedCosmetics.Remove(player.UserId);
        _oocColorId.Remove(player.UserId);
        CoinOocColorCache.Remove(player.UserId);
    }

    private void OnOpen(CoinShopOpenRequestEvent msg, EntitySessionEventArgs args) =>
        SendState(args.SenderSession);

    private async void OnBuyCosmetic(CoinShopBuyCosmeticRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_prototypes.TryIndex<CoinCosmeticPrototype>(msg.CosmeticId, out var proto) ||
            IsOwned(userId, msg.CosmeticId) ||
            !_antagTokens.TrySpendBalance(userId, proto.Price, out _))
        {
            SendState(args.SenderSession);
            return;
        }

        await _db.SetPlayerAntagTokenAmount(userId.UserId, CosmeticKeyPrefix + msg.CosmeticId, 1);
        if (!_ownedCosmetics.TryGetValue(userId, out var ownedSet))
        {
            ownedSet = new HashSet<string>();
            _ownedCosmetics[userId] = ownedSet;
        }
        ownedSet.Add(msg.CosmeticId);
        SendState(args.SenderSession);
    }

    private async void OnSelectCosmetic(CoinShopSelectCosmeticRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var oldSelected = _selectedCosmetics.TryGetValue(userId, out var value) ? value : null;

        if (msg.CosmeticId == null)
        {
            if (oldSelected != null)
                await _db.SetPlayerAntagTokenAmount(userId.UserId, SelectedKey(oldSelected), 0);

            _selectedCosmetics[userId] = null;
            SendState(args.SenderSession);
            return;
        }

        if (!IsOwned(userId, msg.CosmeticId))
        {
            SendState(args.SenderSession);
            return;
        }

        if (oldSelected != null && oldSelected != msg.CosmeticId)
            await _db.SetPlayerAntagTokenAmount(userId.UserId, SelectedKey(oldSelected), 0);

        await _db.SetPlayerAntagTokenAmount(userId.UserId, SelectedKey(msg.CosmeticId), 1);
        _selectedCosmetics[userId] = msg.CosmeticId;
        SendState(args.SenderSession);
    }

    private async void OnBuyOocColor(CoinShopBuyOocColorRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_prototypes.TryIndex<CoinOocColorPrototype>(msg.ColorId, out var proto) ||
            !_antagTokens.TrySpendBalance(userId, proto.Price, out _))
        {
            SendState(args.SenderSession);
            return;
        }

        // Renew an active purchase from its expiry date; a new color starts fresh and replaces the old one.
        var newExpire = (CoinOocColorCache.TryGetExpire(userId, out var currentExpire) ? currentExpire : CoinOocColorCache.Today)
            + OocColorDays;

        var oldColorId = _oocColorId.TryGetValue(userId, out var value) ? value : null;
        if (oldColorId != null && oldColorId != msg.ColorId)
            await _db.SetPlayerAntagTokenAmount(userId.UserId, OocColorKeyPrefix + oldColorId, 0);

        await _db.SetPlayerAntagTokenAmount(userId.UserId, OocColorKeyPrefix + msg.ColorId, 1);
        await _db.SetPlayerAntagTokenAmount(userId.UserId, OocColorExpireKey, newExpire);

        _oocColorId[userId] = msg.ColorId;
        CoinOocColorCache.Set(userId, proto.Color, newExpire);
        SendState(args.SenderSession);
    }

    private async void OnLootbox(CoinShopLootboxRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_antagTokens.TrySpendBalance(userId, LootboxCost, out _))
        {
            SendState(args.SenderSession);
            return;
        }

        var pick = RollRandomCosmetic();
        if (pick == null)
        {
            // Empty catalog: refund instead of eating the coins.
            _antagTokens.PayBalance(userId, LootboxCost);
            SendState(args.SenderSession);
            return;
        }

        var refund = 0;
        var duplicate = IsOwned(userId, pick.ID);
        if (duplicate)
        {
            refund = Math.Max(DuplicateRefundMin, pick.Price * DuplicateRefundPercent / 100);
            _antagTokens.PayBalance(userId, refund);
        }
        else
        {
            await _db.SetPlayerAntagTokenAmount(userId.UserId, CosmeticKeyPrefix + pick.ID, 1);
            if (!_ownedCosmetics.TryGetValue(userId, out var ownedSet))
            {
                ownedSet = new HashSet<string>();
                _ownedCosmetics[userId] = ownedSet;
            }
            ownedSet.Add(pick.ID);
        }

        RaiseNetworkEvent(new CoinShopRollResultEvent(pick.Name, pick.Rarity, refund, duplicate), args.SenderSession);
        SendState(args.SenderSession);
    }

    private CoinCosmeticPrototype? RollRandomCosmetic()
    {
        var roll = _random.Next(100);
        var rarity = roll < RarityWeights[0]
            ? CoinRarity.Common
            : roll < RarityWeights[0] + RarityWeights[1]
                ? CoinRarity.Rare
                : roll < RarityWeights[0] + RarityWeights[1] + RarityWeights[2]
                    ? CoinRarity.Epic
                    : CoinRarity.Legendary;

        var pool = _prototypes.EnumeratePrototypes<CoinCosmeticPrototype>()
            .Where(p => p.Rarity == rarity)
            .ToList();

        if (pool.Count == 0)
        {
            // Rarity tier has no items: fall back to anything in the catalog.
            pool = _prototypes.EnumeratePrototypes<CoinCosmeticPrototype>().ToList();
        }

        return pool.Count > 0 ? _random.Pick(pool) : null;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.Player == null)
            return;

        if (!_selectedCosmetics.TryGetValue(args.Player.UserId, out var cosmeticId) || cosmeticId == null)
            return;

        if (!_prototypes.TryIndex<CoinCosmeticPrototype>(cosmeticId, out var proto))
            return;

        var item = Spawn(proto.Item, Transform(args.Mob).Coordinates);
        _inventory.TryEquip(args.Mob, item, proto.Slot, silent: true);
    }

    private bool IsOwned(NetUserId userId, string cosmeticId) =>
        _ownedCosmetics.TryGetValue(userId, out var owned) && owned.Contains(cosmeticId);

    private async void SendState(ICommonSession session)
    {
        var userId = session.UserId;
        var balance = _antagTokens.GetBalance(userId);
        _ownedCosmetics.TryGetValue(userId, out var owned);
        _selectedCosmetics.TryGetValue(userId, out var selected);
        _oocColorId.TryGetValue(userId, out var colorId);
        var colorActive = CoinOocColorCache.TryGet(userId, out _);

        var cosmetics = _prototypes.EnumeratePrototypes<CoinCosmeticPrototype>()
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Price)
            .Select(p => new CoinShopItemEntry(p.ID, p.Name, p.Price, p.Rarity, owned?.Contains(p.ID) ?? false))
            .ToList();

        var colors = _prototypes.EnumeratePrototypes<CoinOocColorPrototype>()
            .OrderBy(p => p.Order)
            .Select(p => new CoinShopColorEntry(p.ID, p.Color, p.Price, colorId == p.ID && colorActive, colorId == p.ID && colorActive))
            .ToList();

        RaiseNetworkEvent(new CoinShopStateEvent(balance, cosmetics, colors, selected), session);
    }

    private static string SelectedKey(string cosmeticId) => CosmeticKeyPrefix + cosmeticId + CosmeticSelectedSuffix;
}
