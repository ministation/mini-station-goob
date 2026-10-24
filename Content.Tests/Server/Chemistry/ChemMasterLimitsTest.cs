// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry;
using NUnit.Framework;

namespace Content.Tests.Server.Chemistry;

[TestFixture, TestOf(typeof(SharedChemMaster))]
public sealed class ChemMasterLimitsTest
{
    private const uint DosageLimit = 20;

    [Test]
    public void RejectsHugePillNumber()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedChemMaster.TryGetPillCreationVolume(
                1, uint.MaxValue, DosageLimit, int.MaxValue, out var needed), Is.False);
            Assert.That(needed, Is.Zero);
        });
    }

    [Test]
    public void RejectsPillNumberAboveAbsoluteLimit()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            1, SharedChemMaster.PillMaxCount + 1, DosageLimit, int.MaxValue, out _), Is.False);
    }

    [Test]
    public void AcceptsPillNumberAtAbsoluteLimit()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            1, SharedChemMaster.PillMaxCount, DosageLimit, (int) SharedChemMaster.PillMaxCount, out var needed), Is.True);
        Assert.That(needed, Is.EqualTo(SharedChemMaster.PillMaxCount));
    }

    [Test]
    public void RejectsZeroPillNumber()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            1, 0, DosageLimit, 1, out _), Is.False);
    }

    [Test]
    public void RejectsDosageAboveLimit()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            DosageLimit + 1, 1, DosageLimit, 1, out _), Is.False);
    }

    [Test]
    public void RejectsZeroDosage()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            0, 1, DosageLimit, 1, out _), Is.False);
    }

    [Test]
    public void RejectsMorePillsThanFit()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            1, 5, DosageLimit, 4, out _), Is.False);
    }

    [Test]
    public void RejectsMultiplicationThatWouldOverflowUint()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedChemMaster.TryGetPillCreationVolume(
                uint.MaxValue, 2, uint.MaxValue, 2, out var needed), Is.False);
            Assert.That(needed, Is.Zero);
        });
    }

    [Test]
    public void RejectsTotalThatWouldOverflowFixedPoint()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedChemMaster.TryGetPillCreationVolume(
                uint.MaxValue, 1, uint.MaxValue, 1, out var needed), Is.False);
            Assert.That(needed, Is.Zero);
        });
    }

    [Test]
    public void ComputesTotalWithoutOverflow()
    {
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            4, 5, DosageLimit, 5, out var needed), Is.True);
        Assert.That(needed, Is.EqualTo(20));
    }

    [Test]
    public void AcceptsMaxPillsAtDosageLimit()
    {
        // A full canister of maximum-dose pills must not be rejected.
        Assert.That(SharedChemMaster.TryGetPillCreationVolume(
            DosageLimit, SharedChemMaster.PillMaxCount, DosageLimit,
            (int) SharedChemMaster.PillMaxCount, out var needed), Is.True);
        Assert.That(needed, Is.EqualTo(DosageLimit * SharedChemMaster.PillMaxCount));
    }
}
