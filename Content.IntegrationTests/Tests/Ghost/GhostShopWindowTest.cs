// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Numerics;
using Content.Client._Mini.CustomGhost;
using Content.Shared._Mini.CustomGhost;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests.Ghost;

/// <summary>
/// Регрессия на "меню магазина призраков на весь экран": темы и цвет ника теперь в отдельных
/// вкладках, а окно компактное, карточки его не растягивают.
/// </summary>
[TestFixture]
public sealed class GhostShopWindowTest
{
    [Test]
    public async Task WindowHasTabsAndIsCompact()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var shop = pair.Client.System<GhostShopSystem>();

        GhostShopWindow window = default!;
        TabContainer? tabs = null;
        var titles = new List<string?>();
        var tabCount = -1;
        Vector2 minSize = default;
        var themesTabTitle = string.Empty;
        var colorsTabTitle = string.Empty;

        await pair.Client.WaitAssertion(() =>
        {
            window = new GhostShopWindow(shop);

            var state = new GhostShopStateEvent(
                100,
                new List<GhostThemeEntry>
                {
                    // Обычная тема за монеты и тема за наигранное время: вторая строит таймер с часами.
                    new("GhostThemeDefault", "Обычный", "", 10, 0, false, false, "", ""),
                    new("GhostThemePumpkin", "Тыквенный", "", 0, 10, false, false, "", ""),
                },
                new List<GhostColorEntry>
                {
                    new("GhostColorDefault", "Цвет", "#ffffff", 10, false, false, 0),
                },
                null,
                3600);

            window.UpdateState(state);

            tabs = FindTabs(window.Contents);
            Assert.That(tabs, Is.Not.Null, "в магазине призраков нет вкладок");

            tabCount = tabs!.ChildCount;
            foreach (var child in tabs.Children)
                titles.Add(TabContainer.GetTabTitle(child));

            themesTabTitle = Loc.GetString("ghost-shop-window-subtitle");
            colorsTabTitle = Loc.GetString("ghost-shop-color-header");
            minSize = window.MinSize;
            window.Dispose();
        });

        Assert.Multiple(() =>
        {
            Assert.That(tabCount, Is.EqualTo(2), "должно быть две вкладки: темы и цвет ника");
            Assert.That(titles, Is.EquivalentTo(new[] { themesTabTitle, colorsTabTitle }),
                "названия вкладок не совпадают");
            Assert.That(minSize.X, Is.LessThan(900), "окно магазина снова на весь экран по ширине");
            Assert.That(minSize.Y, Is.LessThan(700), "окно магазина снова на весь экран по высоте");
        });
    }

    private static TabContainer? FindTabs(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is TabContainer tabs)
                return tabs;

            if (FindTabs(child) is { } nested)
                return nested;
        }

        return null;
    }
}
