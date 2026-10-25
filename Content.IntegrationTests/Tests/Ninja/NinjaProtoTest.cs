// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Prototypes;
using Content.Shared.RetractableItemAction;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Static data checks of the three-path ninja port: modules, Spider OS, boot scripts and their metadata.
/// </summary>
[TestFixture]
public sealed class NinjaProtoTest
{
    private const string OsPrototypeId = "NinjaOs";
    private const string ActivationBootId = "NinjaOsActivationBoot";
    private const string DeactivationBootId = "NinjaOsDeactivationBoot";

    [Test]
    public async Task ModulesMatchThreePathLayout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        var modules = protoMan.EnumeratePrototypes<NinjaModulePrototype>().ToList();

        Assert.Multiple(() =>
        {
            var combinations = modules.Select(module => (module.Path, module.Tier)).ToList();

            foreach (var (path, tier) in NinjaTestHelpers.ExpectedModules)
            {
                Assert.That(combinations, Does.Contain((path, tier)),
                    $"Missing ninjaModule for path {path}, tier {tier}");
            }

            Assert.That(combinations, Has.Count.EqualTo(NinjaTestHelpers.ExpectedModules.Count),
                "The port must have exactly 14 modules: Ghost 1-5, Snake 1-5, Steel 1-4");

            Assert.That(combinations.Distinct().Count(), Is.EqualTo(combinations.Count),
                "More than one ninjaModule shares the same path and tier");

            Assert.That(modules.Select(module => module.ID).Distinct().Count(), Is.EqualTo(modules.Count),
                "Duplicate ninjaModule ids");

            foreach (var module in modules)
            {
                Assert.That(module.Tier, Is.InRange(1, NinjaTestHelpers.MaxTier),
                    $"{module.ID}: tier {module.Tier} is outside 1..{NinjaTestHelpers.MaxTier}");
                Assert.That(module.ID, Does.StartWith("NinjaModule"),
                    $"{module.ID}: module ids must follow the NinjaModule<Path><Name> convention");
            }

            var paths = modules.Select(module => module.Path).Distinct().ToHashSet();
            Assert.That(paths, Is.EquivalentTo(new[] { NinjaPath.Ghost, NinjaPath.Snake, NinjaPath.Steel }),
                "All three paths must have modules");

            var steelTierFive = modules.Where(module => module.Path == NinjaPath.Steel && module.Tier == 5).ToList();
            Assert.That(steelTierFive, Is.Empty, "Steel tier 5 must stay empty (not implemented in the reference)");

            foreach (var (path, maxTier) in NinjaTestHelpers.MaxTierPerPath)
            {
                var pathModules = modules.Where(module => module.Path == path).Select(module => module.Tier).ToList();
                Assert.That(pathModules, Has.Count.EqualTo(maxTier), $"{path} must have exactly {maxTier} modules");
                Assert.That(pathModules, Is.EquivalentTo(Enumerable.Range(1, maxTier)),
                    $"{path} must have exactly one module per tier 1..{maxTier}");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModulesHaveMetadataAndLocalization()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var resMan = server.ResolveDependency<IResourceManager>();
        var locMan = server.ResolveDependency<ILocalizationManager>();

        var modules = protoMan.EnumeratePrototypes<NinjaModulePrototype>().ToList();
        Assert.That(modules, Is.Not.Empty, "No ninjaModule prototypes were loaded");

        Assert.Multiple(() =>
        {
            foreach (var module in modules)
            {
                Assert.That(module.Name.Id, Is.Not.Null.And.Not.Empty, $"{module.ID}: name key is empty");
                Assert.That(module.Description.Id, Is.Not.Null.And.Not.Empty, $"{module.ID}: description key is empty");
                Assert.That(module.Icon, Is.Not.Null, $"{module.ID}: icon is not set");

                NinjaTestHelpers.AssertSpriteExists(resMan, module.Icon, module.ID);
                NinjaTestHelpers.AssertLocalizedInBothCultures(locMan, module.Name);
                NinjaTestHelpers.AssertLocalizedInBothCultures(locMan, module.Description);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ModuleComponentsAndActionsResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        var modules = protoMan.EnumeratePrototypes<NinjaModulePrototype>().ToList();
        Assert.That(modules, Is.Not.Empty, "No ninjaModule prototypes were loaded");

        var moduleActionIds = modules
            .Where(module => module.Actions != null)
            .SelectMany(module => module.Actions!)
            .Select(action => action.Id)
            .ToHashSet();

        Assert.Multiple(() =>
        {
            foreach (var module in modules)
            {
                foreach (var (componentName, entry) in module.Components)
                {
                    var componentType = entry.Component.GetType();
                    Assert.That(factory.TryGetRegistration(componentType, out _), Is.True,
                        $"{module.ID}: component '{componentName}' ({componentType.Name}) is not registered by the component factory");
                }

                if (module.Actions == null)
                    continue;

                foreach (var action in module.Actions)
                {
                    Assert.That(protoMan.HasIndex<EntityPrototype>(action.Id), Is.True,
                        $"{module.ID}: action entity prototype '{action.Id}' does not exist");
                }
            }

            // Every retractable action that belongs to the ninja must be able to resolve the item it summons.
            var retractablePrototypes = protoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(prototype => prototype.Components.ContainsKey("RetractableItemAction"))
                .Where(prototype => moduleActionIds.Contains(prototype.ID) || prototype.ID.Contains("Ninja"));

            foreach (var prototype in retractablePrototypes)
            {
                var retractable = (RetractableItemActionComponent) prototype.Components["RetractableItemAction"].Component;
                Assert.That(protoMan.HasIndex<EntityPrototype>(retractable.SpawnedPrototype.Id), Is.True,
                    $"Action prototype '{prototype.ID}' spawns missing entity prototype '{retractable.SpawnedPrototype.Id}'");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OsAndBootScriptsAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var locMan = server.ResolveDependency<ILocalizationManager>();

        Assert.That(protoMan.TryIndex<NinjaOsPrototype>(OsPrototypeId, out var os), Is.True,
            $"Missing ninjaOs prototype '{OsPrototypeId}'");

        Assert.That(os!.Modules, Is.Empty,
            $"'{OsPrototypeId}' must leave Modules empty so that every registered ninjaModule is available");
        Assert.That(os.MaxTier, Is.EqualTo(NinjaTestHelpers.MaxTier),
            $"'{OsPrototypeId}' must have MaxTier {NinjaTestHelpers.MaxTier}");

        Assert.That(protoMan.TryIndex<NinjaOsBootPrototype>(ActivationBootId, out var activation), Is.True,
            $"Missing ninjaOsBoot prototype '{ActivationBootId}'");
        Assert.That(protoMan.TryIndex<NinjaOsBootPrototype>(DeactivationBootId, out var deactivation), Is.True,
            $"Missing ninjaOsBoot prototype '{DeactivationBootId}'");

        Assert.Multiple(() =>
        {
            ValidateBootScript(activation!, locMan, ActivationBootId);
            ValidateBootScript(deactivation!, locMan, DeactivationBootId);

            var activationChecks = GetChecks(activation!);
            Assert.That(activationChecks, Is.SupersetOf(new[]
            {
                NinjaOsBootCheck.SelfTest,
                NinjaOsBootCheck.Authorized,
                NinjaOsBootCheck.NotActivated,
                NinjaOsBootCheck.ModulesReady,
                NinjaOsBootCheck.VisorSecure,
                NinjaOsBootCheck.HelmetSecure,
                NinjaOsBootCheck.MaskSecure,
                NinjaOsBootCheck.GlovesSecure,
                NinjaOsBootCheck.BootsSecure,
                NinjaOsBootCheck.SuitFasten,
                NinjaOsBootCheck.BatteryReport,
            }), $"'{ActivationBootId}' must run every activation boot check");

            var deactivationChecks = GetChecks(deactivation!);
            Assert.That(deactivationChecks, Is.SupersetOf(new[]
            {
                NinjaOsBootCheck.Authorized,
                NinjaOsBootCheck.VisorSecure,
                NinjaOsBootCheck.HelmetSecure,
                NinjaOsBootCheck.MaskSecure,
                NinjaOsBootCheck.GlovesSecure,
                NinjaOsBootCheck.BootsSecure,
                NinjaOsBootCheck.SuitFasten,
            }), $"'{DeactivationBootId}' must run every deactivation boot check");

            // The port intentionally shortens the reference boot delays, keep it that way.
            var activationDuration = activation!.Steps.Sum(step => step.Delay);
            Assert.That(activationDuration, Is.InRange(1f, 20f),
                $"'{ActivationBootId}' total delay {activationDuration}s must stay around 8-10s, not the reference 144s");

            var deactivationDuration = deactivation!.Steps.Sum(step => step.Delay);
            Assert.That(deactivationDuration, Is.InRange(0.5f, 10f),
                $"'{DeactivationBootId}' total delay {deactivationDuration}s is too long");
        });

        await pair.CleanReturnAsync();
    }

    private static void ValidateBootScript(NinjaOsBootPrototype boot, ILocalizationManager locMan, string bootId)
    {
        Assert.That(boot.Steps, Is.Not.Empty, $"'{bootId}' has no steps");

        foreach (var step in boot.Steps)
        {
            Assert.That(step.Delay, Is.GreaterThan(0f), $"'{bootId}' has a step with a non-positive delay");
            Assert.That(step.Log.Id, Is.Not.Null.And.Not.Empty, $"'{bootId}' has a step without a log key");

            // The battery capacity log line is the only one that expects a format argument.
            NinjaTestHelpers.AssertLocalizedInBothCultures(locMan, step.Log,
                step.Log.Id == "ninja-os-boot-battery-capacity");

            if (step.Check is { } check)
            {
                Assert.That(Enum.IsDefined(typeof(NinjaOsBootCheck), check), Is.True,
                    $"'{bootId}' references undefined boot check {check}");
            }
        }
    }

    private static HashSet<NinjaOsBootCheck> GetChecks(NinjaOsBootPrototype boot)
    {
        return boot.Steps
            .Where(step => step.Check != null)
            .Select(step => step.Check!.Value)
            .ToHashSet();
    }
}
