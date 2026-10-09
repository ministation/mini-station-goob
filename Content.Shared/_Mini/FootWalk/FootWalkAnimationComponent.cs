// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Mini.FootWalk;

/// <summary>
/// Client-side walk bob for humanoids (not borg chassis): per-leg foot lift plus a
/// Stardew-style whole-body bounce and arm swing.
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
    /// UV height of the boot band on full-body clothing (hardsuits).
    /// Keep low so only boots move, not the whole lower suit (avoids solid hop).
    /// </summary>
    [DataField]
    public float OuterFootCut = 0.2f;

    /// <summary>
    /// UV height of the pant region on the jumpsuit (hip line measured from sprite bottom).
    /// The region below the line is punched out of the suit and rebuilt as L/R halves that
    /// follow the legs, so bare shins never show between the hem and the shoes.
    /// </summary>
    [DataField]
    public float JumpsuitHipCut = 0.42f;

    /// <summary>
    /// Peak whole-body bounce on each footfall (Stardew-style): the sprite itself rises while
    /// legs and boots compensate, so torso, head and all clothes visibly ride up. ~1.25px at
    /// 32 PPCM. 0 disables the bounce.
    /// </summary>
    [DataField]
    public float BodyBounceAmplitude = 1.25f / 32f;

    /// <summary>
    /// Peak arm swing. Each arm rises in counter-phase, with the opposite foot. ~1px at 32 PPCM.
    /// 0 disables.
    /// </summary>
    [DataField]
    public float ArmSwingAmplitude = 1.0f / 32f;

    /// <summary>
    /// Whole-body bounce multiplier while sprinting (running reads as a stronger bounce).
    /// </summary>
    [DataField]
    public float SprintBounceFactor = 1.25f;

    /// <summary>
    /// Arm swing multiplier while sprinting.
    /// </summary>
    [DataField]
    public float SprintArmFactor = 1.4f;

    /// <summary>
    /// Bob intensity fade-in rate (per second), so the walk eases in on the first step.
    /// </summary>
    [DataField]
    public float RampInRate = 10f;

    /// <summary>
    /// Bob intensity fade-out rate (per second), so the walk eases out instead of popping
    /// back to rest the instant the mob stops.
    /// </summary>
    [DataField]
    public float RampOutRate = 14f;

    /// <summary>
    /// Client-only walk cycle phase (radians).
    /// </summary>
    [ViewVariables]
    public float Phase;

    /// <summary>
    /// Sprite offset the mob had before the whole-body bounce took over <c>Sprite.Offset</c>.
    /// </summary>
    [ViewVariables]
    public Vector2 BaseSpriteOffset;

    /// <summary>
    /// True while the whole-body bounce is applied to the sprite.
    /// </summary>
    [ViewVariables]
    public bool BodyBounceActive;

    /// <summary>
    /// Client-only bob intensity ramp (0..1): eases in on the first step, eases out on stop.
    /// </summary>
    [ViewVariables]
    public float BobRamp;

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
    /// Shoe source keys whose art crosses the sprite centre inside the foot band. Splitting those
    /// into halves would tear the art, so they bob as one piece.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> BandOnlyShoeSources = new();

    /// <summary>
    /// Runtime outerClothing foot-half layers (front N/S).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSplitKeys = new();

    /// <summary>
    /// Runtime jumpsuit pant-half layers (front N/S, region below JumpsuitHipCut).
    /// </summary>
    [ViewVariables]
    public readonly List<string> JumpsuitSplitKeys = new();

    /// <summary>
    /// Original jumpsuit layers currently tracked (torso part, hidden on front).
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HiddenJumpsuitKeys = new();

    /// <summary>
    /// Jumpsuit source keys whose art crosses the sprite centre inside the pant region
    /// (skirts, fused inseams). They cannot be X-split, so the pant band moves as one piece.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> BandOnlyJumpsuitSources = new();

    /// <summary>
    /// Runtime jumpsuit pant-band layers (side E/W and band-only sources).
    /// </summary>
    [ViewVariables]
    public readonly List<string> JumpsuitBandKeys = new();

    /// <summary>
    /// Original jumpsuit layers with the pant-region hole shader.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> HoledJumpsuitKeys = new();

    /// <summary>
    /// Runtime outerClothing foot-band layers (side E/W).
    /// </summary>
    [ViewVariables]
    public readonly List<string> OuterSideBandKeys = new();

    /// <summary>
    /// Outer clothing source keys whose art crosses the sprite centre inside the foot band. Long
    /// garments (coats, robes) would shear open at the cut, so they bob as one band instead.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<string> BandOnlyOuterSources = new();

    /// <summary>
    /// Offsets the layers had before this system moved them, per layer key. Resets restore these
    /// instead of zero, otherwise items with their own offset (clown shoes, roller skates) would
    /// jump after the first step the mob takes.
    /// </summary>
    [ViewVariables]
    public readonly Dictionary<string, Vector2> BaseOffsets = new();

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
    /// JumpsuitHipCut last applied to jumpsuit hole/band/half shaders (forces rebuild on change).
    /// </summary>
    [ViewVariables]
    public float AppliedJumpsuitHipCut = float.NaN;

    /// <summary>
    /// True while LFoot/RFoot sprite layers are hidden because shoes or outer clothing cover them.
    /// </summary>
    [ViewVariables]
    public bool BodyFeetHidden;

    /// <summary>
    /// Client: last FrameUpdate applied walk offsets. Idle entities skip ResetLowerBody entirely.
    /// </summary>
    [ViewVariables]
    public bool WasAnimating;
}
