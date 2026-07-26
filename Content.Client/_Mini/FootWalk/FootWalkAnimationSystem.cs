// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Clothing;
using Content.Client.Inventory;
using Content.Shared._Mini.FootWalk;
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
using Robust.Shared.Graphics.RSI;
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
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

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
        ResetLowerBody(ent);
        ClearShoeSplits(ent);
        ClearOuterSplits(ent);
        ClearOuterSideBands(ent);
        ent.Comp.ClothingSplitsActive = false;
    }

    private void OnDidEquip(Entity<FootWalkAnimationComponent> ent, ref DidEquipEvent args)
    {
        if (args.Slot is not (ShoesSlot or OuterSlot))
            return;

        // Force rebuild next frame for current facing.
        ent.Comp.ClothingSplitsActive = false;
        ClearShoeSplits(ent);
        ClearOuterSplits(ent);
        ClearOuterSideBands(ent);
    }

    private void OnDidUnequip(Entity<FootWalkAnimationComponent> ent, ref DidUnequipEvent args)
    {
        if (args.Slot == ShoesSlot)
            ClearShoeSplits(ent);
        else if (args.Slot == OuterSlot)
        {
            ClearOuterSplits(ent);
            ClearOuterSideBands(ent);
        }
    }

    private void OnVisualsChanged(Entity<FootWalkAnimationComponent> ent, ref VisualsChangedEvent args)
    {
        if (args.ContainerId is not (ShoesSlot or OuterSlot))
            return;

        ent.Comp.ClothingSplitsActive = false;
        ClearShoeSplits(ent);
        ClearOuterSplits(ent);
        ClearOuterSideBands(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<FootWalkAnimationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walk, out _))
        {
            if (!_spriteQuery.TryGetComponent(uid, out var sprite))
                continue;

            if (!CanAnimate(uid)
                || !_physicsQuery.TryGetComponent(uid, out var physics)
                || physics.LinearVelocity.LengthSquared() < walk.MinSpeedSquared
                || !HasLowerBodyVisuals(uid, sprite))
            {
                ClearClothingWalkLayers((uid, walk));
                ResetLowerBody((uid, walk), sprite);
                continue;
            }

            var facing = _xform.GetWorldRotation(uid).ToRsiDirection(RsiDirectionType.Dir4);
            var useSplits = facing is RsiDirection.South or RsiDirection.North;
            EnsureClothingSplits((uid, walk), useSplits);

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

            ResetLowerBody((uid, walk), sprite, clearTouched: false);
            walk.TouchedEnumLayers.Clear();
            walk.TouchedStringLayers.Clear();

            _humanoidQuery.TryGetComponent(uid, out var humanoid);

            if (useSplits)
            {
                ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY));
                ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY));

                // South: texture left ≈ RFoot. North mirrors L/R on the sheet.
                var invert = facing == RsiDirection.North;
                ApplySplitHalves((uid, sprite), walk, walk.ShoeSplitKeys, leftY, rightY, invert);
                ApplySplitHalves((uid, sprite), walk, walk.OuterSplitKeys, leftY, rightY, invert);
            }
            else
            {
                // Side states: one clothing sprite covers both feet. If boots/suit lift alone,
                // the lower body peeks through the punched hole — keep body glued to clothing Y.
                var sideY = MathF.Max(leftY, rightY);
                var covered = walk.OuterSideBandKeys.Count > 0 || HasSlotVisuals(uid, ShoesSlot);

                if (covered)
                {
                    var cover = new Vector2(0f, sideY);
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, cover);
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, cover);
                }
                else
                {
                    ApplySide((uid, sprite), walk, humanoid, LeftLayers, new Vector2(0f, leftY));
                    ApplySide((uid, sprite), walk, humanoid, RightLayers, new Vector2(0f, rightY));
                }

                ApplyFullSlotOffset((uid, sprite), walk, ShoesSlot, sideY);
                ApplySideBandOffset((uid, sprite), walk, sideY);
            }
        }
    }

    private bool HasSlotVisuals(EntityUid uid, string slot)
    {
        return _invSlotsQuery.TryGetComponent(uid, out var slots)
               && slots.VisualLayerKeys.TryGetValue(slot, out var keys)
               && keys.Count > 0;
    }

    private void ApplySide(
        Entity<SpriteComponent?> ent,
        FootWalkAnimationComponent walk,
        HumanoidAppearanceComponent? humanoid,
        HumanoidVisualLayers[] layers,
        Vector2 offset)
    {
        foreach (var layer in layers)
        {
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

    private void EnsureClothingSplits(Entity<FootWalkAnimationComponent> ent, bool useSplits)
    {
        if (useSplits)
        {
            if (ent.Comp.OuterSideBandKeys.Count > 0)
                ClearOuterSideBands(ent);

            EnsureShoeSplits(ent, forceRebuild: !ent.Comp.ClothingSplitsActive);
            EnsureOuterSplits(ent, forceRebuild: !ent.Comp.ClothingSplitsActive);
            ent.Comp.ClothingSplitsActive = true;
            return;
        }

        if (ent.Comp.ClothingSplitsActive
            || ent.Comp.ShoeSplitKeys.Count > 0
            || ent.Comp.OuterSplitKeys.Count > 0)
        {
            ClearShoeSplits(ent);
            ClearOuterSplits(ent);
            ent.Comp.ClothingSplitsActive = false;
        }

        EnsureOuterSideBands(ent, forceRebuild: ent.Comp.OuterSideBandKeys.Count == 0);
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

            _sprite.LayerSetVisible((ent.Owner, sprite), key, false);
            ent.Comp.HiddenShoeKeys.Add(key);

            var displacementKey = $"{key}-displacement";
            if (slots.VisualLayerKeys[ShoesSlot].Contains(displacementKey)
                && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false))
            {
                _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
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
    }

    private void EnsureOuterSplits(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearOuterSplits(ent);
            return;
        }

        if (!TryGetSourceKeys(slots, OuterSlot, out var sourceKeys))
        {
            ClearOuterSplits(ent);
            return;
        }

        if (!forceRebuild && SplitsMatch(ent.Comp.OuterSplitKeys, sourceKeys))
            return;

        ClearOuterSplits(ent, sprite);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            // Punch foot band out of the full suit so halves can bob without double boots.
            var hole = _prototypes.Index(FootHoleShader).InstanceUnique();
            hole.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(key, hole, FootHoleShader.Id);
            ent.Comp.HoledOuterKeys.Add(key);

            var displacementKey = $"{key}-displacement";
            if (slots.VisualLayerKeys[OuterSlot].Contains(displacementKey)
                && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false))
            {
                _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
                ent.Comp.HoledOuterKeys.Add(displacementKey);
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
    }

    private void EnsureOuterSideBands(Entity<FootWalkAnimationComponent> ent, bool forceRebuild)
    {
        if (!_spriteQuery.TryGetComponent(ent.Owner, out var sprite)
            || !_invSlotsQuery.TryGetComponent(ent.Owner, out var slots))
        {
            ClearOuterSideBands(ent);
            return;
        }

        if (!TryGetSourceKeys(slots, OuterSlot, out var sourceKeys))
        {
            ClearOuterSideBands(ent);
            return;
        }

        if (!forceRebuild && SideBandsMatch(ent.Comp.OuterSideBandKeys, sourceKeys))
            return;

        ClearOuterSideBands(ent, sprite);

        foreach (var key in sourceKeys)
        {
            if (!_sprite.TryGetLayer((ent.Owner, sprite), key, out var src, false))
                continue;

            var hole = _prototypes.Index(FootHoleShader).InstanceUnique();
            hole.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(key, hole, FootHoleShader.Id);
            ent.Comp.HoledOuterKeys.Add(key);

            var displacementKey = $"{key}-displacement";
            if (slots.VisualLayerKeys[OuterSlot].Contains(displacementKey)
                && _sprite.LayerMapTryGet((ent.Owner, sprite), displacementKey, out _, false))
            {
                _sprite.LayerSetVisible((ent.Owner, sprite), displacementKey, false);
                ent.Comp.HoledOuterKeys.Add(displacementKey);
            }

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
            _sprite.LayerSetVisible(layer, true);
            _sprite.LayerSetAutoAnimated(layer, src.AutoAnimated);
            _sprite.LayerSetDirOffset(layer, src.DirOffset);

            var shader = _prototypes.Index(FootBandShader).InstanceUnique();
            shader.SetParameter("footCut", ent.Comp.OuterFootCut);
            sprite.LayerSetShader(bandKey, shader, FootBandShader.Id);
            ent.Comp.OuterSideBandKeys.Add(bandKey);
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

    private void ClearOuterSplits(Entity<FootWalkAnimationComponent> ent, SpriteComponent? sprite = null)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.OuterSplitKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            foreach (var key in ent.Comp.HoledOuterKeys)
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                    continue;

                // Restore visibility if we hid displacement; clear foot-hole shader.
                _sprite.LayerSetVisible((ent.Owner, sprite), index, true);
                sprite.LayerSetShader(index, shader: null, prototype: null);
            }
        }

        ent.Comp.OuterSplitKeys.Clear();
        ent.Comp.HoledOuterKeys.Clear();
    }

    private void ClearOuterSideBands(Entity<FootWalkAnimationComponent> ent, SpriteComponent? sprite = null)
    {
        if (sprite == null)
            _spriteQuery.TryGetComponent(ent.Owner, out sprite);

        if (sprite != null)
        {
            foreach (var key in ent.Comp.OuterSideBandKeys)
                _sprite.RemoveLayer((ent.Owner, sprite), key, logMissing: false);

            foreach (var key in ent.Comp.HoledOuterKeys)
            {
                if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false))
                    continue;

                _sprite.LayerSetVisible((ent.Owner, sprite), index, true);
                sprite.LayerSetShader(index, shader: null, prototype: null);
            }
        }

        ent.Comp.OuterSideBandKeys.Clear();
        ent.Comp.HoledOuterKeys.Clear();
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

    private bool CanAnimate(EntityUid uid)
    {
        if (_borgQuery.HasComp(uid))
            return false;

        // Clown/jester shoes already play WaddleAnimation — skip foot bob.
        if (_waddleQuery.HasComp(uid))
            return false;

        if (_mobQuery.TryGetComponent(uid, out var mob) && !_mobState.IsAlive(uid, mob))
            return false;

        return !_standing.IsDown(uid);
    }

    private void ClearClothingWalkLayers(Entity<FootWalkAnimationComponent> ent)
    {
        if (!ent.Comp.ClothingSplitsActive
            && ent.Comp.ShoeSplitKeys.Count == 0
            && ent.Comp.OuterSplitKeys.Count == 0
            && ent.Comp.OuterSideBandKeys.Count == 0)
            return;

        ClearShoeSplits(ent);
        ClearOuterSplits(ent);
        ClearOuterSideBands(ent);
        ent.Comp.ClothingSplitsActive = false;
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

    private void ResetLowerBody(
        Entity<FootWalkAnimationComponent> ent,
        SpriteComponent? sprite = null,
        bool clearTouched = true)
    {
        if (sprite == null && !_spriteQuery.TryGetComponent(ent.Owner, out sprite))
        {
            ent.Comp.TouchedEnumLayers.Clear();
            ent.Comp.TouchedStringLayers.Clear();
            return;
        }

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

        if (clearTouched)
        {
            ent.Comp.TouchedEnumLayers.Clear();
            ent.Comp.TouchedStringLayers.Clear();
        }
    }
}
