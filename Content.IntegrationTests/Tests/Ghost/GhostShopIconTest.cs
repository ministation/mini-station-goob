// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Client._Mini.CoinShop;
using Content.Client._Mini.CustomGhost;
using Content.Shared._Mini.CustomGhost;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ghost;

/// <summary>
/// The ghost shop card must show a single ghost sprite, not the whole theme sprite sheet with all
/// directions and animation frames.
/// </summary>
[TestFixture]
public sealed class GhostShopIconTest
{
    [Test]
    public async Task ThemeIconsUseASingleFrame()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var protoMan = pair.Client.ResolveDependency<IPrototypeManager>();
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
                Assert.Multiple(() =>
                {
                    Assert.That(icon!.Width, Is.EqualTo(32),
                        $"Theme '{theme.ID}' icon is {icon.Width}x{icon.Height}, the raw sheet instead of one frame");
                    Assert.That(icon.Height, Is.EqualTo(32),
                        $"Theme '{theme.ID}' icon is {icon.Width}x{icon.Height}, the raw sheet instead of one frame");
                });

                var coinIcon = coinShop.GetGhostIcon(path);
                Assert.That(coinIcon, Is.Not.Null, $"No coin shop icon for theme '{theme.ID}' ({path})");
                Assert.That(coinIcon!.Width, Is.EqualTo(32),
                    $"Coin shop theme '{theme.ID}' icon is {coinIcon.Width}x{coinIcon.Height}");
            }
        });

        await pair.CleanReturnAsync();
    }
}
