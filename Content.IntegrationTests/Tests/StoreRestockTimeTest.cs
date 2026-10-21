// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Reflection;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests;

/// <summary>
/// Regression coverage for the NTR (ПНТ) store restock timer, see issue #187.
/// Restock timestamps are stored in station time (time since the round start) and a purchase
/// must add the configured cooldown exactly once.
/// </summary>
[TestFixture]
public sealed class StoreRestockTimeTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: listing
  id: TestRestockWithCooldown
  productEntity: Crowbar
  cost:
    NTLoyaltyPoint: 1
  categories:
  - NTRstation
  restockTime: 300
  resetRestockOnPurchase: true
  restockAfterPurchase: 1200

- type: listing
  id: TestRestockFallbackCooldown
  productEntity: Crowbar
  cost:
    NTLoyaltyPoint: 1
  categories:
  - NTRstation
  restockTime: 300
  resetRestockOnPurchase: true
";

    [Test]
    public async Task RestockTimerUsesStationTimeAndAddsCooldownOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var testMap = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        var entManager = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var ticker = server.System<GameTicker>();

        var roundStartProperty = typeof(SharedGameTicker).GetProperty(
            nameof(SharedGameTicker.RoundStartTimeSpan),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var savedRoundStart = ticker.RoundStartTimeSpan;

        EntityUid briefcase = default;
        EntityUid buyer = default;

        try
        {
            // Move the round start 30 minutes back: station time (CurTime - RoundStartTimeSpan) now
            // clearly differs from absolute CurTime, so both an absolute timestamp and a doubled
            // station offset show up in the assertions below.
            await server.WaitPost(() =>
            {
                roundStartProperty.SetValue(ticker, timing.CurTime - TimeSpan.FromMinutes(30));

                briefcase = entManager.SpawnEntity("ExecutiveBriefcaseDebug", testMap.GridCoords);
                buyer = entManager.SpawnEntity("HumanUniformDummy", testMap.GridCoords);
            });
            await server.WaitIdleAsync();

            // Buying in the game is a UI message handled by the store system.
            await server.WaitPost(() =>
            {
                server.EntMan.EventBus.RaiseLocalEvent(briefcase,
                    new StoreBuyListingMessage("TestRestockWithCooldown") { Actor = buyer });
                server.EntMan.EventBus.RaiseLocalEvent(briefcase,
                    new StoreBuyListingMessage("TestRestockFallbackCooldown") { Actor = buyer });
            });
            await server.WaitIdleAsync();

            await server.WaitAssertion(() =>
            {
                var store = entManager.GetComponent<StoreComponent>(briefcase);
                var stationTime = timing.CurTime.Subtract(ticker.RoundStartTimeSpan);

                var withCooldown = store.FullListingsCatalog.First(x => x.ID == "TestRestockWithCooldown");
                Assert.That(withCooldown.RestockAfterPurchase, Is.Not.Null,
                    "The restockAfterPurchase cooldown from YAML must be loaded");
                Assert.That(withCooldown.RestockAfterPurchase!.Value, Is.EqualTo(TimeSpan.FromMinutes(20)));
                AssertClose(withCooldown.RestockTime, stationTime + TimeSpan.FromMinutes(20),
                    "RestockTime must be a station time timestamp using restockAfterPurchase");

                var fallback = store.FullListingsCatalog.First(x => x.ID == "TestRestockFallbackCooldown");
                Assert.That(fallback.RestockAfterPurchase, Is.Null,
                    "This listing intentionally has no restockAfterPurchase");
                AssertClose(fallback.RestockTime, stationTime + TimeSpan.FromSeconds(300),
                    "Without restockAfterPurchase the station time must not be added twice");
            });
        }
        finally
        {
            roundStartProperty.SetValue(ticker, savedRoundStart);
        }

        await pair.CleanReturnAsync();
    }

    private const double ToleranceSeconds = 5;

    private static void AssertClose(TimeSpan actual, TimeSpan expected, string message)
    {
        Assert.That(Math.Abs((actual - expected).TotalSeconds), Is.LessThanOrEqualTo(ToleranceSeconds),
            $"{message}. Expected {expected}, got {actual}.");
    }
}
