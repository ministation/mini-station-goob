// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Goobstation.Shared.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingRodComponent : Component
{
    // Legacy Goob fields, kept while the old action based fishing is being replaced.

    /// <summary>
    /// Higher value will make every interact more productive.
    /// </summary>
    [DataField]
    public float Efficiency = 1f;

    [DataField]
    public float StartingProgress = 0.33f;

    [DataField]
    public float StartingStruggleTime = 0.3f;

    [DataField]
    public float BreakOnDistance = 8f;

    [DataField]
    public EntProtoId FloatPrototype = "FishingLure";

    [DataField]
    public EntProtoId ThrowLureActionId = "ActionStartFishing";

    [DataField, AutoNetworkedField]
    public EntityUid? ThrowLureActionEntity;

    [DataField]
    public EntProtoId PullLureActionId = "ActionStopFishing";

    [DataField, AutoNetworkedField]
    public EntityUid? PullLureActionEntity;

    // tg fishing fields.

    /// <summary>
    /// How far the lure can be cast, in tiles.
    /// </summary>
    [DataField]
    public float CastRange = 3f;

    /// <summary>
    /// Additive modifier to the minigame difficulty.
    /// </summary>
    [DataField]
    public float DifficultyModifier;

    [DataField]
    public EntProtoId LurePrototype = "FishingLure";

    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("_Goobstation/Objects/Specific/Fishing/fishing_lure.rsi"), "rope");

    [DataField, ViewVariables]
    public Vector2 RopeUserOffset = new(0f, 0f);

    [DataField, ViewVariables]
    public Vector2 RopeLureOffset = new(0f, 0f);

    /// <summary>
    /// Minigame tuning multipliers. Equipment may modify them through events.
    /// </summary>
    [DataField]
    public float CompletionSpeedMultiplier = 1f;

    [DataField]
    public float BaitSpeedMultiplier = 1f;

    [DataField]
    public float GravityMultiplier = 1f;

    [DataField]
    public float BouncinessMultiplier = 1f;

    [DataField]
    public float BaitHeightMultiplier = 1f;

    [DataField]
    public float DecelerationMultiplier = 1f;

    [DataField]
    public float ExperienceMultiplier = 1f;

    /// <summary>
    /// Frame state from the fishing HUD RSI used by the minigame window.
    /// </summary>
    [DataField]
    public string FrameState = "frame_wood";

    /// <summary>
    /// Currently cast lure, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? FishingLure;
}
