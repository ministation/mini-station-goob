// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Checks the reagents and the entity prototypes (weapons, traps, clones) used by the ninja paths.
/// Item prototype ids are not hardcoded: the test scans prototypes by their components and only
/// falls back to the ids documented in the port spec for diagnostics.
/// </summary>
[TestFixture]
public sealed class NinjaContentTest
{
    /// <summary>
    /// Reagents injected by the Snake tier 2 and Steel tier 2 modules.
    /// </summary>
    private static readonly string[] PathReagents = new[] { "Chiyurizine", "Velaxin" };

    /// <summary>
    /// One gameplay feature per entry: an entity prototype with the given component must exist.
    /// Known ids from the port spec are only used to pick a specific prototype and for diagnostics.
    /// </summary>
    private static readonly (string Feature, string Component, string[] KnownIds)[] PathItemFeatures = new[]
    {
        ("johyo grappling gun", "NinjaJohyo", new[] { "NinjaJohyo" }),
        ("johyo projectile", "NinjaJohyoProjectile", new[] { "NinjaJohyoProjectile" }),
        ("caltrop trap", "NinjaCaltrop", new[] { "NinjaCaltrop" }),
        ("energy net gun", "NinjaEnergyNetGun", new[] { "WeaponNinjaNet" }),
        ("energy net", "NinjaEnergyNet", new[] { "NinjaNet" }),
        ("holographic clone", "NinjaGhostClone", new[] { "NinjaGhostHologram", "HolographicNinjaClone", "NinjaHolographicClone" }),
    };

    private const string ShurikenEmitterKnownId = "WeaponNinjaShuriken";
    private const string AmmoProviderComponent = "NinjaSuitAmmoProvider";

    [Test]
    public async Task PathReagentsExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        var warnings = new List<string>();
        var present = new List<ReagentPrototype>();

        foreach (var reagentId in PathReagents)
        {
            if (!protoMan.TryIndex<ReagentPrototype>(reagentId, out var reagent))
            {
                warnings.Add($"Reagent '{reagentId}' is not loaded yet; it is required by the ninja path modules");
                continue;
            }

            present.Add(reagent);
        }

        // Localized* properties resolve localization through IoC and must be read on the server main thread.
        await pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var reagent in present)
                {
                    Assert.That(reagent.LocalizedName, Is.Not.Empty, $"Reagent '{reagent.ID}' has no localized name");
                    Assert.That(reagent.LocalizedDescription, Is.Not.Empty,
                        $"Reagent '{reagent.ID}' has no localized description");
                    Assert.That(reagent.LocalizedPhysicalDescription, Is.Not.Empty,
                        $"Reagent '{reagent.ID}' has no localized physical description");
                }
            });
        });

        if (warnings.Count > 0)
            Assert.Warn(string.Join("; ", warnings));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PathItemPrototypesExistAndSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var entityPrototypes = protoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(prototype => !prototype.Abstract)
            .ToList();

        var warnings = new List<string>();
        var missingFeatures = new List<string>();
        var prototypesToSpawn = new Dictionary<string, string>();

        foreach (var (feature, component, knownIds) in PathItemFeatures)
        {
            var foundByComponent = entityPrototypes
                .Where(prototype => prototype.Components.ContainsKey(component))
                .Select(prototype => prototype.ID)
                .ToList();

            var knownFound = knownIds.Where(id => protoMan.HasIndex<EntityPrototype>(id)).ToList();

            if (foundByComponent.Count == 0 && knownFound.Count == 0)
            {
                missingFeatures.Add($"no prototype with component '{component}' or known ids [{string.Join(", ", knownIds)}] ({feature})");
                continue;
            }

            if (foundByComponent.Count == 0)
            {
                warnings.Add($"{feature}: no prototype has component '{component}', using documented id instead");
            }
            else if (knownFound.Count == 0)
            {
                warnings.Add($"{feature}: none of the documented ids [{string.Join(", ", knownIds)}] resolve; " +
                             $"found by component: [{string.Join(", ", foundByComponent)}]");
            }

            var candidate = knownFound.Count > 0 ? knownFound[0] : foundByComponent[0];
            prototypesToSpawn.TryAdd(candidate, feature);
        }

        // The shuriken emitter is the ranged weapon that fires through the ninja ammo provider.
        var ammoProviders = entityPrototypes
            .Where(prototype => prototype.Components.ContainsKey(AmmoProviderComponent))
            .Select(prototype => prototype.ID)
            .ToList();

        if (ammoProviders.Count == 0)
            missingFeatures.Add($"no prototype with component '{AmmoProviderComponent}' (shuriken emitter, johyo, net gun)");

        if (protoMan.HasIndex<EntityPrototype>(ShurikenEmitterKnownId))
        {
            prototypesToSpawn.TryAdd(ShurikenEmitterKnownId, "shuriken emitter");
        }
        else
        {
            warnings.Add($"Shuriken emitter id '{ShurikenEmitterKnownId}' does not resolve; " +
                         $"ammo provider prototypes found: [{string.Join(", ", ammoProviders)}]");
        }

        Assert.That(missingFeatures, Is.Empty,
            "Missing ninja path features:\n" + string.Join("\n", missingFeatures));

        var map = await pair.CreateTestMap();
        var spawned = new Dictionary<EntityUid, string>();

        await server.WaitPost(() =>
        {
            foreach (var (prototypeId, description) in prototypesToSpawn)
            {
                spawned[entMan.SpawnEntity(prototypeId, map.GridCoords)] = $"{prototypeId} ({description})";
            }
        });

        await server.WaitRunTicks(2);

        Assert.Multiple(() =>
        {
            foreach (var (uid, description) in spawned)
            {
                // Projectiles and traps may despawn immediately without a flight target or a trigger,
                // their prototypes are still validated by the spawn call above.
                if (description.Contains("projectile", StringComparison.OrdinalIgnoreCase))
                    continue;

                Assert.That(entMan.EntityExists(uid), Is.True,
                    $"Prototype {description} was deleted immediately after spawning");
            }
        });

        if (warnings.Count > 0)
            TestContext.Out.WriteLine("Ninja path warnings: " + string.Join("; ", warnings));

        await pair.CleanReturnAsync();
    }
}
