// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
    private static readonly ProtoId<ShaderPrototype> HalfClipShader = "SpriteHalfClip";
    private static readonly ProtoId<ShaderPrototype> FootHalfClipShader = "SpriteFootHalfClip";
    private static readonly ProtoId<ShaderPrototype> FootHoleShader = "SpriteFootHole";
    private static readonly ProtoId<ShaderPrototype> FootBandShader = "SpriteFootBand";
    private static readonly ProtoId<ShaderPrototype> PantHalfClipShader = "SpritePantHalfClip";

    private const string ShoesSlot = "shoes";
    private const string OuterSlot = "outerClothing";
    private const string JumpsuitSlot = "jumpsuit";
    private const string WalkLeftSuffix = "-walk-L";
    private const string WalkRightSuffix = "-walk-R";
    private const string WalkBandSuffix = "-walk-band";

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

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
    /// Cache for <see cref="ArtCrossesCentre"/>, keyed by RSI path, state name and cut height.
    /// Reading frame pixels is a GPU readback, so each garment art is inspected once per session.
    /// </summary>
    private readonly Dictionary<(string Rsi, string State, float Cut), bool> _crossesCentreCache = new();

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
        HumanoidVisualLayers.LHand,
    ];

    private static readonly HumanoidVisualLayers[] RightArmLayers =
    [
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.RHand,
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

            // Mini fix: velocity relative to whatever the mob stands on (grid/shuttle). World
            // velocity played the walk bob on a standing player inside a moving shuttle and
            // cancelled it when walking against the shuttle's motion.
            var velocity = physics.LinearVelocity;
            var gridUid = Transform(uid).GridUid;
            if (gridUid is { } grid && grid != uid
                && _physicsQuery.TryGetComponent(grid, out var gridPhysics))
            {
                velocity -= gridPhysics.LinearVelocity;
            }

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
            EnsureClothingModeIfNeeded((uid, walk), frontMode);
            EnsureJointPatches((uid, walk), sprite);

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
            // layer rides it, feet included — like the baked walk frames in Stardew. Every other
            // offset below is a pure lift (>= 0), so parts can only overlap more, never separate:
            // no slits can open between limbs, naked or clothed.
            var bodyY = 0f;
            if (_bodyBounce && walk.BodyBounceAmplitude > 0f)
            {
                bodyY = MathF.Abs(MathF.Sin(walk.Phase)) * walk.BodyBounceAmplitude
                        * (sprinting ? walk.SprintBounceFactor : 1f) * walk.BobRamp;
                EnsureBodyBounce((uid, walk), sprite);
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

            // Only undo last tick's offsets — do not re-zero every lower-body layer.
            ResetTouchedOffsets((uid, walk), sprite);

            _humanoidQuery.TryGetComponent(uid, out var humanoid);

            if (frontMode)
            {
                // South: sprite left ≈ RFoot. North mirrors L/R on the sheet (clothing only).
                // Body layers stay anatomical L/R — never invert with the camera sheet.
                var invert = facing == RsiDirection.North;

                ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY), skipFeet: hasShoes || hasOuter);
                ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY), skipFeet: hasShoes || hasOuter);

                ApplySplitHalves((uid, sprite), walk, walk.ShoeSplitKeys, leftY, rightY, invert);

                // Pant halves ride their leg exactly; lifts are never negative, so a half can
                // only slide up over the holed torso, never open a slit at the hip.
                ApplySplitHalves((uid, sprite), walk, walk.JumpsuitSplitKeys, leftY, rightY, invert);

                // Footwear and garments that cannot be split (their art has no centre gap) stay on
                // the full sprite or band: one piece, bouncing on each footfall instead of shearing.
                var singlePieceY = MathF.Max(leftY, rightY);
                ApplyFullSlotOffset((uid, sprite), walk, ShoesSlot, singlePieceY);
                ApplyBandOffset((uid, sprite), walk, walk.OuterSideBandKeys, singlePieceY);
                ApplyBandOffset((uid, sprite), walk, walk.JumpsuitBandKeys, singlePieceY);
            }
            else
            {
                // Side: one silhouette — clothing lifts with near foot; don't alternate bare feet under boots.
                if (hasOuter)
                {
                    // Full suit: legs must move with the boot band or flesh peeks through the hole.
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, nearY), skipFeet: true);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, nearY), skipFeet: true);
                }
                else if (hasJumpsuit)
                {
                    // Jumpsuit: the pant band rides the near leg, so both legs must follow it —
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

                ApplyBandOffset((uid, sprite), walk, walk.JumpsuitBandKeys, nearY);
            }

            if (walk.ArmSwingAmplitude > 0f)
            {
                ApplySide((uid, sprite), walk, humanoid, LeftArmLayers, new Vector2(0f, leftArmY));
                ApplySide((uid, sprite), walk, humanoid, RightArmLayers, new Vector2(0f, rightArmY));
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
    /// When a limb lifts, its vacated pixels are filled by the patch with the same art — the
    /// "root" of the limb stays put while the limb itself rises, so no slit can open at the
    /// joint (this is the closest a dynamic sprite rig gets to Stardew's baked overlap).
    /// </summary>
    private void EnsureJointPatches(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.JointPatchKeys.Count > 0)
            return;

        // Below the groin: the patch only shows where every upper layer is empty — exactly the
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
    /// On-screen RSI facing — same angle sprites use (worldRotation + eyeRotation).
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

        // Foot markings ride with the foot layer — hide when covering clothing is on.
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

    private void EnsureClothingModeIfNeeded(Entity<FootWalkAnimationComponent> ent, bool frontMode)
    {
        var desired = frontMode ? (byte) 1 : (byte) 2;
        var cutChanged = (!float.IsNaN(ent.Comp.AppliedOuterFootCut)
                          && !MathHelper.CloseToPercent(ent.Comp.AppliedOuterFootCut, ent.Comp.OuterFootCut))
                         || (!float.IsNaN(ent.Comp.AppliedJumpsuitHipCut)
                             && !MathHelper.CloseToPercent(ent.Comp.AppliedJumpsuitHipCut, ent.Comp.JumpsuitHipCut));

        if (cutChanged)
        {
            ClearOuterSplits(ent, clearHole: false);
            ClearOuterSideBands(ent, clearHole: true);
            ClearJumpsuitSplits(ent, clearHole: false);
            ClearJumpsuitSideBands(ent, clearHole: true);
            ent.Comp.ClothingMode = 0;
            ent.Comp.ClothingSplitsActive = false;
        }

        if (!ent.Comp.ClothingSplitsActive)
        {
            EnsureClothingMode(ent, frontMode);
            return;
        }

        if (ent.Comp.ClothingMode == desired)
            return;

        SetClothingModeVisible(ent, frontMode);
        ent.Comp.ClothingMode = desired;
    }

    private void EnsureClothingMode(Entity<FootWalkAnimationComponent> ent, bool frontMode)
    {
        var desired = frontMode ? (byte) 1 : (byte) 2;

        // Build both front halves and side bands once; facing only toggles visibility.
        EnsureShoeSplits(ent, forceRebuild: ent.Comp.ShoeSplitKeys.Count == 0);
        EnsureOuterSplits(ent, forceRebuild: ent.Comp.OuterSplitKeys.Count == 0);
        EnsureOuterSideBands(ent, forceRebuild: ent.Comp.OuterSideBandKeys.Count == 0);
        EnsureJumpsuitSplits(ent, forceRebuild: ent.Comp.JumpsuitSplitKeys.Count == 0);
        EnsureJumpsuitSideBands(ent, forceRebuild: ent.Comp.JumpsuitBandKeys.Count == 0);
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

        // Jumpsuit originals (holed torso) stay visible in every mode — the pant halves/band
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

    private void EnsureShoeSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
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
            var bandOnly = ArtCrossesCentre(src.ActualRsi, src.State, footCut: null);
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

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkLeftSuffix,
                keepRight: false,
                srcIndex + 1,
                HalfClipShader,
                ent.Comp.ShoeSplitKeys,
                footCut: null);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                continue;

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkRightSuffix,
                keepRight: true,
                srcIndex + 2,
                HalfClipShader,
                ent.Comp.ShoeSplitKeys,
                footCut: null);
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

    private void EnsureOuterSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
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

            EnsureOuterHole(ent, sprite, key, slots);

            // A hem that is one piece cannot be cut in half: the halves would pull it apart.
            if (ArtCrossesCentre(src.ActualRsi, src.State, ent.Comp.OuterFootCut))
                ent.Comp.BandOnlyOuterSources.Add(key);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkLeftSuffix,
                keepRight: false,
                srcIndex + 1,
                FootHalfClipShader,
                ent.Comp.OuterSplitKeys,
                ent.Comp.OuterFootCut);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                continue;

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkRightSuffix,
                keepRight: true,
                srcIndex + 2,
                FootHalfClipShader,
                ent.Comp.OuterSplitKeys,
                ent.Comp.OuterFootCut);
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

    private void EnsureOuterSideBands(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
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

            EnsureOuterHole(ent, sprite, key, slots);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var bandKey = key + WalkBandSuffix;
            var layer = _sprite.AddBlankLayer((ent.Owner, sprite), srcIndex + 1);
            _sprite.LayerMapSet((ent.Owner, sprite), bandKey, srcIndex + 1);

            var rsi = src.ActualRsi;
            if (rsi != null)
                _sprite.LayerSetRsi(layer, rsi, src.State);
            else if (src.Texture != null)
                _sprite.LayerSetTexture(layer, src.Texture);

            _sprite.LayerSetColor(layer, src.Color);
            _sprite.LayerSetOffset(layer, src.Offset);
            _sprite.LayerSetScale(layer, src.Scale);
            _sprite.LayerSetVisible(layer, ent.Comp.ClothingMode == 2 || IsBandOnly(ent.Comp.BandOnlyOuterSources, bandKey));
            _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
            _sprite.LayerSetDirOffset(layer, src.DirOffset);

            var shader = _prototypes.Index(FootBandShader).InstanceUnique();
            shader.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(bandKey, shader, FootBandShader.Id);
            SetBaseOffset(ent.Comp, bandKey, src.Offset);
            ent.Comp.OuterSideBandKeys.Add(bandKey);
        }
    }

    private void EnsureOuterHole(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        string key,
        InventorySlotsComponent slots)
    {
        if (!ent.Comp.HoledOuterKeys.Contains(key))
        {
            var hole = _prototypes.Index(FootHoleShader).InstanceUnique();
            hole.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(key, hole, FootHoleShader.Id);
            ent.Comp.HoledOuterKeys.Add(key);
        }

        var displacementKey = $"{key}-displacement";
        if (slots.VisualLayerKeys[OuterSlot].Contains(displacementKey)
            && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false)
            && ent.Comp.HoledOuterKeys.Add(displacementKey))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
        }
    }

    private void EnsureJumpsuitSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
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

            EnsureJumpsuitHole(ent, sprite, key, slots);

            // A fused inseam or a skirt crosses the centre inside the pant region and cannot be
            // X-split; the band moves as one piece instead.
            if (ArtCrossesCentre(src.ActualRsi, src.State, ent.Comp.JumpsuitHipCut))
                ent.Comp.BandOnlyJumpsuitSources.Add(key);

            ent.Comp.HiddenJumpsuitKeys.Add(key);
            SetBaseOffset(ent.Comp, key, src.Offset);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkLeftSuffix,
                keepRight: false,
                srcIndex + 1,
                PantHalfClipShader,
                ent.Comp.JumpsuitSplitKeys,
                ent.Comp.JumpsuitHipCut);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out srcIndex, false))
                continue;

            CreateHalfLayer(
                (ent.Owner, sprite),
                ent.Comp,
                src,
                key,
                WalkRightSuffix,
                keepRight: true,
                srcIndex + 2,
                PantHalfClipShader,
                ent.Comp.JumpsuitSplitKeys,
                ent.Comp.JumpsuitHipCut);
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

    private void EnsureJumpsuitSideBands(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
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

            EnsureJumpsuitHole(ent, sprite, key, slots);

            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var srcIndex, false))
                continue;

            var bandKey = key + WalkBandSuffix;
            var layer = _sprite.AddBlankLayer((ent.Owner, sprite), srcIndex + 1);
            _sprite.LayerMapSet((ent.Owner, sprite), bandKey, srcIndex + 1);

            var rsi = src.ActualRsi;
            if (rsi != null)
                _sprite.LayerSetRsi(layer, rsi, src.State);
            else if (src.Texture != null)
                _sprite.LayerSetTexture(layer, src.Texture);

            _sprite.LayerSetColor(layer, src.Color);
            _sprite.LayerSetOffset(layer, src.Offset);
            _sprite.LayerSetScale(layer, src.Scale);
            _sprite.LayerSetVisible(layer, ent.Comp.ClothingMode == 2 || IsBandOnly(ent.Comp.BandOnlyJumpsuitSources, bandKey));
            _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
            _sprite.LayerSetDirOffset(layer, src.DirOffset);

            var shader = _prototypes.Index(FootBandShader).InstanceUnique();
            shader.SetParameter("footCut", ent.Comp.JumpsuitHipCut);
            sprite.LayerSetShader(bandKey, shader, FootBandShader.Id);
            SetBaseOffset(ent.Comp, bandKey, src.Offset);
            ent.Comp.JumpsuitBandKeys.Add(bandKey);
        }
    }

    private void EnsureJumpsuitHole(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent sprite,
        string key,
        InventorySlotsComponent slots)
    {
        if (!ent.Comp.HoledJumpsuitKeys.Contains(key))
        {
            var hole = _prototypes.Index(FootHoleShader).InstanceUnique();
            hole.SetParameter("footCut", ent.Comp.JumpsuitHipCut);
            sprite.LayerSetShader(key, hole, FootHoleShader.Id);
            ent.Comp.HoledJumpsuitKeys.Add(key);
        }

        var displacementKey = $"{key}-displacement";
        if (slots.VisualLayerKeys[JumpsuitSlot].Contains(displacementKey)
            && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false)
            && ent.Comp.HoledJumpsuitKeys.Add(displacementKey))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
        }
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

    private void CreateHalfLayer(
        Entity<SpriteComponent> ent,
        FootWalkAnimationComponent walk,
        Layer src,
        string sourceKey,
        string suffix,
        bool keepRight,
        int insertAt,
        ProtoId<ShaderPrototype> shaderId,
        List<string> splitKeys,
        float? footCut)
    {
        var halfKey = sourceKey + suffix;
        var layer = _sprite.AddBlankLayer(ent, insertAt);
        _sprite.LayerMapSet(ent.AsNullable(), halfKey, insertAt);

        var rsi = src.ActualRsi;
        if (rsi != null)
            _sprite.LayerSetRsi(layer, rsi, src.State);
        else if (src.Texture != null)
            _sprite.LayerSetTexture(layer, src.Texture);

        _sprite.LayerSetColor(layer, src.Color);
        _sprite.LayerSetOffset(layer, src.Offset);
        _sprite.LayerSetScale(layer, src.Scale);
        _sprite.LayerSetVisible(layer, true);
        _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
        _sprite.LayerSetDirOffset(layer, src.DirOffset);

        var shader = _prototypes.Index(shaderId).InstanceUnique();
        shader.SetParameter("keepRight", keepRight ? 1f : 0f);
        if (footCut != null)
            shader.SetParameter("footCut", footCut.Value);

        ent.Comp.LayerSetShader(halfKey, shader, shaderId.Id);
        SetBaseOffset(walk, halfKey, src.Offset);
        splitKeys.Add(halfKey);
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
        foreach (var key in ent.Comp.HoledOuterKeys)
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                continue;

            _sprite.LayerSetVisible((ent.Owner, sprite), index, true);
            sprite.LayerSetShader(index, shader: null, prototype: null);
        }
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
        foreach (var key in ent.Comp.HoledJumpsuitKeys)
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                continue;

            _sprite.LayerSetVisible((ent.Owner, sprite), index, true);
            sprite.LayerSetShader(index, shader: null, prototype: null);
        }
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

        // Clown/jester shoes already play WaddleAnimation — skip foot bob.
        if (_waddleQuery.HasComp(uid))
            return false;

        if (_mobQuery.TryGetComponent(uid, out var mob) && !_mobState.IsAlive(uid, mob))
            return false;

        // No ground contact in zero-G — nothing to push off.
        if (_gravity.IsWeightless(uid))
            return false;

        return !_standing.IsDown(uid);
    }

    private void ClearClothingWalkLayers(Entity<FootWalkAnimationComponent> ent)
    {
        // Patches duplicate the limb art and go stale on any appearance change; rebuilt next frame.
        ClearJointPatches(ent);

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
    /// True when the layer art crosses the horizontal centre of the sprite, meaning a left/right cut
    /// with the halves moving in antiphase would shear the art open. That is the case for long
    /// garments whose hem is a single piece (coats, robes, aprons) and for the small species whose
    /// legs sit on the centre line.
    /// </summary>
    /// <param name="footCut">
    /// Height of the bottom band to inspect, or null to inspect the whole frame (shoes are cut along
    /// their full height, outer clothing only along its foot band).
    /// </param>
    private bool ArtCrossesCentre(RSI? rsi, RSI.StateId state, float? footCut)
    {
        if (rsi == null)
            return false;

        var cut = footCut ?? 1f;
        var key = (rsi.Path.ToString(), state.Name ?? string.Empty, cut);
        if (_crossesCentreCache.TryGetValue(key, out var cached))
            return cached;

        var result = false;
        try
        {
            if (rsi.TryGetState(state, out var rsiState))
            {
                if (rsiState.RsiDirections == RsiDirectionType.Dir1)
                {
                    result = CrossesCentre(rsiState.Frame0, cut);
                }
                else
                {
                    // The cut hits both front views, so either one having no gap is enough.
                    result = CrossesCentre(rsiState.GetFrame(RsiDirection.South, 0), cut)
                             || CrossesCentre(rsiState.GetFrame(RsiDirection.North, 0), cut);
                }
            }
        }
        catch (Exception e)
        {
            // Pixel reads go through the GPU; when that fails, keep the plain left/right split.
            Log.Debug($"Foot walk: cannot inspect {rsi.Path} state {state}: {e.Message}");
        }

        _crossesCentreCache[key] = result;
        return result;
    }

    private static bool CrossesCentre(Texture texture, float footCut)
    {
        var width = texture.Width;
        var height = texture.Height;
        if (width < 4 || height < 2)
            return false;

        var left = width / 2 - 1;
        var right = width / 2;
        var from = Math.Max(0, (int) (height * (1f - footCut)));

        // y = 0 is the top of the frame, so the foot band is the tail of the frame. Walking up from
        // the bottom both starts where the feet are and stops at the first crossing found.
        for (var y = height - 1; y >= from; y--)
        {
            if (SampleAlpha(texture, left, y) <= 0.05f)
                continue;

            if (SampleAlpha(texture, right, y) > 0.05f)
                return true;
        }

        return false;
    }

    private static float SampleAlpha(Texture texture, int x, int y)
    {
        // AtlasTexture.GetPixel asserts against the frame's absolute top edge instead of its own
        // height, which fails for every row of a frame sitting in the first sheet row, so read
        // through the source texture with the frame offset applied.
        if (texture is AtlasTexture atlas)
        {
            var source = atlas.SourceTexture;
            return source.GetPixel(x + (int) atlas.SubRegion.Left, y + (int) atlas.SubRegion.Top).A;
        }

        return texture.GetPixel(x, y).A;
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
