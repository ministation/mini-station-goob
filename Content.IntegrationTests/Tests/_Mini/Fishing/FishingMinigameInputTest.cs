// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

using Content.Client._Mini.Fishing.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// The fishing minigame catcher must stay a small box on the fishing marker. When it was a
/// full screen control it swallowed every click, so the HUD and open windows stopped working
/// for the whole fight.
/// </summary>
[TestFixture]
public sealed class FishingMinigameInputTest
{
    [Test]
    public async Task CatcherCoversOnlyItsBox()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        await pair.Client.WaitPost(() =>
        {
            var parent = new LayoutContainer
            {
                MinSize = new Vector2(1000f, 1000f),
                MaxSize = new Vector2(1000f, 1000f),
            };
            var catcher = new FishingMinigameInputControl();
            parent.AddChild(catcher);

            var box = new UIBox2(0f, 0f, 1000f, 1000f);
            parent.Arrange(box);

            catcher.SetRect(new Vector2(120f, 260f), new Vector2(60f, 120f));

            // Real UI runs the queued measure before arrange; the catcher needs its new size measured first.
            catcher.Measure(new Vector2(1000f, 1000f));
            parent.Arrange(box);

            Assert.Multiple(() =>
            {
                Assert.That(catcher.Position, Is.EqualTo(new Vector2(120f, 260f)), "The catcher did not move to the marker");
                Assert.That(catcher.Size, Is.EqualTo(new Vector2(60f, 120f)), "The catcher did not shrink to the marker box");
                Assert.That(catcher.Size.X, Is.LessThan(parent.Size.X), "The catcher must not cover the whole screen");
                Assert.That(catcher.MouseFilter, Is.EqualTo(Control.MouseFilterMode.Stop), "The catcher must still receive the wheel");
            });
        });

        await pair.CleanReturnAsync();
    }
}
