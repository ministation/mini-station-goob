using System.Linq;
using System.Threading.Tasks;
using Content.Server._Mini.AntagTokens;
using Content.Server.Database;
using Content.Shared.Ghost;
using Content.Shared._Mini.CustomGhost;
using Content.Shared._Mini.CoinShop;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.CustomGhost;

public sealed class CustomGhostSystem : EntitySystem
{
    public const string ThemeTokenPrefix = "ghost-theme:";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;
    [Dependency] private readonly HalloweenPlaytimeSystem _halloween = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<GhostShopOpenRequestEvent>(OnShopOpen);
        SubscribeNetworkEvent<GhostShopBuyRequestEvent>(OnShopBuy);
        SubscribeNetworkEvent<GhostShopSelectRequestEvent>(OnShopSelect);
        SubscribeNetworkEvent<GhostShopBuyColorRequestEvent>(OnShopBuyColor);
        SubscribeNetworkEvent<GhostShopSelectColorRequestEvent>(OnShopSelectColor);
    }

    private async void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
        if (EntityManager.TrySystem<Content.Server._CorvaxGoob.Ghost.GhostGoLobbySystem>(out var lobby))
            lobby.MarkGhostAttached(uid);

        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;

        if (!await ApplyOwnedTheme(uid, session.UserId))
            TrySetCkeySprite(uid, GetCkey(session));
    }

    private static string GetCkey(ICommonSession session)
    {
        var playerName = session.Name;

        if (playerName.StartsWith("localhost@"))
            return playerName["localhost@".Length..];

        return playerName;
    }

    private void TrySetCkeySprite(EntityUid ghostUid, string ckey)
    {
        foreach (var proto in _prototypeManager.EnumeratePrototypes<CustomGhostPrototype>())
        {
            if (string.IsNullOrEmpty(proto.Ckey)
                || !string.Equals(proto.Ckey, ckey, StringComparison.CurrentCultureIgnoreCase))
                continue;

            ApplyTheme(ghostUid, proto.ID);
            return;
        }
    }

    public async Task<bool> ApplyOwnedTheme(EntityUid ghostUid, NetUserId userId)
    {
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        // Prefer an explicit ":selected" token. Do not TryIndex the raw suffix
        // "ThemeId:selected" - that never matches a prototype and skips the pick.
        string? selectedThemeId = null;
        string? fallbackThemeId = null;

        foreach (var token in tokens)
        {
            if (!token.TokenId.StartsWith("ghost-theme:") || token.Amount <= 0)
                continue;

            var themeId = token.TokenId["ghost-theme:".Length..];

            if (themeId.EndsWith(":selected"))
            {
                var selectedId = themeId[..^":selected".Length];
                if (_prototypeManager.TryIndex<CustomGhostPrototype>(selectedId, out _))
                    selectedThemeId = selectedId;
                continue;
            }

            if (fallbackThemeId == null && _prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out _))
                fallbackThemeId = themeId;
        }

        var themeToApply = selectedThemeId ?? fallbackThemeId;
        if (themeToApply == null)
            return false;

        ApplyTheme(ghostUid, themeToApply);
        return true;
    }

    public void ApplyTheme(EntityUid ghostUid, string themeId)
    {
        if (!_prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out var proto))
            return;

        _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.Sprite, proto.CustomSpritePath.ToString());
        _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.SizeOverride, proto.SizeOverride);
        _appearanceSystem.SetData(ghostUid,
            CustomGhostAppearance.AlphaOverride,
            proto.AlphaOverride > 0 ? proto.AlphaOverride : 1f);

        // Do not overwrite MetaData name/description - GhostSystem already set the
        // player's character/ckey name. Theme display names belong in the shop UI only.

        EntityManager.AddComponents(ghostUid, proto.Components);
        _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.YAMLKOSTIL, themeId);
    }

    private async void OnShopOpen(GhostShopOpenRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var (balance, ownedThemes, selectedTheme, colors) = await GetPlayerShopData(userId);
        SendShopState(args.SenderSession, balance, ownedThemes, selectedTheme);
    }

    private async void OnShopBuy(GhostShopBuyRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_prototypeManager.TryIndex<CustomGhostPrototype>(msg.ThemeId, out var proto)
            || !string.IsNullOrEmpty(proto.Ckey))
        {
            SendShopState(args.SenderSession);
            return;
        }

        // Темы за наигранное время не списывают монеты - вместо цены проверяется трекер Halloween.
        if (proto.PlaytimeHours > 0)
        {
            BuyWithPlaytime(args.SenderSession, proto);
            return;
        }

        var tokenId = $"ghost-theme:{msg.ThemeId}";
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        if (tokens.Any(t => t.TokenId == tokenId && t.Amount > 0))
        {
            SendShopState(args.SenderSession);
            return;
        }

        if (!_antagTokens.TrySpendBalance(userId, proto.Price, out _))
        {
            SendShopState(args.SenderSession);
            return;
        }

        await _db.SetPlayerAntagTokenAmount(userId.UserId, tokenId, 1);

        var ownedThemes = new List<string>();
        foreach (var t in tokens)
        {
            if (t.TokenId.StartsWith("ghost-theme:") && !t.TokenId.EndsWith(":selected"))
                ownedThemes.Add(t.TokenId["ghost-theme:".Length..]);
        }
        ownedThemes.Add(msg.ThemeId);

        var ownedTokens = await _db.GetPlayerAntagTokens(userId.UserId);
        var selectedToken = ownedTokens.FirstOrDefault(t => t.TokenId.EndsWith(":selected"));
        var selectedTheme = selectedToken?.TokenId["ghost-theme:".Length..].Replace(":selected", "");

        SendShopState(args.SenderSession, _antagTokens.GetBalance(userId), ownedThemes, selectedTheme);
    }

    /// <summary>
    /// Покупка темы за наигранное время: монеты не тратятся, нужно набрать достаточно
    /// часов ПОСЛЕ хэллоуин-апдейта (тот же счётчик, что у достижения за 10 часов).
    /// Недобор не показывает ошибок - в карточке темы уже тикает таймер остатка.
    /// </summary>
    private async void BuyWithPlaytime(ICommonSession session, CustomGhostPrototype proto)
    {
        var userId = session.UserId;
        var owned = false;
        var halloween = _halloween.GetHalloweenPlaytime(session);

        if (halloween >= TimeSpan.FromHours(proto.PlaytimeHours))
        {
            var tokenId = $"{ThemeTokenPrefix}{proto.ID}";
            var tokens = await _db.GetPlayerAntagTokens(userId.UserId);
            owned = tokens.Any(t => t.TokenId == tokenId && t.Amount > 0);

            if (!owned)
                await _db.SetPlayerAntagTokenAmount(userId.UserId, tokenId, 1);
        }

        var (balance, ownedThemes, selectedTheme, _) = await GetPlayerShopData(userId);
        SendShopState(session, balance, ownedThemes, selectedTheme);
    }

    /// <summary>
    /// Выдаёт тему как ownership-токен (покупка за время или награда достижения).
    /// Повторный вызов для уже выданной темы ничего не меняет.
    /// </summary>
    public async Task<bool> TryGrantTheme(NetUserId userId, string themeId)
    {
        if (!_prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out _))
            return false;

        var tokenId = $"{ThemeTokenPrefix}{themeId}";
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        if (tokens.Any(t => t.TokenId == tokenId && t.Amount > 0))
            return true;

        await _db.SetPlayerAntagTokenAmount(userId.UserId, tokenId, 1);
        return true;
    }

    private async void OnShopBuyColor(GhostShopBuyColorRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        if (!_prototypeManager.TryIndex<CoinOocColorPrototype>(msg.ColorId, out var proto) ||
            !_antagTokens.TrySpendBalance(userId, proto.Price, out _))
        {
            SendShopState(args.SenderSession);
            return;
        }

        // Renewal extends from the current expiry; a first purchase (or an expired one) starts fresh.
        var newExpire = (CoinOocColorCache.TryGetExpire(userId, out var currentExpire) ? currentExpire : CoinOocColorCache.Today) + 30;

        // Owned colors are never removed - buying another color keeps the previous ones selectable.
        await _db.SetPlayerAntagTokenAmount(userId.UserId, "ooc-color:" + msg.ColorId, 1);
        await _db.SetPlayerAntagTokenAmount(userId.UserId, "ooc-color-expire", newExpire);
        await SelectColor(userId, msg.ColorId, newExpire);

        SendShopState(args.SenderSession);
    }

    private async void OnShopSelectColor(GhostShopSelectColorRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        // Free switching between colors of an active subscription.
        if (!tokens.Any(tk => tk.Amount > 0 && tk.TokenId == "ooc-color:" + msg.ColorId) ||
            !CoinOocColorCache.TryGetExpire(userId, out var expire))
        {
            SendShopState(args.SenderSession);
            return;
        }

        await SelectColor(userId, msg.ColorId, expire);
        SendShopState(args.SenderSession);
    }

    private async Task SelectColor(NetUserId userId, string colorId, int expireDay)
    {
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        foreach (var token in tokens)
        {
            if (token.Amount > 0 && token.TokenId.StartsWith("ooc-color:") && token.TokenId.EndsWith(":selected"))
                await _db.SetPlayerAntagTokenAmount(userId.UserId, token.TokenId, 0);
        }

        await _db.SetPlayerAntagTokenAmount(userId.UserId, "ooc-color:" + colorId + ":selected", 1);

        if (_prototypeManager.TryIndex<CoinOocColorPrototype>(colorId, out var proto))
            CoinOocColorCache.Set(userId, proto.Color, expireDay);
    }

    private async void OnShopSelect(GhostShopSelectRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var themeId = msg.ThemeId == "GhostThemeDefault" ? null : msg.ThemeId;
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        // Без этой проверки клиент мог выбрать любую тему, не покупая её, - вместе со
        // спрайтом применились бы и компоненты темы вроде способности тыквенного призрака.
        if (themeId != null)
        {
            if (!_prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out var selProto)
                || !string.IsNullOrEmpty(selProto.Ckey)
                || !tokens.Any(t => t.TokenId == $"{ThemeTokenPrefix}{themeId}" && t.Amount > 0))
            {
                SendShopState(args.SenderSession);
                return;
            }
        }

        string? oldSelectedThemeId = null;

        foreach (var token in tokens)
        {
            if (token.Amount > 0
                && token.TokenId.StartsWith("ghost-theme:")
                && token.TokenId.EndsWith(":selected"))
            {
                oldSelectedThemeId = token.TokenId;
                break;
            }
        }

        if (oldSelectedThemeId != null && themeId == null)
        {
            await _db.SetPlayerAntagTokenAmount(userId.UserId, oldSelectedThemeId, 0);
        }
        else if (themeId != null)
        {
            if (oldSelectedThemeId != null)
                await _db.SetPlayerAntagTokenAmount(userId.UserId, oldSelectedThemeId, 0);

            var selectTokenId = $"ghost-theme:{themeId}:selected";
            await _db.SetPlayerAntagTokenAmount(userId.UserId, selectTokenId, 1);
        }

        tokens = await _db.GetPlayerAntagTokens(userId.UserId);
        var ownedThemes = new List<string>();
        string? selectedTheme = null;

        foreach (var token in tokens)
        {
            if (!token.TokenId.StartsWith("ghost-theme:") || token.Amount <= 0)
                continue;

            var tId = token.TokenId["ghost-theme:".Length..];

            if (tId.EndsWith(":selected"))
                selectedTheme = tId[..^":selected".Length];
            else
                ownedThemes.Add(tId);
        }

        SendShopState(args.SenderSession, _antagTokens.GetBalance(userId), ownedThemes, selectedTheme);

        if (args.SenderSession.AttachedEntity is { Valid: true } ent && HasComp<GhostComponent>(ent))
            ApplyTheme(ent, themeId ?? "GhostThemeDefault");
    }

    private async void SendShopState(ICommonSession session)
    {
        var (balance, ownedThemes, selectedTheme, colors) = await GetPlayerShopData(session.UserId);
        SendShopState(session, balance, ownedThemes, selectedTheme);
    }

    private async void SendShopState(ICommonSession session, int balance, List<string>? ownedThemes = null, string? selectedTheme = null, string? error = null)
    {
        var colors = (await GetPlayerShopData(session.UserId)).colors;
        var playtimeSeconds = _halloween.GetHalloweenPlaytime(session).TotalSeconds;

        var themes = _prototypeManager.EnumeratePrototypes<CustomGhostPrototype>()
            .Where(p => p.Price >= 0 && string.IsNullOrEmpty(p.Ckey))
            .OrderBy(p => p.Order)
            .Select(proto =>
            {
                // Тема за playtime имеет price 0, но не является "всегда доступной" по умолчанию.
                var isDefault = proto.Price == 0 && proto.PlaytimeHours <= 0;
                var owned = isDefault || (ownedThemes?.Contains(proto.ID) ?? false);
                var selected = isDefault
                    ? selectedTheme == null
                    : selectedTheme == proto.ID;
                return new GhostThemeEntry(
                    proto.ID,
                    proto.Name,
                    proto.Description,
                    proto.Price,
                    proto.PlaytimeHours,
                    owned,
                    selected,
                    proto.CustomSpritePath.ToString(),
                    "animated"
                );
            })
            .ToList();

        RaiseNetworkEvent(new GhostShopStateEvent(balance, themes, colors ?? new List<GhostColorEntry>(), error, playtimeSeconds), session);
    }

    private async Task<(int balance, List<string> ownedThemes, string? selectedTheme, List<GhostColorEntry> colors)> GetPlayerShopData(NetUserId userId)
    {
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        var balance = _antagTokens.GetBalance(userId);
        var ownedThemes = new List<string>();
        string? selectedTheme = null;
        var colors = BuildColorEntries(tokens, userId);

        foreach (var token in tokens)
        {
            if (token.TokenId.StartsWith("ghost-theme:") && token.Amount > 0)
            {
                var themeId = token.TokenId["ghost-theme:".Length..];

                if (themeId.EndsWith(":selected"))
                {
                    selectedTheme = themeId[..^":selected".Length];
                }
                else
                {
                    ownedThemes.Add(themeId);
                }
            }
        }

        return (balance, ownedThemes, selectedTheme, colors);
    }

    private List<GhostColorEntry> BuildColorEntries(List<PlayerAntagToken> tokens, NetUserId userId)
    {
        var ownedColors = new List<string>();
        string? colorId = null;
        var expireDay = 0;

        foreach (var token in tokens)
        {
            if (token.Amount <= 0)
                continue;

            if (token.TokenId.StartsWith("ooc-color:"))
            {
                var key = token.TokenId["ooc-color:".Length..];
                if (key.EndsWith(":selected"))
                    colorId = key[..^":selected".Length];
                else
                    ownedColors.Add(key);
            }
            else if (token.TokenId == "ooc-color-expire")
            {
                expireDay = token.Amount;
            }
        }

        // Legacy rows: colors bought before the ":selected" key existed have none.
        colorId ??= ownedColors.FirstOrDefault();

        var active = colorId != null && expireDay > CoinOocColorCache.Today &&
                     _prototypeManager.TryIndex<CoinOocColorPrototype>(colorId, out _);
        var daysLeft = active ? Math.Max(0, expireDay - CoinOocColorCache.Today) : 0;

        return _prototypeManager.EnumeratePrototypes<CoinOocColorPrototype>()
            .OrderBy(p => p.Order)
            .Select(p => new GhostColorEntry(
                p.ID,
                p.Name,
                p.Color,
                p.Price,
                active && ownedColors.Contains(p.ID),
                active && colorId == p.ID,
                active && ownedColors.Contains(p.ID) ? daysLeft : 0))
            .ToList();
    }
}
