// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.FootWalk;

/// <summary>
/// Client-side lower-body walk bob for humanoids (not borg chassis).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FootWalkAnimationComponent : Component
{
    /// <summary>
    /// Peak lift in sprite units. ~2.8px at 32 PPCM.
    /// </summary>
    [DataField]
    public float Amplitude = 2.8f / 32f;

    /// <summary>
    /// Walk cycle speed in radians per second at a normal walk.
    /// </summary>
    [DataField]
    public float CycleSpeed = 10f;

    [DataField]
    public float WalkRate = 0.6375f;

    [DataField]
    public float SprintRate = 1.4025f;

    [DataField]
    public float MinSlowFactor = 0.35f;

    [DataField]
    public float MaxSlowFactor = 1.1f;

    [DataField]
    public float MinSpeedSquared = 0.04f;

    /// <summary>
    /// Far-foot amplitude multiplier when facing E/W (avoids one-leg hop).
    /// </summary>
    [DataField]
    public float SideFarAmplitudeFactor = 0.4f;

    /// <summary>
    /// Local UV height of the foot band on full-body clothing (hardsuits).
    /// UV2.y == 0 at sprite bottom.
    /// </summary>
    [DataField]
    public float OuterFootCut = 0.35f;

    /// <summary>
    /// Client-only walk cycle phase (radians).
    /// </summary>
    [ViewVariables]
    public float Phase;

    [ViewVariables]
    public readonly HashSet<Enum> TouchedEnumLayers = new();

    [ViewVariables]
    public readonly HashSet<string> TouchedStringLayers = new();

    /// <summary>
    /// Runtime shoe half layers ({key}-walk-L / {key}-walk-R).
    /// </summary>
    [ViewVariables]
    public readonly List<string> ShoeSplitKeys = new();

    /// <summary>
    /// Original shoe layers currently hidden while splits are active.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HiddenShoeKeys = new();

    /// <summary>
    /// Runtime outerClothing foot-half layers (N/S).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSplitKeys = new();

    /// <summary>
    /// Runtime outerClothing foot-band layers (E/W, no X-split).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSideBandKeys = new();

    /// <summary>
    /// Original outerClothing layers that had a foot-hole shader applied.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HoledOuterKeys = new();

    /// <summary>
    /// True while N/S half-split clothing layers are active (false on E/W).
    /// </summary>
    [ViewVariables]
    public bool ClothingSplitsActive;
}
