// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Content.Shared._Mini.Ninja;
using Content.Shared.Actions;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ninja;

/// <summary>
/// Helpers shared by the ninja integration tests.
/// </summary>
public static class NinjaTestHelpers
{
    /// <summary>
    /// Every module of the three-path port: Ghost 1-5, Snake 1-5, Steel 1-4.
    /// Steel tier 5 is intentionally absent (the reference does not implement it either).
    /// </summary>
    public static readonly IReadOnlyList<(NinjaPath Path, int Tier)> ExpectedModules = new (NinjaPath Path, int Tier)[]
    {
        (NinjaPath.Ghost, 1),
        (NinjaPath.Ghost, 2),
        (NinjaPath.Ghost, 3),
        (NinjaPath.Ghost, 4),
        (NinjaPath.Ghost, 5),
        (NinjaPath.Snake, 1),
        (NinjaPath.Snake, 2),
        (NinjaPath.Snake, 3),
        (NinjaPath.Snake, 4),
        (NinjaPath.Snake, 5),
        (NinjaPath.Steel, 1),
        (NinjaPath.Steel, 2),
        (NinjaPath.Steel, 3),
        (NinjaPath.Steel, 4),
    };

    /// <summary>
    /// Highest module tier of every path.
    /// </summary>
    public static readonly IReadOnlyDictionary<NinjaPath, int> MaxTierPerPath = new Dictionary<NinjaPath, int>
    {
        [NinjaPath.Ghost] = 5,
        [NinjaPath.Snake] = 5,
        [NinjaPath.Steel] = 4,
    };

    public const int MaxTier = 5;

    public static readonly string[] SupportedCultures = new[] { "en-US", "ru-RU" };

    /// <summary>
    /// Checks that a locale key exists and is resolved in every supported culture.
    /// </summary>
    /// <param name="hasFormattingArguments">Skip formatting; the message expects arguments that are not provided here.</param>
    public static void AssertLocalizedInBothCultures(
        ILocalizationManager locMan,
        string key,
        bool hasFormattingArguments = false)
    {
        Assert.That(key, Is.Not.Null.And.Not.Empty, "Localization key must not be empty");

        var originalCulture = locMan.DefaultCulture;

        try
        {
            foreach (var cultureName in SupportedCultures)
            {
                locMan.SetCulture(CultureInfo.GetCultureInfo(cultureName));

                Assert.That(locMan.HasString(key), Is.True,
                    $"Missing {cultureName} locale string '{key}'");

                if (hasFormattingArguments)
                    continue;

                var text = locMan.GetString(key);
                Assert.That(text, Is.Not.EqualTo(key),
                    $"Locale string '{key}' is not resolved in {cultureName}");
            }
        }
        finally
        {
            if (originalCulture != null)
                locMan.SetCulture(originalCulture);
        }
    }

    /// <summary>
    /// Checks that a sprite specifier points to an existing texture or to an existing RSI state.
    /// </summary>
    public static void AssertSpriteExists(IResourceManager resMan, SpriteSpecifier? icon, string context)
    {
        Assert.That(icon, Is.Not.Null, $"{context}: icon is not set");

        switch (icon)
        {
            case SpriteSpecifier.Rsi rsi:
            {
                var metaPath = rsi.RsiPath / "meta.json";
                Assert.That(resMan.ContentFileExists(metaPath), Is.True,
                    $"{context}: RSI manifest '{metaPath}' does not exist (sprite '{rsi.RsiPath}')");

                using var stream = resMan.ContentFileRead(metaPath);
                using var document = JsonDocument.Parse(stream);

                Assert.That(document.RootElement.TryGetProperty("states", out var states), Is.True,
                    $"{context}: RSI manifest '{metaPath}' has no states");

                var stateNames = states
                    .EnumerateArray()
                    .Select(state => state.TryGetProperty("name", out var name) ? name.GetString() : null)
                    .Where(name => name != null)
                    .ToHashSet();

                Assert.That(stateNames, Does.Contain(rsi.RsiState),
                    $"{context}: RSI state '{rsi.RsiState}' does not exist in '{rsi.RsiPath}'");
                break;
            }
            case SpriteSpecifier.Texture texture:
                Assert.That(resMan.ContentFileExists(texture.TexturePath), Is.True,
                    $"{context}: texture '{texture.TexturePath}' does not exist");
                break;
            default:
                Assert.Fail($"{context}: unexpected icon type {icon.GetType().Name}");
                break;
        }
    }

    /// <summary>
    /// Prototype ids of all actions currently granted to an entity.
    /// </summary>
    public static HashSet<string> GetActionPrototypeIds(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid holder)
    {
        var result = new HashSet<string>();

        foreach (var action in actions.GetActions(holder))
        {
            var meta = entMan.GetComponent<MetaDataComponent>(action.Owner);
            if (meta.EntityPrototype is { } prototype)
                result.Add(prototype.ID);
        }

        return result;
    }
}
