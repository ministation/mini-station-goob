using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Ghost;
using Content.Shared._Mini.CustomGhost;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.CustomGhost;

public sealed class CustomGhostSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<GhostShopOpenRequestEvent>(OnShopOpen);
        SubscribeNetworkEvent<GhostShopBuyRequestEvent>(OnShopBuy);
        SubscribeNetworkEvent<GhostShopSelectRequestEvent>(OnShopSelect);
    }

    private async void OnPlayerAttached(EntityUid uid, GhostComponent component, PlayerAttachedEvent args)
    {
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

    private async Task<bool> ApplyOwnedTheme(EntityUid ghostUid, NetUserId userId)
    {
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        string? selectedThemeId = null;
        foreach (var token in tokens)
        {
            if (token.TokenId.StartsWith("ghost-theme:") && token.Amount > 0)
            {
                var themeId = token.TokenId["ghost-theme:".Length..];
                if (!_prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out _))
                    continue;

                if (token.TokenId.EndsWith(":selected"))
                {
                    selectedThemeId = themeId;
                    break;
                }

                selectedThemeId ??= themeId;
            }
        }

        if (selectedThemeId == null)
            return false;

        ApplyTheme(ghostUid, selectedThemeId);
        return true;
    }

    private void ApplyTheme(EntityUid ghostUid, string themeId)
    {
        if (!_prototypeManager.TryIndex<CustomGhostPrototype>(themeId, out var proto))
            return;

        _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.Sprite, proto.CustomSpritePath.ToString());

        if (proto.SizeOverride != Vector2.One)
            _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.SizeOverride, proto.SizeOverride);

        if (proto.AlphaOverride > 0)
            _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.AlphaOverride, proto.AlphaOverride);

        if (proto.GhostName != string.Empty)
            _metaData.SetEntityName(ghostUid, proto.GhostName);

        if (proto.GhostDescription != string.Empty)
            _metaData.SetEntityDescription(ghostUid, proto.GhostDescription);

        EntityManager.AddComponents(ghostUid, proto.Components);
        _appearanceSystem.SetData(ghostUid, CustomGhostAppearance.YAMLKOSTIL, themeId);
    }

    private async void OnShopOpen(GhostShopOpenRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var (balance, ownedThemes, selectedTheme) = await GetPlayerShopData(userId);
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

        var tokenId = $"ghost-theme:{msg.ThemeId}";
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        if (tokens.Any(t => t.TokenId == tokenId && t.Amount > 0))
        {
            SendShopState(args.SenderSession);
            return;
        }

        var balanceToken = tokens.FirstOrDefault(t => t.TokenId == "balance");
        var balance = balanceToken?.Amount ?? 0;

        if (balance < proto.Price)
        {
            SendShopState(args.SenderSession);
            return;
        }

        var newBalance = balance - proto.Price;
        await _db.SetPlayerAntagTokenAmount(userId.UserId, "balance", newBalance);
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

        SendShopState(args.SenderSession, newBalance, ownedThemes, selectedTheme);
    }

    private async void OnShopSelect(GhostShopSelectRequestEvent msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;
        var themeId = msg.ThemeId == "GhostThemeDefault" ? null : msg.ThemeId;
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);
        string? oldSelectedThemeId = null;

        foreach (var token in tokens)
        {
            if (token.TokenId.StartsWith("ghost-theme:") && token.TokenId.EndsWith(":selected"))
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
        var balanceToken = tokens.FirstOrDefault(t => t.TokenId == "balance");

        foreach (var token in tokens)
        {
            if (token.TokenId.StartsWith("ghost-theme:"))
            {
                var tId = token.TokenId["ghost-theme:".Length..];

                if (tId.EndsWith(":selected"))
                {
                    selectedTheme = tId.Replace(":selected", "");
                }
                else
                {
                    ownedThemes.Add(tId);
                }
            }
        }

        SendShopState(args.SenderSession, balanceToken?.Amount ?? 0, ownedThemes, selectedTheme);

        if (themeId != null && args.SenderSession.AttachedEntity is { Valid: true } ent && HasComp<GhostComponent>(ent))
            ApplyTheme(ent, themeId);
    }

    private async void SendShopState(ICommonSession session)
    {
        var (balance, ownedThemes, selectedTheme) = await GetPlayerShopData(session.UserId);
        SendShopState(session, balance, ownedThemes, selectedTheme);
    }

    private void SendShopState(ICommonSession session, int balance, List<string>? ownedThemes = null, string? selectedTheme = null)
    {
        var themes = _prototypeManager.EnumeratePrototypes<CustomGhostPrototype>()
            .Where(p => p.Price >= 0 && string.IsNullOrEmpty(p.Ckey))
            .OrderBy(p => p.Order)
            .Select(proto =>
            {
                var owned = proto.Price == 0 || (ownedThemes?.Contains(proto.ID) ?? false);
                var selected = proto.Price == 0
                    ? selectedTheme == null
                    : selectedTheme == proto.ID;
                return new GhostThemeEntry(
                    proto.ID,
                    proto.Name,
                    proto.Description,
                    proto.Price,
                    owned,
                    selected,
                    proto.CustomSpritePath.ToString(),
                    "animated"
                );
            })
            .ToList();

        RaiseNetworkEvent(new GhostShopStateEvent(balance, themes), session);
    }

    private async Task<(int balance, List<string> ownedThemes, string? selectedTheme)> GetPlayerShopData(NetUserId userId)
    {
        var tokens = await _db.GetPlayerAntagTokens(userId.UserId);

        var balance = 0;
        var ownedThemes = new List<string>();
        string? selectedTheme = null;

        foreach (var token in tokens)
        {
            if (token.TokenId == "balance")
            {
                balance = token.Amount;
            }
            else if (token.TokenId.StartsWith("ghost-theme:") && token.Amount > 0)
            {
                var themeId = token.TokenId["ghost-theme:".Length..];

                if (themeId.EndsWith(":selected"))
                {
                    selectedTheme = themeId.Replace(":selected", "");
                }
                else
                {
                    ownedThemes.Add(themeId);
                }
            }
        }

        return (balance, ownedThemes, selectedTheme);
    }
}
