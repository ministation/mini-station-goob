// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Stealth;

namespace Content.IntegrationTests.Tests.Stealth;

/// <summary>
/// Checks how far the client is allowed to draw a cloaked entity: the distortion effect that gives
/// the cloaked player feedback must not be visible to anyone else.
/// </summary>
[TestFixture]
public sealed class StealthRenderingTest
{
    [Test]
    public void FullyHiddenEntitiesAreInvisibleToOtherPlayers()
    {
        // At -1.5 the stealth shader has no distortion at all and blends into the background.
        Assert.Multiple(() =>
        {
            Assert.That(StealthSystem.GetRenderedVisibility(0f, false), Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(StealthSystem.GetRenderedVisibility(-0.8f, false), Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(StealthSystem.GetRenderedVisibility(-1.5f, false), Is.EqualTo(-1.5f).Within(0.001f));

            // The cloaked player keeps a readable glitch of their own position.
            Assert.That(StealthSystem.GetRenderedVisibility(-1.5f, true), Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(StealthSystem.GetRenderedVisibility(-0.2f, true), Is.EqualTo(-0.2f).Within(0.001f));

            // Partially visible stealth is unchanged for everyone.
            Assert.That(StealthSystem.GetRenderedVisibility(0.3f, false), Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(StealthSystem.GetRenderedVisibility(0.3f, true), Is.EqualTo(0.3f).Within(0.001f));
        });
    }
}
