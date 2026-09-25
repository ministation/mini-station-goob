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
/// </summary>
public sealed partial class FootWalkAnimationSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> HalfClipShader = "SpriteHalfClip";
    private static readonly ProtoId<ShaderPrototype> FootHalfClipShader = "SpriteFootHalfClip";
    private static readonly ProtoId<ShaderPrototype> FootHoleShader = "SpriteFootHole";
    private static readonly ProtoId<ShaderPrototype> FootBandShader = "SpriteFootBand";

    private const string ShoesSlot = "shoes";
    private const string OuterSlot = "outerClothing";
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

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<BorgChassisComponent> _borgQuery;
    private EntityQuery<WaddleAnimationComponent> _waddleQuery;
    private EntityQuery<InputMoverComponent> _moverQuery;
    private EntityQuery<MovementSpeedModifierComponent> _moveSpeedQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;
    private EntityQuery<InventorySlotsComponent> _invSlotsQuery;

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

        SubscribeLocalEvent<FootWalkAnimationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FootWalkAnimationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FootWalkAnimationComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<FootWalkAnimationComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<FootWalkAnimationComponent, VisualsChangedEvent>(OnVisualsChanged,
            after: [typeof(ClientClothingSystem)]);
    }

    private void OnStartup(Entity<FootWalkAnimationComponent> ent, ref ComponentStartup args)
    {
        // Facing-aware splits are applied in FrameUpdate.
    }

    private void OnShutdown(Entity<FootWalkAnimationComponent> ent, ref ComponentShutdown args)
    {
        if (_spriteQuery.TryGetComponent(ent.Owner, out var sprite))
            SetBodyFeetHidden(ent, sprite, hide: false);

        ResetLowerBody(ent);
        ClearClothingWalkLayers(ent);
        ent.Comp.WasAnimating = false;
    }

    private void OnDidEquip(Entity<FootWalkAnimationComponent> ent, ref DidEquipEvent args)
    {
        if (!_enabled || args.Slot is not (ShoesSlot or OuterSlot))
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
    }

    private void OnVisualsChanged(Entity<FootWalkAnimationComponent> ent, ref VisualsChangedEvent args)
    {
        if (!_enabled || args.ContainerId is not (ShoesSlot or OuterSlot))
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

            // Cheap reject for the common idle case before CanAnimate / gravity checks.
            if (!_physicsQuery.TryGetComponent(uid, out var physics)
                || physics.LinearVelocity.LengthSquared() < walk.MinSpeedSquared)
            {
                StopAnimating((uid, walk), sprite);
                continue;
            }

            if (!CanAnimate(uid) || !HasLowerBodyVisuals(uid, sprite))
            {
                StopAnimating((uid, walk), sprite);
                continue;
            }

            walk.WasAnimating = true;

            // Must match sprite RSI direction (world + eye), otherwise camera turns invert bob / wrong mode.
            var facing = GetScreenFacing(uid);
            var frontMode = facing is RsiDirection.South or RsiDirection.North;
            EnsureClothingModeIfNeeded((uid, walk), frontMode);

            var hasShoes = HasSlotVisuals(uid, ShoesSlot);
            var hasOuter = HasSlotVisuals(uid, OuterSlot);
            // Never show bare feet under shoes or a hardsuit boot band/hole.
            SetBodyFeetHidden((uid, walk), sprite, hide: hasShoes || hasOuter);

            var speed = physics.LinearVelocity.Length();
            walk.Phase += frameTime * walk.CycleSpeed * GetStepRate(uid, walk, speed);

            var leftAmp = walk.Amplitude;
            var rightAmp = walk.Amplitude;

            // Far foot still bobs a bit on side view so it doesn't look like a one-leg hop.
            if (facing == RsiDirection.East)
                leftAmp *= walk.SideFarAmplitudeFactor;
            else if (facing == RsiDirection.West)
                rightAmp *= walk.SideFarAmplitudeFactor;

            var leftY = MathF.Max(0f, MathF.Sin(walk.Phase)) * leftAmp;
            var rightY = MathF.Max(0f, MathF.Sin(walk.Phase + MathF.PI)) * rightAmp;

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
                ApplySplitHalves((uid, sprite), walk, walk.OuterSplitKeys, leftY, rightY, invert);
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
                    ApplySideBandOffset((uid, sprite), walk, nearY);
            }
        }
    }

    private void StopAnimating(Entity<FootWalkAnimationComponent> ent, SpriteComponent sprite)
    {
        if (!ent.Comp.WasAnimating)
            return;

        ClearClothingWalkLayers(ent);
        SetBodyFeetHidden(ent, sprite, hide: false);
        ResetLowerBody(ent, sprite);
        ent.Comp.WasAnimating = false;
        ent.Comp.Phase = 0f;
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

    private void ApplySideBandOffset(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        float y)
    {
        var offset = new Vector2(0f, y);
        foreach (var key in walk.OuterSideBandKeys)
            SetLayerOffset(ent, walk, key, offset);
    }

    private void EnsureClothingModeIfNeeded(Entity<FootWalkAnimationComponent> ent, bool frontMode)
    {
        var desired = frontMode ? (byte) 1 : (byte) 2;
        var cutChanged = !float.IsNaN(ent.Comp.AppliedOuterFootCut)
                         && !MathHelper.CloseToPercent(ent.Comp.AppliedOuterFootCut, ent.Comp.OuterFootCut);

        if (cutChanged)
        {
            ClearOuterSplits(ent, clearHole: false);
            ClearOuterSideBands(ent, clearHole: true);
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
        ent.Comp.ClothingSplitsActive = true;
        ent.Comp.AppliedOuterFootCut = ent.Comp.OuterFootCut;

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
        foreach (var key in ent.Comp.HiddenShoeKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, !frontMode);
        }

        foreach (var key in ent.Comp.ShoeSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, frontMode);
        }

        // Outer: front = X-halves, side = Y-band. Same hole on the base suit.
        foreach (var key in ent.Comp.OuterSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, frontMode);
        }

        foreach (var key in ent.Comp.OuterSideBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetVisible((ent.Owner, sprite), index, !frontMode);
        }
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

            var displacementKey = $"{key}-displacement";
            if (slots.VisualLayerKeys[ShoesSlot].Contains(displacementKey)
                && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false))
            {
                ent.Comp.HiddenShoeKeys.Add(displacementKey);
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
            _sprite.LayerSetVisible(layer, ent.Comp.ClothingMode == 2);
            _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
            _sprite.LayerSetDirOffset(layer, src.DirOffset);

            var shader = _prototypes.Index(FootBandShader).InstanceUnique();
            shader.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(bandKey, shader, FootBandShader.Id);
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

        ent.Comp.ShoeSplitKeys.Clear();
        ent.Comp.HiddenShoeKeys.Clear();
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

        ent.Comp.OuterSplitKeys.Clear();
        if (clearHole)
            ent.Comp.HoledOuterKeys.Clear();
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

        _sprite.LayerSetOffset(ent, index, offset);
        walk.TouchedStringLayers.Add(layerKey);
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
            ClearClothingWalkLayers((uid, walk));
            if (_spriteQuery.TryGetComponent(uid, out var sprite))
            {
                SetBodyFeetHidden((uid, walk), sprite, hide: false);
                ResetLowerBody((uid, walk), sprite);
            }

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
        if (!ent.Comp.ClothingSplitsActive
            && ent.Comp.ShoeSplitKeys.Count == 0
            && ent.Comp.OuterSplitKeys.Count == 0
            && ent.Comp.OuterSideBandKeys.Count == 0)
        {
            if (_spriteQuery.TryGetComponent(ent.Owner, out var idleSprite))
                SetBodyFeetHidden(ent, idleSprite, hide: false);
            return;
        }

        ClearShoeSplits(ent);
        ClearOuterSplits(ent, clearHole: false);
        ClearOuterSideBands(ent, clearHole: true);
        ent.Comp.ClothingSplitsActive = false;
        ent.Comp.ClothingMode = 0;
        ent.Comp.AppliedOuterFootCut = float.NaN;

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
    /// Zero only layers touched last tick, then clear the touch sets for this tick's Apply*.
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
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
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
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }

        foreach (var key in ent.Comp.OuterSplitKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }

        foreach (var key in ent.Comp.OuterSideBandKeys)
        {
            if (_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                _sprite.LayerSetOffset((ent.Owner, sprite), index, Vector2.Zero);
        }
    }
}
