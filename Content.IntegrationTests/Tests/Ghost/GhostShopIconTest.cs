// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Client._Mini.CoinShop;
using Content.Client._Mini.CustomGhost;
using Content.Client.Resources;
using Content.Shared._Mini.CustomGhost;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ghost;

/// <summary>
/// The ghost shop card must show a single ghost sprite, not the whole theme sprite sheet with all
/// directions and animation frames. The frame must match the theme RSI's declared size, so themes
/// are free to use any sprite resolution (the pumpkin ghost is 48x48, the rest are 32x32).
/// </summary>
[TestFixture]
public sealed class GhostShopIconTest
{
    [Test]
    public async Task ThemeIconsUseASingleFrame()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var protoMan = pair.Client.ResolveDependency<IPrototypeManager>();
        var resourceCache = pair.Client.ResolveDependency<IResourceCache>();
        var ghostShop = pair.Client.System<GhostShopSystem>();
        var coinShop = pair.Client.System<CoinShopSystem>();

        await pair.Client.WaitAssertion(() =>
        {
            var themes = protoMan.EnumeratePrototypes<CustomGhostPrototype>().ToList();
            Assert.That(themes, Is.Not.Empty, "No custom ghost themes found");

            foreach (var theme in themes)
            {
                var path = theme.CustomSpritePath.ToString();
                var icon = ghostShop.GetIconTexture(path, "animated");
                Assert.That(icon, Is.Not.Null, $"No icon for theme '{theme.ID}' ({path})");

                var rsi = resourceCache.GetResource<RSIResource>(
                    new ResPath(GhostShopSystem.ToTexturePath(path))).RSI;

                Assert.Multiple(() =>
                {
                    Assert.That(icon!.Width, Is.EqualTo(rsi.Size.X),
                        $"Theme '{theme.ID}' icon is {icon.Width}x{icon.Height}, expected a single {rsi.Size.X}x{rsi.Size.Y} frame instead of the raw sheet");
                    Assert.That(icon!.Height, Is.EqualTo(rsi.Size.Y),
                        $"Theme '{theme.ID}' icon is {icon.Width}x{icon.Height}, expected a single {rsi.Size.X}x{rsi.Size.Y} frame instead of the raw sheet");
                });

                var coinIcon = coinShop.GetGhostIcon(path);
                Assert.That(coinIcon, Is.Not.Null, $"No coin shop icon for theme '{theme.ID}' ({path})");
                Assert.That(coinIcon!.Width, Is.EqualTo(rsi.Size.X),
                    $"Coin shop theme '{theme.ID}' icon is {coinIcon.Width}x{coinIcon.Height}");
            }
        });

        await pair.CleanReturnAsync();
    }
}
