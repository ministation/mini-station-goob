// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Fishing.Sources;

[Serializable, NetSerializable]
public enum FishAnalyzerUiKey : byte
{
    Key,
}

/// <summary>
/// Everything the analyzer window needs to know about one scanned fish.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishAnalyzerEntry
{
    /// <summary>
    /// Entity prototype of the fish, used for its localized name.
    /// </summary>
    public string Species = string.Empty;

    /// <summary>
    /// Species data prototype, used for fluid, temperature, traits and evolutions.
    /// </summary>
    public string? Fish;

    public float Size;
    public float Weight;
    public bool Alive = true;
    public string? Catcher;

    /// <summary>
    /// All traits of this fish instance: species traits minus removed ones plus injected ones.
    /// </summary>
    public List<string> Traits = new();

    /// <summary>
    /// True when some traits were added by genetic engineering.
    /// </summary>
    public bool HasExtraTraits;

    /// <summary>
    /// Minutes into the shift the fish was caught, or -1 if unknown.
    /// </summary>
    public float CatchMinutes = -1f;
}

[Serializable, NetSerializable]
public sealed class FishAnalyzerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<FishAnalyzerEntry> Fish;

    public FishAnalyzerBoundUserInterfaceState(List<FishAnalyzerEntry> fish)
    {
        Fish = fish;
    }
}
