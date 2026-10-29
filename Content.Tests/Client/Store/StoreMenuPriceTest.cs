// SPDX-License-Identifier: MIT

using Content.Client.Store.Ui;
using Content.Goobstation.Maths.FixedPoint;
using NUnit.Framework;

namespace Content.Tests.Client.Store;

[TestFixture, TestOf(typeof(StoreMenu)), Parallelizable]
public sealed class StoreMenuPriceTest
{
    // A currency without a cash icon used to show "125 Украденная эссенция" in the 56px buy button
    // and spill over the card. Now the button shows the amount only, with the full price as tooltip.
    [Test]
    public void BuyButtonTextIsAmountOnly()
    {
        var (text, toolTip) = StoreMenu.FormatListingPrice(FixedPoint2.New(125), "125 Украденная эссенция");

        Assert.That(text, Is.EqualTo("125"));
        Assert.That(text, Does.Not.Contain("эссенция"));
        Assert.That(toolTip, Is.EqualTo("125 Украденная эссенция"));
    }
}
