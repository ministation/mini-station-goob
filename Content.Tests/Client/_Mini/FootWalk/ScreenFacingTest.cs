// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Mini.FootWalk;
using NUnit.Framework;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;

namespace Content.Tests.Client._Mini.FootWalk;

/// <summary>
/// Регрессия: походка считала направление через GetCardinalDir, а спрайт рисуется с bias.
/// На диагоналях ровно 135° и 315° (W+D и S+A) кадр показывается E/W, а походка думала N/S,
/// и одежда нарезалась из кадра другого направления: голова смотрела в одну сторону,
/// скафандр поворачивался в другую.
/// </summary>
[TestFixture]
public sealed class ScreenFacingTest
{
    /// <summary>Эти углы выставляет движок при диагональном вводе.</summary>
    [Test]
    public void DiagonalsMatchRenderedSprite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(45)), Is.EqualTo(RsiDirection.East));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(135)), Is.EqualTo(RsiDirection.East));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(225)), Is.EqualTo(RsiDirection.West));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(315)), Is.EqualTo(RsiDirection.West));
        });
    }

    [Test]
    public void CardinalsMatchRenderedSprite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(0)), Is.EqualTo(RsiDirection.South));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(90)), Is.EqualTo(RsiDirection.East));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(180)), Is.EqualTo(RsiDirection.North));
            Assert.That(FootWalkAnimationSystem.ScreenFacing(Angle.FromDegrees(270)), Is.EqualTo(RsiDirection.West));
        });
    }
}
