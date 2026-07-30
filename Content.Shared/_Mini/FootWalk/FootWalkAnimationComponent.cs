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
    public float Amplitude = 2.5f / 32f;

    /// <summary>
    /// Walk cycle speed in radians per second at a normal walk.
    /// </summary>
    [DataField]
    public float CycleSpeed = 9f;

    [DataField]
    public float WalkRate = 0.6375f;

    [DataField]
    public float SprintRate = 1.2025f;

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
    /// Local UV height of the boot band on full-body clothing (hardsuits).
    /// Keep low so only boots move, not the whole lower suit (avoids solid hop).
    /// </summary>
    [DataField]
    public float OuterFootCut = 0.2f;

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
    /// Original shoe layers currently tracked (hidden on front, shown on side).
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HiddenShoeKeys = new();

    /// <summary>
    /// Runtime outerClothing foot-half layers (front N/S).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSplitKeys = new();

    /// <summary>
    /// Runtime outerClothing foot-band layers (side E/W).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSideBandKeys = new();

    /// <summary>
    /// Original outerClothing layers with foot-hole shader.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HoledOuterKeys = new();

    /// <summary>
    /// True while any clothing walk layers are built (front halves and/or side bands).
    /// </summary>
    [ViewVariables]
    public bool ClothingSplitsActive;

    /// <summary>
    /// Last applied clothing mode so facing changes only toggle visibility.
    /// 0 = none, 1 = front (N/S halves), 2 = side (E/W band).
    /// </summary>
    [ViewVariables]
    public byte ClothingMode;

    /// <summary>
    /// OuterFootCut last applied to hole/band/half shaders (forces rebuild on change).
    /// </summary>
    [ViewVariables]
    public float AppliedOuterFootCut = float.NaN;

    /// <summary>
    /// True while LFoot/RFoot sprite layers are hidden because shoes or outer clothing cover them.
    /// </summary>
    [ViewVariables]
    public bool BodyFeetHidden;
}
