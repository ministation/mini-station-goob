// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Goobstation.Client.Achievements.UI;
using Content.Goobstation.Shared.Achievements;
using Content.Shared._Mini.DailyQuests;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Guards the reward badge on the achievement cards: every card of a visible achievement must show
/// how many coins it is worth, the badge sits on the right edge of the card, stays gold while the
/// reward is pending and turns green once the achievement is unlocked.
/// </summary>
[TestFixture]
public sealed class AchievementsPanelTest
{
    [Test]
    public async Task RewardBadgeIsGoldWhileLockedAndGreenWhenUnlocked()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var client = pair.Client;
        await client.WaitIdleAsync();

        var entries = new List<AchievementEntry>();
        await client.WaitPost(() =>
        {
            var panel = new AchievementsPanel();

            var data = new List<AchievementData>
            {
                // Unlocked: three coins already on the balance.
                new("test-unlocked",
                    "achievement-welcome-aboard-name",
                    "achievement-welcome-aboard-desc",
                    null,
                    "General",
                    1,
                    1,
                    DateTime.UtcNow,
                    1f,
                    DailyQuestRarity.Epic,
                    3),
                // Locked: the reward is still pending.
                new("test-locked",
                    "achievement-welcome-aboard-name",
                    "achievement-welcome-aboard-desc",
                    null,
                    "General",
                    0,
                    1,
                    null,
                    1f,
                    DailyQuestRarity.Rare,
                    1),
                // Hidden and locked: the reward must stay secret.
                new("test-hidden",
                    null,
                    null,
                    null,
                    "General",
                    0,
                    1,
                    null,
                    1f,
                    DailyQuestRarity.Legendary,
                    5),
            };

            panel.UpdateAchievements(data);
            CollectEntries(panel, entries);
        });

        Assert.That(entries, Has.Count.EqualTo(3), "Every achievement must get a card");

        var unlocked = entries.Single(e => FindCoinLabel(e)?.Text == "+3");
        var locked = entries.Single(e => FindCoinLabel(e)?.Text == "+1");
        var hidden = entries[2];

        var unlockedBadge = FindBadge(unlocked);
        var lockedBadge = FindBadge(locked);
        Assert.That(unlockedBadge, Is.Not.Null, "Unlocked achievement has no coin badge");
        Assert.That(lockedBadge, Is.Not.Null, "Locked achievement has no coin badge");
        Assert.That(FindCoinLabel(hidden), Is.Null, "Hidden achievement leaks its coin reward");

        // The badge must be the right-most element of the card.
        var unlockedRow = (BoxContainer) unlocked.Children.First();
        Assert.That(unlockedRow.Children.ToList().IndexOf(unlockedBadge!), Is.EqualTo(unlockedRow.ChildCount - 1),
            "The coin badge must sit on the right edge of the card");

        // Pending reward: gold (red > green > blue). Received reward: green (green dominant).
        var lockedStyle = (StyleBoxFlat) lockedBadge!.PanelOverride!;
        Assert.That(lockedStyle.BackgroundColor.R, Is.GreaterThan(lockedStyle.BackgroundColor.G),
            "Pending coin badge must stay gold");
        Assert.That(lockedStyle.BackgroundColor.G, Is.GreaterThan(lockedStyle.BackgroundColor.B));

        var unlockedStyle = (StyleBoxFlat) unlockedBadge!.PanelOverride!;
        Assert.That(unlockedStyle.BackgroundColor.G, Is.GreaterThan(unlockedStyle.BackgroundColor.R),
            "Received coin badge must turn green");
        Assert.That(unlockedStyle.BackgroundColor.G, Is.GreaterThan(unlockedStyle.BackgroundColor.B));

        await pair.CleanReturnAsync();
    }

    private static Label? FindCoinLabel(Control card)
    {
        return Descendants<Label>(card).FirstOrDefault(l => l.Text != null && l.Text.StartsWith('+'));
    }

    private static PanelContainer? FindBadge(Control card)
    {
        var label = FindCoinLabel(card);
        if (label == null)
            return null;

        for (var c = label.Parent; c != null; c = c.Parent)
        {
            if (c is PanelContainer { PanelOverride: StyleBoxFlat } panel && panel.Name == "CoinBadge")
                return panel;
        }

        return null;
    }

    private static IEnumerable<T> Descendants<T>(Control control) where T : Control
    {
        foreach (var child in control.Children)
        {
            if (child is T typed)
                yield return typed;

            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }

    private static void CollectEntries(Control control, List<AchievementEntry> entries)
    {
        if (control is AchievementEntry entry)
            entries.Add(entry);

        foreach (var child in control.Children)
            CollectEntries(child, entries);
    }
}
