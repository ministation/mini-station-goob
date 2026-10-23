// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics;
using System.Numerics;
using Content.Client.Clothing;
using Content.Client.Inventory;
using Content.Shared._Mini.FootWalk;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Gravity;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Goobstation.Shared.Waddle;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Mini.FootWalk;

/// <summary>
/// Walk bob for each lower-body side (leg + foot + markings).
/// Shoes are split into L/R halves; hardsuit boot band is punched and split the same way.
/// Far foot is suppressed when facing E/W.
/// On top sits a Stardew-style whole-body bounce: the sprite itself rises on each footfall while
/// legs/feet/boots compensate, so torso, head and every piece of clothing visibly bob.
/// </summary>
public sealed partial class FootWalkAnimationSystem : EntitySystem
{
    private const string ShoesSlot = "shoes";
    private const string OuterSlot = "outerClothing";
    private const string JumpsuitSlot = "jumpsuit";
    private const string WalkLeftSuffix = "-walk-L";
    private const string WalkRightSuffix = "-walk-R";
    private const string WalkBandSuffix = "-walk-band";
    private const string SleeveSuffix = "-walk-sleeve";

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private bool _enabled = true;
    private bool _bodyBounce = true;

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<BorgChassisComponent> _borgQuery;
    private EntityQuery<WaddleAnimationComponent> _waddleQuery;
    private EntityQuery<InputMoverComponent> _moverQuery;
    private EntityQuery<MovementSpeedModifierComponent> _moveSpeedQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;
    private EntityQuery<InventorySlotsComponent> _invSlotsQuery;

    /// <summary>
    /// Centre-column alpha of every loaded RSI frame, taken from the CPU atlas sheet: the pixel
    /// inspection below costs memory reads instead of GPU readbacks.
    /// </summary>
    private readonly RsiCentreProfiles _centreProfiles = new();

    /// <summary>
    /// Original RSI and state of clothing layers whose lower band was replaced with a cut texture
    /// (the old shader holes), so the layer can be restored when the walk animation stops.
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<string, (RSI Rsi, RSI.StateId State)>> _holedOriginals = new();

    // Leg+foot: IPC/cyber legs bake most of the foot into the leg sprite.
    private static readonly HumanoidVisualLayers[] LeftLayers =
    [
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.LFoot,
    ];

    private static readonly HumanoidVisualLayers[] RightLayers =
    [
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.RFoot,
    ];

    private static readonly HumanoidVisualLayers[] LeftArmLayers =
    [
        HumanoidVisualLayers.LArm,
    ];

    private static readonly HumanoidVisualLayers[] RightArmLayers =
    [
        HumanoidVisualLayers.RArm,
    ];

    /// <summary>
    /// Garments that get sleeve layers, with the limbs each of them can cover.
    /// </summary>
    private static readonly (string Slot, HumanoidVisualLayers[] Limbs)[] SleeveSlots =
    [
        (JumpsuitSlot, [HumanoidVisualLayers.LArm, HumanoidVisualLayers.RArm]),
        (OuterSlot, [HumanoidVisualLayers.LArm, HumanoidVisualLayers.RArm]),
    ];

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _borgQuery = GetEntityQuery<BorgChassisComponent>();
        _waddleQuery = GetEntityQuery<WaddleAnimationComponent>();
        _moverQuery = GetEntityQuery<InputMoverComponent>();
        _moveSpeedQuery = GetEntityQuery<MovementSpeedModifierComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>();
        _invSlotsQuery = GetEntityQuery<InventorySlotsComponent>();

        _cfg.OnValueChanged(MiniCCVars.FootWalkAnimationEnabled, enabled =>
        {
            _enabled = enabled;
            if (!enabled)
                DisableAllAnimations();
        }, true);

        _cfg.OnValueChanged(MiniCCVars.FootWalkBodyBounceEnabled, enabled =>
        {
            _bodyBounce = enabled;
            if (!enabled)
                DisableBodyBounce();
        }, true);

        SubscribeLocalEvent<FootWalkAnimationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FootWalkAnimationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FootWalkAnimationComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<FootWalkAnimationComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<FootWalkAnimationComponent, AppearanceChangeEvent>(OnAppearanceChanged,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<FootWalkAnimationComponent, VisualsChangedEvent>(OnVisualsChanged,
            after: [typeof(ClientClothingSystem)]);

        _centreProfiles.Initialize(_resourceCache);
    }

    public override void Shutdown()
    {
        base.Shutdown();
    }

    private void OnStartup(Entity<FootWalkAnimationComponent> ent, ref ComponentStartup args)
    {
        // Facing-aware splits are applied in FrameUpdate.
    }

    private void OnAppearanceChanged(Entity<FootWalkAnimationComponent> ent, ref AppearanceChangeEvent args)
    {
        // Any appearance change makes the clothing system re-create the shoe/outer layers from
        // scratch, and the copies made here are left behind: the source layer comes back visible on
        // top of them and the punched hole is gone. Drop everything, the next walk frame rebuilds it.
        if (_enabled)
            ClearClothingWalkLayers(ent);
    }

    private void OnShutdown(Entity<FootWalkAnimationComponent> ent, ref ComponentShutdown args)
    {
        if (_spriteQuery.TryGetComponent(ent.Owner, out var sprite))
        {
            SetBodyFeetHidden(ent, sprite, hide: false);
            StopBodyBounce(ent, sprite);
        }

        ResetLowerBody(ent);
        ClearClothingWalkLayers(ent);
        ClearJointPatches(ent);
        ent.Comp.WasAnimating = false;
    }

    private void OnDidEquip(Entity<FootWalkAnimationComponent> ent, ref DidEquipEvent args)
    {
        if (!_enabled || args.Slot is not (ShoesSlot or OuterSlot or JumpsuitSlot))
            return;

        // Force rebuild next frame for current facing.
        ClearClothingWalkLayers(ent);
    }

    private void OnDidUnequip(Entity<FootWalkAnimationComponent> ent, ref DidUnequipEvent args)
    {
        if (!_enabled)
            return;

        if (args.Slot == ShoesSlot)
        {
            ClearShoeSplits(ent);
            ent.Comp.ClothingMode = 0;
        }
        else if (args.Slot == OuterSlot)
        {
            ClearOuterSplits(ent, clearHole: false);
            ClearOuterSideBands(ent, clearHole: true);
            ent.Comp.ClothingMode = 0;
        }
        else if (args.Slot == JumpsuitSlot)
        {
            ClearJumpsuitSplits(ent, clearHole: false);
            ClearJumpsuitSideBands(ent, clearHole: true);
            ent.Comp.ClothingMode = 0;
        }
    }

