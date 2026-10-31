// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

using Content.Server.GameTicking.Presets;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// The fishing mode rule and its game preset must exist and the preset must actually
/// start the fishing rule, otherwise the mode boots as a plain round.
/// </summary>
[TestFixture]
public sealed class FishingModePrototypeTest
{
    [Test]
    public async Task RuleAndPresetLoad()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true });

        await pair.Server.WaitAssertion(() =>
        {
            var proto = pair.Server.ResolveDependency<IPrototypeManager>();

            Assert.Multiple(() =>
            {
                Assert.That(proto.HasIndex<EntityPrototype>("FishingModeRule"), Is.True,
                    "Fishing rule entity prototype is missing");
                Assert.That(proto.HasIndex<GamePresetPrototype>("FishingMode"), Is.True,
                    "Fishing game preset is missing");
            });

            var preset = proto.Index<GamePresetPrototype>("FishingMode");
            Assert.That(preset.Rules.Any(rule => rule.Id == "FishingModeRule"), Is.True,
                "The fishing preset does not start the fishing rule");
        });

        await pair.CleanReturnAsync();
    }
}