    private void OnVisualsChanged(Entity<FootWalkAnimationComponent> ent, ref VisualsChangedEvent args)
    {
        if (!_enabled || args.ContainerId is not (ShoesSlot or OuterSlot or JumpsuitSlot))
            return;

        ClearClothingWalkLayers(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        if (!_enabled)
            return;

        // FrameUpdate uses real wall-clock delta (same as original). Entity Update can run
        // multiple predicted ticks per frame and made the bob look too fast.
        var query = EntityQueryEnumerator<FootWalkAnimationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walk, out _))
        {
            if (!_spriteQuery.TryGetComponent(uid, out var sprite) || !sprite.Visible)
                continue;

            if (!_physicsQuery.TryGetComponent(uid, out var physics))
                continue;

            // Mini fix: velocity relative to whatever the mob stands on (grid/shuttle). In RT
            // PhysicsComponent.LinearVelocity is already relative to the parent (see
            // SharedPhysicsSystem.GetMapVelocities, which adds the parents' velocities on top),
            // so a mob standing still on a moving shuttle has it at zero, and a walking mob has
            // its walking speed there. Subtracting the grid's velocity is therefore wrong: on a
            // moving shuttle it produced a fake speed ~= shuttle speed and played the walk bob
            // on everyone standing still. The regression test
            // Content.IntegrationTests/Tests/_Mini/FootWalkMovingGridTest guards this.
            var velocity = physics.LinearVelocity;

            // Cheap reject for the common idle case before CanAnimate / gravity checks.
            var moving = velocity.LengthSquared() >= walk.MinSpeedSquared
                         && CanAnimate(uid)
                         && HasLowerBodyVisuals(uid, sprite);

            if (moving)
            {
                walk.WasAnimating = true;
                walk.BobRamp = MathF.Min(1f, walk.BobRamp + frameTime * walk.RampInRate);
            }
            else if (walk.BobRamp > 0f)
            {
                // Ease the bob out over a few frames instead of snapping offsets to zero.
                walk.BobRamp = MathF.Max(0f, walk.BobRamp - frameTime * walk.RampOutRate);
            }
            else
            {
                StopAnimating((uid, walk), sprite);
                continue;
            }

            // Must match sprite RSI direction (world + eye), otherwise camera turns invert bob / wrong mode.
            var facing = GetScreenFacing(uid);
            var frontMode = facing is RsiDirection.South or RsiDirection.North;

            // Undo last tick's offsets before building anything: the rebuild below records each
            // layer's rest offset, and a layer still carrying this frame's lift would bake it into
            // the base and then add the next lift on top, drifting a little further on every turn.
            ResetTouchedOffsets((uid, walk), sprite);

            // Setting clothing layers up walks the clothing RSIs (alpha profiles, splitters); a slow
            // setup for one mob is what shows up as a freeze when a new entity walks into view.
            var setupStart = Stopwatch.GetTimestamp();

            EnsureClothingModeIfNeeded((uid, walk), frontMode, facing);
            EnsureJointPatches((uid, walk), sprite);

            var setupMs = (Stopwatch.GetTimestamp() - setupStart) * 1000.0 / Stopwatch.Frequency;

            // 15 ms is too tight for loaded CI runners and first-time RSI loads, where a
            // normal setup can take 20-30 ms and the warning is mapped to a test failure.
            // 50 ms is where the hitch becomes visible, so real regressions are still caught.
            if (setupMs >= 50.0)
                Log.Warning($"Foot walk: setup for {ToPrettyString(uid)} took {setupMs:F1} ms");

            var hasShoes = HasSlotVisuals(uid, ShoesSlot);
            var hasOuter = HasSlotVisuals(uid, OuterSlot);
            var hasJumpsuit = HasSlotVisuals(uid, JumpsuitSlot);
            // Never show bare feet under shoes or a hardsuit boot band/hole.
            SetBodyFeetHidden((uid, walk), sprite, hide: hasShoes || hasOuter);

            var speed = velocity.Length();
            walk.Phase += frameTime * walk.CycleSpeed * GetStepRate(uid, walk, speed);

            var leftAmp = walk.Amplitude;
            var rightAmp = walk.Amplitude;

            // Far foot still bobs a bit on side view so it doesn't look like a one-leg hop.
            if (facing == RsiDirection.East)
                leftAmp *= walk.SideFarAmplitudeFactor;
            else if (facing == RsiDirection.West)
                rightAmp *= walk.SideFarAmplitudeFactor;

            var leftY = MathF.Max(0f, MathF.Sin(walk.Phase)) * leftAmp * walk.BobRamp;
            var rightY = MathF.Max(0f, MathF.Sin(walk.Phase + MathF.PI)) * rightAmp * walk.BobRamp;

            var sprinting = _moverQuery.TryGetComponent(uid, out var mover) && mover.Sprinting;

            // Stardew-style whole-body bounce: the sprite itself hops on each footfall and every
            // layer rides it, feet included - like the baked walk frames in Stardew. Every other
            // offset below is a pure lift (>= 0), so parts can only overlap more, never separate:
            // no slits can open between limbs, naked or clothed.
            var bodyY = 0f;
            if (_bodyBounce && walk.BodyBounceAmplitude > 0f)
            {
                bodyY = MathF.Abs(MathF.Sin(walk.Phase)) * walk.BodyBounceAmplitude
                        * (sprinting ? walk.SprintBounceFactor : 1f) * walk.BobRamp;
                EnsureBodyBounce((uid, walk), sprite);
                walk.CurrentBounceY = bodyY;
                _sprite.SetOffset((uid, sprite), walk.BaseSpriteOffset + new Vector2(0f, bodyY));
            }
            else if (walk.BodyBounceActive)
            {
                StopBodyBounce((uid, walk), sprite);
            }

            // Arms swing in counter-phase: each arm rises with the opposite foot.
            var armScale = (walk.Amplitude > 0f ? walk.ArmSwingAmplitude / walk.Amplitude : 0f)
                           * (sprinting ? walk.SprintArmFactor : 1f);
            var leftArmY = rightY * armScale;
            var rightArmY = leftY * armScale;
            if (facing == RsiDirection.East)
                leftArmY *= walk.SideFarAmplitudeFactor;
            else if (facing == RsiDirection.West)
                rightArmY *= walk.SideFarAmplitudeFactor;

            // Side near foot (the one toward the camera for E/W sprites).
            var nearY = facing == RsiDirection.East ? rightY : leftY;

            // Mini: right after the reset the layers sit in their rest pose, so this is the sprite
            // height without the walk animation. Overlays above the head (status icons, health bar,
            // do-after) anchor on it instead of the animated bounds, otherwise the raising legs moved
            // them ~1.25 px every step - that was the «худ дёргается при ходьбе».
            walk.RestBoundsHeight = _sprite.GetLocalBounds((uid, sprite)).Height;

            _humanoidQuery.TryGetComponent(uid, out var humanoid);

            if (frontMode)
            {
                // South: sprite left ≈ RFoot. North mirrors L/R on the sheet (clothing only).
                // Body layers stay anatomical L/R - never invert with the camera sheet.
                var invert = facing == RsiDirection.North;

                ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY), skipFeet: hasShoes || hasOuter);
                ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY), skipFeet: hasShoes || hasOuter);

                ApplySplitHalves((uid, sprite), walk, walk.ShoeSplitKeys, leftY, rightY, invert);

                // Outer "splits" are the hardsuit BOOT halves (the cut texture keeps the boot band,
                // not the suit body) - they are footwear and must ride the legs exactly.
                ApplySplitHalves((uid, sprite), walk, walk.OuterSplitKeys, leftY, rightY, invert);

                // Pant halves ride their leg exactly; lifts are never negative, so a half can
                // only slide up over the holed torso, never open a slit at the hip.
                ApplySplitHalves((uid, sprite), walk, walk.JumpsuitSplitKeys, leftY, rightY, invert);

                // Footwear and garments that cannot be cut (their art has no centre gap) stay on
                // the full sprite or band: one piece, bouncing on each footfall instead of shearing.
                var singlePieceY = MathF.Max(leftY, rightY);
                ApplyFullSlotOffset((uid, sprite), walk, ShoesSlot, singlePieceY);
                ApplyBandOffset((uid, sprite), walk, walk.OuterSideBandKeys, singlePieceY);
                ApplyBandOffset((uid, sprite), walk, walk.JumpsuitBandKeys, singlePieceY);
            }
            else
            {
                // Side: one silhouette - clothing lifts with near foot; don't alternate bare feet under boots.
                if (hasOuter)
                {
                    // Full suit: legs and the suit both move with the near foot.
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, nearY), skipFeet: true);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, nearY), skipFeet: true);
                }
                else if (hasJumpsuit)
                {
                    // Jumpsuit: the pant band rides the near leg, so both legs must follow it -
                    // otherwise the far leg slides out of the pants on every step.
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, nearY), skipFeet: false);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, nearY), skipFeet: false);
                }
                else if (hasShoes)
                {
                    // Pants + shoes: legs can alternate; feet stay hidden under shoes.
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY), skipFeet: true);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY), skipFeet: true);
                }
                else
                {
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY), skipFeet: false);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY), skipFeet: false);
                }

                if (hasShoes)
                    ApplyFullSlotOffset((uid, sprite), walk, ShoesSlot, nearY);

                if (hasOuter)
                    ApplyBandOffset((uid, sprite), walk, walk.OuterSideBandKeys, nearY);

                if (hasJumpsuit)
                    ApplyBandOffset((uid, sprite), walk, walk.JumpsuitBandKeys, nearY);
            }

            // Arms swing in counter-phase with the opposite foot. Every garment worn over an arm has a
            // sleeve copy that rides the same offset, so the cloth moves with the limb and the bare arm
            // can never slip out from under it.
            if (walk.ArmSwingAmplitude > 0f)
            {
                ApplySide((uid, sprite), walk, humanoid, LeftArmLayers, new Vector2(0f, leftArmY));
                ApplySide((uid, sprite), walk, humanoid, RightArmLayers, new Vector2(0f, rightArmY));
                ApplySleeveCover((uid, walk), sprite, leftArmY, rightArmY);
            }
        }
    }

    private void StopAnimating(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (!ent.Comp.WasAnimating)
            return;

        // Reset first: it restores the offsets the layers had before the bob, and clearing after it
        // drops the records it just used.
        SetBodyFeetHidden(ent, sprite, hide: false);
        ResetLowerBody(ent, sprite);
        ClearClothingWalkLayers(ent);
        ClearJointPatches(ent, sprite);
        StopBodyBounce(ent, sprite);
        ent.Comp.WasAnimating = false;
        ent.Comp.Phase = 0f;
        ent.Comp.BobRamp = 0f;
        // Layers are back at their rest pose: the live bounds are stable again.
        ent.Comp.RestBoundsHeight = 0f;
    }

    /// <summary>
    /// Takes over the whole sprite offset on the first walking frame.
    /// </summary>
    private void EnsureBodyBounce(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.BodyBounceActive)
            return;

        ent.Comp.BaseSpriteOffset = sprite.Offset;
        ent.Comp.BodyBounceActive = true;
    }

    private void StopBodyBounce(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (!ent.Comp.BodyBounceActive)
            return;

        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.BaseSpriteOffset);
        ent.Comp.BodyBounceActive = false;
        ent.Comp.CurrentBounceY = 0f;
    }

    private void DisableBodyBounce()
    {
        var query = EntityQueryEnumerator<FootWalkAnimationComponent>();
        while (query.MoveNext(out var uid, out var walk))
        {
            if (!walk.BodyBounceActive || !_spriteQuery.TryGetComponent(uid, out var sprite))
                continue;

            StopBodyBounce((uid, walk), sprite);
        }
    }

    /// <summary>
    /// Joint patches: rest-pose duplicates of the moving limb layers, inserted below the torso.
    /// When a limb lifts, its vacated pixels are filled by the patch with the same art - the
    /// "root" of the limb stays put while the limb itself rises, so no slit can open at the
    /// joint (this is the closest a dynamic sprite rig gets to Stardew's baked overlap).
    /// </summary>
    private void EnsureJointPatches(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.JointPatchKeys.Count > 0)
            return;

        // Below the groin: the patch only shows where every upper layer is empty - exactly the
        // strip a lifting limb vacates.
        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), HumanoidVisualLayers.Groin, out var insertAt, false))
            insertAt = 0;

        foreach (var limb in new[] { HumanoidVisualLayers.LLeg, HumanoidVisualLayers.RLeg, HumanoidVisualLayers.LArm, HumanoidVisualLayers.RArm })
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), limb, out var limbIndex, false)
                || !_sprite.TryGetLayer((ent.Owner, sprite), limbIndex, out var src, false))
                continue;

            var key = $"walk-patch-{limb}";
            var layer = _sprite.AddBlankLayer((ent.Owner, sprite), insertAt);
            _sprite.LayerMapSet((ent.Owner, sprite), key, insertAt);

            var rsi = src.ActualRsi;
            if (rsi != null)
                _sprite.LayerSetRsi(layer, rsi, src.State);
            else if (src.Texture != null)
                _sprite.LayerSetTexture(layer, src.Texture);

            _sprite.LayerSetColor(layer, src.Color);

            // Mid-walk the limb layer carries an animation offset; the patch must hold the rest pose.
            _sprite.LayerSetOffset(layer, ent.Comp.TouchedEnumLayers.Contains(limb) ? Vector2.Zero : src.Offset);
            _sprite.LayerSetScale(layer, src.Scale);
            _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
            _sprite.LayerSetDirOffset(layer, src.DirOffset);
            ent.Comp.JointPatchKeys.Add(key);
        }
    }

    private void ClearJointPatches(Entity<FootWalkAnimationComponent> ent, SpriteComponent? sprite = null)
    {
        if (ent.Comp.JointPatchKeys.Count == 0)
            return;

        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.JointPatchKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);
        }

        ent.Comp.JointPatchKeys.Clear();
    }

    /// <summary>
    /// On-screen RSI facing - same angle sprites use (worldRotation + eyeRotation).
    /// </summary>
    private RsiDirection GetScreenFacing(EntityUid uid)
    {
        var angle = (_xform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();
        return angle.ToRsiDirection(RsiDirectionType.Dir4);
    }

    private bool HasSlotVisuals(EntityUid uid, string slot)
    {
        return _invSlotsQuery.TryGetComponent(uid, out var slots)
               && slots.VisualLayerKeys.TryGetValue(slot, out var keys)
               && keys.Count > 0;
    }

    private void SetBodyFeetHidden(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite, bool hide)
    {
        if (ent.Comp.BodyFeetHidden == hide)
            return;

        foreach (var layer in new[] { HumanoidVisualLayers.LFoot, HumanoidVisualLayers.RFoot })
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), layer, out var index, false))
                continue;

            _sprite.LayerSetVisible((ent.Owner, sprite), index, !hide);
        }

        // Foot markings ride with the foot layer - hide when covering clothing is on.
        if (_humanoidQuery.TryGetComponent(ent.Owner, out var humanoid))
            SetFootMarkingsVisible((ent.Owner, sprite), humanoid, visible: !hide);

        ent.Comp.BodyFeetHidden = hide;
    }

    private void SetFootMarkingsVisible(
        Entity<SpriteComponent> ent,
        HumanoidAppearanceComponent humanoid,
        bool visible)
    {
        foreach (var part in new[] { HumanoidVisualLayers.LFoot, HumanoidVisualLayers.RFoot })
        {
            var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(part);
            if (!humanoid.MarkingSet.TryGetCategory(category, out var list))
                continue;

            foreach (var marking in list)
            {
                if (!_markings.TryGetMarking(marking, out var proto))
                    continue;

                foreach (var spriteSpec in proto.Sprites)
                {
                    if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                        continue;

                    var key = $"{proto.ID}-{rsi.RsiState}";
                    if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
                        _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
                }
            }
        }
    }

    private void ApplySide(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        HumanoidAppearanceComponent? humanoid,
        HumanoidVisualLayers[] layers,
        Vector2 offset,
        bool skipFeet = false)
    {
        foreach (var layer in layers)
        {
            if (skipFeet && layer is HumanoidVisualLayers.LFoot or HumanoidVisualLayers.RFoot)
                continue;

            SetLayerOffset(ent, walk, layer, offset);
            OffsetMarkingsForPart(ent, walk, humanoid, layer, offset);
        }
    }

    private void OffsetMarkingsForPart(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        HumanoidAppearanceComponent? humanoid,
        HumanoidVisualLayers part,
        Vector2 offset)
    {
        if (humanoid == null)
            return;

        var category = MarkingCategoriesConversion.FromHumanoidVisualLayers(part);
        if (!humanoid.MarkingSet.TryGetCategory(category, out var list))
            return;

        foreach (var marking in list)
        {
            if (!_markings.TryGetMarking(marking, out var proto))
                continue;

            foreach (var spriteSpec in proto.Sprites)
            {
                if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                    continue;

                SetLayerOffset(ent, walk, $"{proto.ID}-{rsi.RsiState}", offset);
            }
        }
    }

    private void ApplySplitHalves(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        List<string> keys,
        float leftY,
        float rightY,
        bool invertSides)
    {
        foreach (var key in keys)
        {
            Vector2 offset;
            if (key.EndsWith(WalkLeftSuffix, StringComparison.Ordinal))
            {
                // South: texture left ≈ RFoot. North: texture left ≈ LFoot.
                offset = new Vector2(0f, invertSides ? leftY : rightY);
            }
            else if (key.EndsWith(WalkRightSuffix, StringComparison.Ordinal))
            {
                offset = new Vector2(0f, invertSides ? rightY : leftY);
            }
            else
                continue;

            SetLayerOffset(ent, walk, key, offset);
        }
    }

    private void ApplyFullSlotOffset(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        string slot,
        float y)
    {
        if (!_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
            return;

        if (!slots.VisualLayerKeys.TryGetValue(slot, out var keys))
            return;

        var offset = new Vector2(0f, y);
        foreach (var key in keys)
        {
            if (key.EndsWith("-displacement", StringComparison.Ordinal))
                continue;

            SetLayerOffset(ent, walk, key, offset);
        }
    }

    private void ApplyBandOffset(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        List<string> keys,
        float y)
    {
        var offset = new Vector2(0f, y);
        foreach (var key in keys)
            SetLayerOffset(ent, walk, key, offset);
    }

    private void EnsureClothingModeIfNeeded(Entity<FootWalkAnimationComponent> ent, bool frontMode, RsiDirection facing)
    {
        var desired = frontMode ? (byte) 1 : (byte) 2;
        var cutChanged = (!float.IsNaN(ent.Comp.AppliedOuterFootCut)
                          && !MathHelper.CloseToPercent(ent.Comp.AppliedOuterFootCut, ent.Comp.OuterFootCut))
                         || (!float.IsNaN(ent.Comp.AppliedJumpsuitHipCut)
                             && !MathHelper.CloseToPercent(ent.Comp.AppliedJumpsuitHipCut, ent.Comp.JumpsuitHipCut));
        var facingChanged = ent.Comp.ClothingFacing != facing;

        // Cut textures are per facing, so a turn rebuilds the clothing layers from the cache.
        if (cutChanged || (facingChanged && ent.Comp.ClothingSplitsActive))
        {
            ClearShoeSplits(ent);
            ClearOuterSplits(ent, clearHole: false);
            ClearOuterSideBands(ent, clearHole: true);
            ClearJumpsuitSplits(ent, clearHole: false);
            ClearJumpsuitSideBands(ent, clearHole: true);
            ClearSleeveCover(ent);
            ent.Comp.ClothingMode = 0;
            ent.Comp.ClothingSplitsActive = false;
        }

        if (facingChanged)
            ent.Comp.ClothingFacing = facing;

        if (!ent.Comp.ClothingSplitsActive)
        {
            EnsureClothingMode(ent, frontMode, facing);
            return;
        }

        if (ent.Comp.ClothingMode == desired)
            return;

        SetClothingModeVisible(ent, frontMode);
        ent.Comp.ClothingMode = desired;
    }

    private void EnsureClothingMode(Entity<FootWalkAnimationComponent> ent, bool frontMode, RsiDirection facing)
    {
        var desired = frontMode ? (byte) 1 : (byte) 2;

        // Every clothing piece is cut on the CPU into plain textures (see RsiCentreProfiles): the
        // visuals match the old clip shaders, but no custom shader layers are created, so the v293
        // renderer cannot draw them as a rectangular patch of neighbouring atlas sprites.
        EnsureShoeSplits(ent, forceRebuild: ent.Comp.ShoeSplitKeys.Count == 0, facing);
        EnsureOuterSplits(ent, forceRebuild: ent.Comp.OuterSplitKeys.Count == 0, facing);
        EnsureOuterSideBands(ent, forceRebuild: ent.Comp.OuterSideBandKeys.Count == 0, facing);
        EnsureJumpsuitSplits(ent, forceRebuild: ent.Comp.JumpsuitSplitKeys.Count == 0, facing);
        EnsureJumpsuitSideBands(ent, forceRebuild: ent.Comp.JumpsuitBandKeys.Count == 0, facing);

        if (_spriteQuery.TryGetComponent(ent.Owner, out var sleeveSprite))
            EnsureSleeveCover(ent, sleeveSprite, facing);

        ent.Comp.ClothingSplitsActive = true;
        ent.Comp.AppliedOuterFootCut = ent.Comp.OuterFootCut;
        ent.Comp.AppliedJumpsuitHipCut = ent.Comp.JumpsuitHipCut;

        if (ent.Comp.ClothingMode == desired)
            return;

        SetClothingModeVisible(ent, frontMode);
        ent.Comp.ClothingMode = desired;
    }

    private void SetClothingModeVisible(Entity<FootWalkAnimationComponent> ent, bool frontMode)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite))
            return;

        // Shoes: front = L/R halves, side = full sprite (X-cut looks wrong on E/W).
        // Art without a centre gap keeps the full sprite in front too, otherwise the cut would tear it.
        foreach (var key in ent.Comp.HiddenShoeKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, !frontMode || IsBandOnly(ent.Comp.BandOnlyShoeSources, key));
        }

        foreach (var key in ent.Comp.ShoeSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, frontMode && !IsBandOnly(ent.Comp.BandOnlyShoeSources, key));
        }

        // Outer: front = X-halves, side = Y-band. Same hole on the base suit.
        foreach (var key in ent.Comp.OuterSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, frontMode && !IsBandOnly(ent.Comp.BandOnlyOuterSources, key));
        }

        foreach (var key in ent.Comp.OuterSideBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, !frontMode || IsBandOnly(ent.Comp.BandOnlyOuterSources, key));
        }

        // Jumpsuit originals (holed torso) stay visible in every mode - the pant halves/band
        // only replace the region below the hip cut, so there is nothing to toggle here.
        foreach (var key in ent.Comp.JumpsuitSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, frontMode && !IsBandOnly(ent.Comp.BandOnlyJumpsuitSources, key));
        }

        foreach (var key in ent.Comp.JumpsuitBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, !frontMode || IsBandOnly(ent.Comp.BandOnlyJumpsuitSources, key));
        }
    }

    /// <summary>
    /// Sleeve layers: a copy of every garment worn over an arm, clipped to that arm's own art, riding
    /// the arm swing. Cloth is opaque over the arm, so lifting the arm out from under a static sleeve
    /// pushed bare skin out along the shoulder on every step. Cutting the sleeve out of the garment does
    /// not work either: the row it vacates at the cuff has nothing behind and would flash a hole each
    /// step. A copy drawn on top of the untouched garment does work - the union of both always covers
    /// the arm, so the sleeve visibly moves with it and no skin can show.
    /// </summary>
    private void EnsureSleeveCover(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite, RsiDirection facing)
    {
        if (ent.Comp.ArmSwingAmplitude <= 0f || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearSleeveCover(ent, sprite);
            return;
        }

        // Count what the build below would create; same conditions, so a built set always matches.
        var wanted = 0;
        foreach (var (slot, limbs) in SleeveSlots)
        {
            if (!TryGetSourceKeys(slots, slot, out var sourceKeys))
                continue;

            foreach (var key in sourceKeys)
            {
                if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false)
                    || !TryGetCutSource(ent.Owner, key, src, out var garmentRsi, out var garmentState))
                    continue;

                foreach (var limb in limbs)
                {
                    if (!TryGetSleeveLimb(ent, sprite, limb, out var limbRsi, out var limbState))
                        continue;

                    if (_centreProfiles.GetSleeveCut(garmentRsi, garmentState, limbRsi, limbState, facing) == null)
                        continue;

                    wanted++;
                }
            }
        }

        if (wanted == ent.Comp.SleeveCoverKeys.Count)
            return;

        ClearSleeveCover(ent, sprite);

        foreach (var (slot, limbs) in SleeveSlots)
        {
            if (!TryGetSourceKeys(slots, slot, out var sourceKeys))
                continue;

            foreach (var key in sourceKeys)
            {
                if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false)
                    || !TryGetCutSource(ent.Owner, key, src, out var garmentRsi, out var garmentState)
                    || !_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                    continue;

                foreach (var limb in limbs)
                {
                    if (!TryGetSleeveLimb(ent, sprite, limb, out var limbRsi, out var limbState))
                        continue;

                    var cut = _centreProfiles.GetSleeveCut(garmentRsi, garmentState, limbRsi, limbState, facing);
                    if (cut == null)
                        continue;

                    var sleeveKey = $"{key}{SleeveSuffix}-{limb}";
                    var sleeve = _sprite.AddBlankLayer((ent.Owner, sprite), srcIndex + 1);
                    _sprite.LayerMapSet((ent.Owner, sprite), sleeveKey, srcIndex + 1);
                    _sprite.LayerSetTexture(sleeve, cut);
                    _sprite.LayerSetColor(sleeve, src.Color);
                    _sprite.LayerSetOffset(sleeve, src.Offset);
                    _sprite.LayerSetScale(sleeve, src.Scale);
                    _sprite.LayerSetVisible(sleeve, false);

                    SetBaseOffset(ent.Comp, sleeveKey, src.Offset);
                    ent.Comp.SleeveCoverKeys[sleeveKey] = limb;
                }
            }
        }
    }

    private bool TryGetSleeveLimb(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        HumanoidVisualLayers limb,
        out RSI rsi,
        out RSI.StateId state)
    {
        rsi = null!;
        state = default;
        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), limb, out var limbIndex, false)
            || !_sprite.TryGetLayer((ent.Owner, sprite), limbIndex, out var limbLayer, false))
            return false;

        return TryGetCutSource(limbLayer, out rsi, out state);
    }

    /// <summary>
    /// Moves every sleeve copy with the arm it covers.
    /// </summary>
    private void ApplySleeveCover(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        float leftArmY,
        float rightArmY)
    {
        foreach (var (key, limb) in ent.Comp.SleeveCoverKeys)
        {
            var lift = limb switch
            {
                HumanoidVisualLayers.LArm => leftArmY,
                HumanoidVisualLayers.RArm => rightArmY,
                _ => 0f,
            };

            SetLayerOffset((ent.Owner, sprite), ent.Comp, key, new Vector2(0f, lift));

            // At rest the copy would only draw the garment on top of itself.
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, lift != 0f);
        }
    }

    private void ClearSleeveCover(Entity<FootWalkAnimationComponent> ent, SpriteComponent? sprite = null)
    {
        if (ent.Comp.SleeveCoverKeys.Count == 0)
            return;

        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.SleeveCoverKeys.Keys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.SleeveCoverKeys.Keys);
        ent.Comp.SleeveCoverKeys.Clear();
    }

    /// <summary>
    /// True when a split or band key belongs to art that must not be cut in half.
    /// </summary>
    private static bool IsBandOnly(HashSet<string> sources, string layerKey)
    {
        foreach (var suffix in new[] { WalkLeftSuffix, WalkRightSuffix, WalkBandSuffix })
        {
            if (layerKey.EndsWith(suffix, StringComparison.Ordinal))
                return sources.Contains(layerKey[..^suffix.Length]);
        }

        return sources.Contains(layerKey);
    }

    private void EnsureShoeSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild, RsiDirection facing)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearShoeSplits(ent);
            return;
        }

        if (!TryGetSourceKeys(slots, ShoesSlot, out var sourceKeys))
        {
            ClearShoeSplits(ent);
            return;
        }

        if (!forceRebuild && SplitsMatch(ent.Comp.ShoeSplitKeys, sourceKeys))
            return;

        ClearShoeSplits(ent, sprite);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            // Track originals; visibility is set by ClothingMode (start hidden until mode applied).
            ent.Comp.HiddenShoeKeys.Add(key);
            SetBaseOffset(ent.Comp, key, src.Offset);

            // Shoes are cut along their whole height, so any art on the centre line would tear.
            var bandOnly = !TryGetCutSource(src, out _, out _);
            if (bandOnly)
                ent.Comp.BandOnlyShoeSources.Add(key);

            var displacementKey = $"{key}-displacement";
            if (slots.VisualLayerKeys[ShoesSlot].Contains(displacementKey)
                && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false))
            {
                ent.Comp.HiddenShoeKeys.Add(displacementKey);

                // The feed layer keeps the same visibility as the sprite it displaces.
                if (bandOnly)
                    ent.Comp.BandOnlyShoeSources.Add(displacementKey);
            }

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var halvesOk = !bandOnly
                && CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkLeftSuffix,
                    keepRight: false,
                    srcIndex + 1,
                    cut: null,
                    facing,
                    ent.Comp.ShoeSplitKeys);

            if (halvesOk)
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                    continue;

                halvesOk = CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkRightSuffix,
                    keepRight: true,
                    srcIndex + 2,
                    cut: null,
                    facing,
                    ent.Comp.ShoeSplitKeys);
            }

            if (!halvesOk)
                ent.Comp.BandOnlyShoeSources.Add(key);
        }

        // Default to current mode if already known, else hide halves until SetClothingModeVisible.
        if (ent.Comp.ClothingMode == 1)
            SetClothingModeVisible(ent, frontMode: true);
        else if (ent.Comp.ClothingMode == 2)
            SetClothingModeVisible(ent, frontMode: false);
        else
        {
            foreach (var key in ent.Comp.ShoeSplitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
            }
        }
    }

    private void EnsureOuterSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild, RsiDirection facing)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearOuterSplits(ent, clearHole: true);
            return;
        }

        if (!TryGetSourceKeys(slots, OuterSlot, out var sourceKeys))
        {
            ClearOuterSplits(ent, clearHole: true);
            return;
        }

        if (!forceRebuild && SplitsMatch(ent.Comp.OuterSplitKeys, sourceKeys))
            return;

        ClearOuterSplits(ent, sprite, clearHole: false);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            var holeOk = EnsureOuterHole(ent, sprite, key, slots, src, facing);

            // A hem that is one piece cannot be cut in half: the halves would pull it apart.
            var bandOnly = !holeOk
                || !TryGetCutSource(ent.Owner, key, src, out _, out _);
            if (bandOnly)
                ent.Comp.BandOnlyOuterSources.Add(key);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var halvesOk = !bandOnly
                && CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkLeftSuffix,
                    keepRight: false,
                    srcIndex + 1,
                    ent.Comp.OuterFootCut,
                    facing,
                    ent.Comp.OuterSplitKeys);

            if (halvesOk)
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                    continue;

                halvesOk = CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkRightSuffix,
                    keepRight: true,
                    srcIndex + 2,
                    ent.Comp.OuterFootCut,
                    facing,
                    ent.Comp.OuterSplitKeys);
            }

            if (!halvesOk)
                ent.Comp.BandOnlyOuterSources.Add(key);
        }

        // Hide until mode selects front.
        if (ent.Comp.ClothingMode != 1)
        {
            foreach (var key in ent.Comp.OuterSplitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
            }
        }
        else
        {
            // Same, for art that stays on the band in both views.
            foreach (var key in ent.Comp.OuterSplitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false)
                    && IsBandOnly(ent.Comp.BandOnlyOuterSources, key))
                {
                    _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
                }
            }
        }
    }

    private void EnsureOuterSideBands(Entity<FootWalkAnimationComponent> ent, bool forceRebuild, RsiDirection facing)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearOuterSideBands(ent, clearHole: true);
            return;
        }

        if (!TryGetSourceKeys(slots, OuterSlot, out var sourceKeys))
        {
            ClearOuterSideBands(ent, clearHole: true);
            return;
        }

        if (!forceRebuild && SideBandsMatch(ent.Comp.OuterSideBandKeys, sourceKeys))
            return;

        ClearOuterSideBands(ent, sprite, clearHole: false);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            if (!EnsureOuterHole(ent, sprite, key, slots, src, facing))
                continue;

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var bandKey = key + WalkBandSuffix;
            var bandTexture = TryGetCutSource(ent.Owner, key, src, out var bandRsi, out var bandState)
                ? _centreProfiles.GetCut(bandRsi, bandState, facing, WalkCutMode.Band, ent.Comp.OuterFootCut)
                : null;
            if (bandTexture == null)
                continue;

            var layer = _sprite.AddBlankLayer((ent.Owner, sprite), srcIndex + 1);
            _sprite.LayerMapSet((ent.Owner, sprite), bandKey, srcIndex + 1);
            _sprite.LayerSetTexture(layer, bandTexture);
            _sprite.LayerSetColor(layer, src.Color);
            _sprite.LayerSetOffset(layer, src.Offset);
            _sprite.LayerSetScale(layer, src.Scale);
            _sprite.LayerSetVisible(layer, ent.Comp.ClothingMode == 2 || IsBandOnly(ent.Comp.BandOnlyOuterSources, bandKey));

            SetBaseOffset(ent.Comp, bandKey, src.Offset);
            ent.Comp.OuterSideBandKeys.Add(bandKey);
        }
    }

    private bool EnsureOuterHole(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        string key,
        InventorySlotsComponent slots,
        Layer src,
        RsiDirection facing)
    {
        if (!ent.Comp.HoledOuterKeys.Contains(key))
        {
            var top = TryGetCutSource(src, out var topRsi, out var topState)
                ? _centreProfiles.GetCut(topRsi, topState, facing, WalkCutMode.Top, ent.Comp.OuterFootCut)
                : null;

            if (top == null)
                return false;

            if (!_holedOriginals.TryGetValue(ent.Owner, out var originals))
            {
                originals = new Dictionary<string, (RSI Rsi, RSI.StateId State)>();
                _holedOriginals[ent.Owner] = originals;
            }

            originals[key] = (src.ActualRsi!, src.State);
            _sprite.LayerSetTexture((ent.Owner, sprite), key, top);
            ent.Comp.HoledOuterKeys.Add(key);
        }

        var displacementKey = $"{key}-displacement";
        if (slots.VisualLayerKeys[OuterSlot].Contains(displacementKey)
            && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false)
            && ent.Comp.HoledOuterKeys.Add(displacementKey))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
        }

        return true;
    }

    private void EnsureJumpsuitSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild, RsiDirection facing)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearJumpsuitSplits(ent);
            return;
        }

        if (!TryGetSourceKeys(slots, JumpsuitSlot, out var sourceKeys))
        {
            ClearJumpsuitSplits(ent);
            return;
        }

        if (!forceRebuild && SplitsMatch(ent.Comp.JumpsuitSplitKeys, sourceKeys))
            return;

        ClearJumpsuitSplits(ent, sprite);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            var holeOk = EnsureJumpsuitHole(ent, sprite, key, slots, src, facing);

            // A fused inseam or a skirt crosses the centre inside the pant region and cannot be
            // X-split; the band moves as one piece instead.
            var bandOnly = !holeOk
                || !TryGetCutSource(ent.Owner, key, src, out _, out _);
            if (bandOnly)
                ent.Comp.BandOnlyJumpsuitSources.Add(key);

            ent.Comp.HiddenJumpsuitKeys.Add(key);
            SetBaseOffset(ent.Comp, key, src.Offset);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var halvesOk = !bandOnly
                && CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkLeftSuffix,
                    keepRight: false,
                    srcIndex + 1,
                    ent.Comp.JumpsuitHipCut,
                    facing,
                    ent.Comp.JumpsuitSplitKeys);

            if (halvesOk)
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                    continue;

                halvesOk = CreateHalfLayer(
                    (ent.Owner, sprite),
                    ent.Comp,
                    src,
                    key,
                    WalkRightSuffix,
                    keepRight: true,
                    srcIndex + 2,
                    ent.Comp.JumpsuitHipCut,
                    facing,
                    ent.Comp.JumpsuitSplitKeys);
            }

            if (!halvesOk)
                ent.Comp.BandOnlyJumpsuitSources.Add(key);
        }

        // Hide until mode selects front (band-only art keeps the holed original instead).
        if (ent.Comp.ClothingMode != 1)
        {
            foreach (var key in ent.Comp.JumpsuitSplitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
            }
        }
        else
        {
            foreach (var key in ent.Comp.JumpsuitSplitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false)
                    && IsBandOnly(ent.Comp.BandOnlyJumpsuitSources, key))
                {
                    _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
                }
            }
        }
    }

    private void EnsureJumpsuitSideBands(Entity<FootWalkAnimationComponent> ent, bool forceRebuild, RsiDirection facing)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearJumpsuitSideBands(ent, clearHole: true);
            return;
        }

        if (!TryGetSourceKeys(slots, JumpsuitSlot, out var sourceKeys))
        {
            ClearJumpsuitSideBands(ent, clearHole: true);
            return;
        }

        if (!forceRebuild && SideBandsMatch(ent.Comp.JumpsuitBandKeys, sourceKeys))
            return;

        ClearJumpsuitSideBands(ent, sprite, clearHole: false);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            if (!EnsureJumpsuitHole(ent, sprite, key, slots, src, facing))
                continue;

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var bandKey = key + WalkBandSuffix;
            var bandTexture = TryGetCutSource(ent.Owner, key, src, out var bandRsi, out var bandState)
                ? _centreProfiles.GetCut(bandRsi, bandState, facing, WalkCutMode.Band, ent.Comp.JumpsuitHipCut)
                : null;
            if (bandTexture == null)
                continue;

            var layer = _sprite.AddBlankLayer((ent.Owner, sprite), srcIndex + 1);
            _sprite.LayerMapSet((ent.Owner, sprite), bandKey, srcIndex + 1);
            _sprite.LayerSetTexture(layer, bandTexture);
            _sprite.LayerSetColor(layer, src.Color);
            _sprite.LayerSetOffset(layer, src.Offset);
            _sprite.LayerSetScale(layer, src.Scale);
            _sprite.LayerSetVisible(layer, ent.Comp.ClothingMode == 2 || IsBandOnly(ent.Comp.BandOnlyJumpsuitSources, bandKey));

            SetBaseOffset(ent.Comp, bandKey, src.Offset);
            ent.Comp.JumpsuitBandKeys.Add(bandKey);
        }
    }

    private bool EnsureJumpsuitHole(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        string key,
        InventorySlotsComponent slots,
        Layer src,
        RsiDirection facing)
    {
        if (!ent.Comp.HoledJumpsuitKeys.Contains(key))
        {
            var top = TryGetCutSource(src, out var topRsi, out var topState)
                ? _centreProfiles.GetCut(topRsi, topState, facing, WalkCutMode.Top, ent.Comp.JumpsuitHipCut)
                : null;

            if (top == null)
                return false;

            if (!_holedOriginals.TryGetValue(ent.Owner, out var originals))
            {
                originals = new Dictionary<string, (RSI Rsi, RSI.StateId State)>();
                _holedOriginals[ent.Owner] = originals;
            }

            originals[key] = (src.ActualRsi!, src.State);
            _sprite.LayerSetTexture((ent.Owner, sprite), key, top);
            ent.Comp.HoledJumpsuitKeys.Add(key);
        }

        var displacementKey = $"{key}-displacement";
        if (slots.VisualLayerKeys[JumpsuitSlot].Contains(displacementKey)
            && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false)
            && ent.Comp.HoledJumpsuitKeys.Add(displacementKey))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
        }

        return true;
    }

    private static bool TryGetSourceKeys(
        InventorySlotsComponent slots,
        string slot,
        out List<string> sourceKeys)
    {
        sourceKeys = new List<string>();
        if (!slots.VisualLayerKeys.TryGetValue(slot, out var keys) || keys.Count == 0)
            return false;

        foreach (var key in keys)
        {
            if (key.EndsWith("-displacement", StringComparison.Ordinal))
                continue;

            sourceKeys.Add(key);
        }

        return sourceKeys.Count > 0;
    }

    private static bool SplitsMatch(List<string> splitKeys, List<string> sourceKeys)
    {
        if (splitKeys.Count != sourceKeys.Count * 2)
            return false;

        foreach (var key in sourceKeys)
        {
            if (!splitKeys.Contains(key + WalkLeftSuffix)
                || !splitKeys.Contains(key + WalkRightSuffix))
                return false;
        }

        return true;
    }

    private static bool SideBandsMatch(List<string> bandKeys, List<string> sourceKeys)
    {
        if (bandKeys.Count != sourceKeys.Count)
            return false;

        foreach (var key in sourceKeys)
        {
            if (!bandKeys.Contains(key + WalkBandSuffix))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a pre-cut half layer for a clothing piece. Returns false when no cut texture is
    /// available: the caller then bobs the whole piece instead (the old band-only fallback).
    /// </summary>
    private bool CreateHalfLayer(
        Entity<SpriteComponent> ent,
        FootWalkAnimationComponent walk,
        Layer src,
        string sourceKey,
        string suffix,
        bool keepRight,
        int insertAt,
        float? cut,
        RsiDirection facing,
        List<string> splitKeys)
    {
        if (!TryGetCutSource(ent.Owner, sourceKey, src, out var rsi, out var state))
            return false;

        var mode = cut == null
            ? (keepRight ? WalkCutMode.HalfR : WalkCutMode.HalfL)
            : (keepRight ? WalkCutMode.BandHalfR : WalkCutMode.BandHalfL);
        var cutTexture = _centreProfiles.GetCut(rsi, state, facing, mode, cut ?? 1f);
        if (cutTexture == null)
            return false;

        var halfKey = sourceKey + suffix;
        var layer = _sprite.AddBlankLayer(ent, insertAt);
        _sprite.LayerMapSet(ent.AsNullable(), halfKey, insertAt);
        _sprite.LayerSetTexture(layer, cutTexture);
        _sprite.LayerSetColor(layer, src.Color);
        _sprite.LayerSetOffset(layer, src.Offset);
        _sprite.LayerSetScale(layer, src.Scale);
        _sprite.LayerSetVisible(layer, true);

        SetBaseOffset(walk, halfKey, src.Offset);
        splitKeys.Add(halfKey);
        return true;
    }

    private void ClearShoeSplits(Entity<FootWalkAnimationComponent> ent, SpriteComponent? sprite = null)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.ShoeSplitKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            foreach (var key in ent.Comp.HiddenShoeKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out _, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), key, true);
            }
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.ShoeSplitKeys);
        ForgetBaseOffsets(ent.Comp, ent.Comp.HiddenShoeKeys);
        ent.Comp.ShoeSplitKeys.Clear();
        ent.Comp.HiddenShoeKeys.Clear();
        ent.Comp.BandOnlyShoeSources.Clear();
    }

    private void ClearOuterSplits(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null,
        bool clearHole = true)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.OuterSplitKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            if (clearHole)
                ClearOuterHoles(ent, sprite);
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.OuterSplitKeys);
        ForgetBaseOffsets(ent.Comp, ent.Comp.HoledOuterKeys);
        ent.Comp.OuterSplitKeys.Clear();
        if (clearHole)
            ent.Comp.HoledOuterKeys.Clear();
        ent.Comp.BandOnlyOuterSources.Clear();
    }

    private void ClearOuterSideBands(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null,
        bool clearHole = true)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.OuterSideBandKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            if (clearHole)
                ClearOuterHoles(ent, sprite);
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.OuterSideBandKeys);
        ent.Comp.OuterSideBandKeys.Clear();
        if (clearHole)
            ent.Comp.HoledOuterKeys.Clear();
    }

    private void ClearOuterHoles(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        RestoreHoledLayers(ent, sprite, ent.Comp.HoledOuterKeys);
    }

    private void ClearJumpsuitSplits(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null,
        bool clearHole = true)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.JumpsuitSplitKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            foreach (var key in ent.Comp.HiddenJumpsuitKeys)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out _, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), key, true);
            }

            if (clearHole)
                ClearJumpsuitHoles(ent, sprite);
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.JumpsuitSplitKeys);
        ForgetBaseOffsets(ent.Comp, ent.Comp.HiddenJumpsuitKeys);
        ForgetBaseOffsets(ent.Comp, ent.Comp.HoledJumpsuitKeys);
        ent.Comp.JumpsuitSplitKeys.Clear();
        ent.Comp.HiddenJumpsuitKeys.Clear();
        ent.Comp.BandOnlyJumpsuitSources.Clear();
        if (clearHole)
            ent.Comp.HoledJumpsuitKeys.Clear();
    }

    private void ClearJumpsuitSideBands(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null,
        bool clearHole = true)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.JumpsuitBandKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            if (clearHole)
                ClearJumpsuitHoles(ent, sprite);
        }

        ForgetBaseOffsets(ent.Comp, ent.Comp.JumpsuitBandKeys);
        ent.Comp.JumpsuitBandKeys.Clear();
        if (clearHole)
            ent.Comp.HoledJumpsuitKeys.Clear();
    }

    private void ClearJumpsuitHoles(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        RestoreHoledLayers(ent, sprite, ent.Comp.HoledJumpsuitKeys);
    }

    /// <summary>
    /// Returns holed layers to their original RSI states and makes them visible again.
    /// </summary>
    private void RestoreHoledLayers(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        HashSet<string> keys)
    {
        var originals = _holedOriginals.TryGetValue(ent.Owner, out var stored) ? stored : null;

        foreach (var key in keys)
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                continue;

            _sprite.LayerSetVisible((ent.Owner, sprite), index, true);

            if (originals != null && originals.TryGetValue(key, out var original))
                _sprite.LayerSetRsi((ent.Owner, sprite), index, original.Rsi, original.State);
        }

        if (originals == null)
            return;

        foreach (var key in keys)
            originals.Remove(key);

        if (originals.Count == 0)
            _holedOriginals.Remove(ent.Owner);
    }

    private void SetLayerOffset(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        Enum layerKey,
        Vector2 offset)
    {
        if (!_sprite.LayerMapTryGet(ent, layerKey, out var index, false))
            return;

        _sprite.LayerSetOffset(ent, index, offset);
        walk.TouchedEnumLayers.Add(layerKey);
    }

    private void SetLayerOffset(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        string layerKey,
        Vector2 offset)
    {
        if (!_sprite.LayerMapTryGet(ent, layerKey, out var index, false))
            return;

        // Added on top of the layer's own offset: several items sit offset from the sprite origin
        // (clown shoes, roller skates) and would jump the moment the mob takes a step.
        _sprite.LayerSetOffset(ent, index, walk.BaseOffsets.GetValueOrDefault(layerKey) + offset);
        walk.TouchedStringLayers.Add(layerKey);
    }

    private static void SetBaseOffset(FootWalkAnimationComponent walk, string layerKey, Vector2 offset)
    {
        walk.BaseOffsets[layerKey] = offset;
    }

    /// <summary>
    /// Drops remembered offsets for layers that are going away. Keys are derived from RSI states,
    /// so a different item reusing the same key must not inherit the previous item's offset.
    /// </summary>
    private static void ForgetBaseOffsets(FootWalkAnimationComponent walk, IEnumerable<string> layerKeys)
    {
        foreach (var key in layerKeys)
        {
            walk.BaseOffsets.Remove(key);
        }
    }

    private float GetStepRate(EntityUid uid, FootWalkAnimationComponent walk, float speed)
    {
        var sprinting = _moverQuery.TryGetComponent(uid, out var mover) && mover.Sprinting;
        var baseRate = sprinting ? walk.SprintRate : walk.WalkRate;

        float expected;
        if (_moveSpeedQuery.TryGetComponent(uid, out var moveSpeed))
        {
            expected = sprinting
                ? Math.Max(moveSpeed.CurrentSprintSpeed, 0.1f)
                : Math.Max(moveSpeed.CurrentWalkSpeed, 0.1f);
        }
        else
        {
            expected = sprinting ? 4.5f : 2.5f;
        }

        var slowFactor = Math.Clamp(speed / expected, walk.MinSlowFactor, walk.MaxSlowFactor);
        return baseRate * slowFactor;
    }

    private void DisableAllAnimations()
    {
        var query = EntityQueryEnumerator<FootWalkAnimationComponent>();
        while (query.MoveNext(out var uid, out var walk))
        {
            if (_spriteQuery.TryGetComponent(uid, out var sprite))
            {
                SetBodyFeetHidden((uid, walk), sprite, hide: false);
                ResetLowerBody((uid, walk), sprite);
                ClearJointPatches((uid, walk), sprite);
                StopBodyBounce((uid, walk), sprite);
            }

            ClearClothingWalkLayers((uid, walk));

            walk.WasAnimating = false;
            walk.Phase = 0f;
        }
    }

    private bool CanAnimate(EntityUid uid)
    {
        if (_borgQuery.HasComp(uid))
            return false;

        // Clown/jester shoes already play WaddleAnimation - skip foot bob.
        if (_waddleQuery.HasComp(uid))
            return false;

        if (_mobQuery.TryGetComponent(uid, out var mob) && !_mobState.IsAlive(uid, mob))
            return false;

        // No ground contact in zero-G - nothing to push off.
        if (_gravity.IsWeightless(uid))
            return false;

        return !_standing.IsDown(uid);
    }

    private void ClearClothingWalkLayers(Entity<FootWalkAnimationComponent> ent)
    {
        // Patches duplicate the limb art and go stale on any appearance change; rebuilt next frame.
        ClearJointPatches(ent);
        ClearSleeveCover(ent);

        if (!ent.Comp.ClothingSplitsActive
            && ent.Comp.ShoeSplitKeys.Count == 0
            && ent.Comp.OuterSplitKeys.Count == 0
            && ent.Comp.OuterSideBandKeys.Count == 0
            && ent.Comp.JumpsuitSplitKeys.Count == 0
            && ent.Comp.JumpsuitBandKeys.Count == 0)
        {
            if (_spriteQuery.TryGetComponent(ent.Owner, out var idleSprite))
                SetBodyFeetHidden(ent, idleSprite, hide: false);
            return;
        }

        ClearShoeSplits(ent);
        ClearOuterSplits(ent, clearHole: false);
        ClearOuterSideBands(ent, clearHole: true);
        ClearJumpsuitSplits(ent, clearHole: false);
        ClearJumpsuitSideBands(ent, clearHole: true);
        ent.Comp.ClothingSplitsActive = false;
        ent.Comp.ClothingMode = 0;
        ent.Comp.AppliedOuterFootCut = float.NaN;
        ent.Comp.AppliedJumpsuitHipCut = float.NaN;

        if (_spriteQuery.TryGetComponent(ent.Owner, out var sprite))
            SetBodyFeetHidden(ent, sprite, hide: false);
    }

    private bool HasLowerBodyVisuals(EntityUid uid, SpriteComponent sprite)
    {
        foreach (var layer in LeftLayers)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out _, false))
                return true;
        }

        foreach (var layer in RightLayers)
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out _, false))
                return true;
        }

        return false;
    }

    /// <summary>
    /// RSI and state of a clothing layer when it can be cut reliably: a plain texture or an animated
    /// state cannot (its frames are not a static per-facing image).
    /// </summary>
    private static bool TryGetCutSource(Layer src, out RSI rsi, out RSI.StateId state)
    {
        state = src.State;
        rsi = src.ActualRsi!;
        return rsi != null
               && rsi.TryGetState(state, out var rsiState)
               && !rsiState.IsAnimated;
    }

    /// <summary>
    /// Same, but for a layer whose lower band was already replaced with a cut texture: the texture
    /// write invalidates the layer state, so the original RSI and state are taken from the hole cache.
    /// </summary>
    private bool TryGetCutSource(EntityUid owner, string key, Layer src, out RSI rsi, out RSI.StateId state)
    {
        if (_holedOriginals.TryGetValue(owner, out var originals)
            && originals.TryGetValue(key, out var original))
        {
            rsi = original.Rsi;
            state = original.State;
            return rsi.TryGetState(state, out var holedState) && !holedState.IsAnimated;
        }

        return TryGetCutSource(src, out rsi, out state);
    }

    /// <summary>
    /// Restore only layers touched last tick, then clear the touch sets for this tick's Apply*.
    /// </summary>
    private void ResetTouchedOffsets(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        foreach (var key in ent.Comp.TouchedEnumLayers)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }

        foreach (var key in ent.Comp.TouchedStringLayers)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }

        ent.Comp.TouchedEnumLayers.Clear();
        ent.Comp.TouchedStringLayers.Clear();
    }

    private void ResetLowerBody(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null)
    {
        if (sprite == null && !_spriteQuery.TryGetComponent(ent.Owner, out sprite))
        {
            ent.Comp.TouchedEnumLayers.Clear();
            ent.Comp.TouchedStringLayers.Clear();
            return;
        }

        ResetTouchedOffsets(ent, sprite);

        foreach (var layer in LeftLayers)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), layer, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }

        foreach (var layer in RightLayers)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), layer, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }

        foreach (var key in ent.Comp.ShoeSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }

        foreach (var key in ent.Comp.OuterSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }

        foreach (var key in ent.Comp.OuterSideBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }

        foreach (var key in ent.Comp.JumpsuitSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }

        foreach (var key in ent.Comp.JumpsuitBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, ent.Comp.BaseOffsets.GetValueOrDefault(key));
        }
    }
}
