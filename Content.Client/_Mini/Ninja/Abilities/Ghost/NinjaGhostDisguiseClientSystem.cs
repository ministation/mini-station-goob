// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using System.Text;
using Content.Client.Clothing;
using Content.Client._Mini.Ninja.Systems;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Client side of the chameleon: while the disguise is active the real gear of the ninja is hidden and the
/// clothing of the scanned crew member is drawn instead, so the disguise is visible to everyone.
/// </summary>
/// <remarks>
/// The server can not do this itself: the clothing visuals of an item are not networked, so copying them on the
/// server changes nothing for the clients. The server only replicates the outfit of the active disguise, and this
/// system replaces the equipment visuals of every worn item. An item that the scanned target did not wear gets no
/// layers at all, which hides it.
/// </remarks>
public sealed partial class NinjaGhostDisguiseClientSystem : EntitySystem
{
    /// <summary>
    /// How often the disguise is re-checked. The clothing layers may be redrawn by the engine at any time.
    /// </summary>
    private const float IntegrityInterval = 0.5f;

    private const string JumpsuitSlot = "jumpsuit";
    private const string OuterClothingSlot = "outerClothing";
    private const string GapLayerPrefix = "mini-disguise";

    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// Disguised wearers with the state that was applied to them.
    /// </summary>
    private readonly Dictionary<EntityUid, WearerDisguise> _active = new();

    private readonly HashSet<EntityUid> _syncedWearers = new();
    private readonly List<EntityUid> _staleWearers = new();
    private readonly List<EntityUid> _staleItems = new();

    private float _integrityTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostDisguiseComponent, ComponentStartup>(OnSuitStartup);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, AfterAutoHandleStateEvent>(OnSuitState);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, ComponentShutdown>(OnSuitShutdown);
        SubscribeLocalEvent<NinjaGhostDisguiseItemComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: new[] { typeof(ClientClothingSystem), typeof(NinjaAppearanceSystem) });
        SubscribeLocalEvent<NinjaGhostDisguiseItemComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _integrityTimer += frameTime;
        if (_integrityTimer < IntegrityInterval)
            return;

        _integrityTimer %= IntegrityInterval;
        _syncedWearers.Clear();

        var query = EntityQueryEnumerator<NinjaGhostDisguiseComponent>();
        while (query.MoveNext(out var suit, out var comp))
        {
            if (!IsDisguised(comp))
                continue;

            var wearer = Transform(suit).ParentUid;
            if (!wearer.IsValid())
                continue;

            _syncedWearers.Add(wearer);
            SyncWearer(wearer, suit, comp);
        }

        _staleWearers.Clear();
        foreach (var wearer in _active.Keys)
        {
            if (!_syncedWearers.Contains(wearer))
                _staleWearers.Add(wearer);
        }

        foreach (var wearer in _staleWearers)
            ClearWearer(wearer);

        _staleWearers.Clear();
        _syncedWearers.Clear();

        // Markers left on items that are no longer worn by a disguised ninja (dropped, stolen or given away).
        _staleItems.Clear();
        var markerQuery = EntityQueryEnumerator<NinjaGhostDisguiseItemComponent>();
        while (markerQuery.MoveNext(out var item, out var marker))
        {
            if (marker.Wearer.IsValid() &&
                _active.ContainsKey(marker.Wearer) &&
                Transform(item).ParentUid == marker.Wearer)
            {
                continue;
            }

            _staleItems.Add(item);
        }

        foreach (var item in _staleItems)
        {
            if (Exists(item))
                RemoveItemSubstitution(item);
        }

        _staleItems.Clear();
    }

    private void OnSuitStartup(Entity<NinjaGhostDisguiseComponent> ent, ref ComponentStartup args)
    {
        SyncSuit(ent);
    }

    private void OnSuitState(Entity<NinjaGhostDisguiseComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SyncSuit(ent);
    }

    private void OnSuitShutdown(Entity<NinjaGhostDisguiseComponent> ent, ref ComponentShutdown args)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (wearer.IsValid())
            ClearWearer(wearer);
    }

    private void SyncSuit(Entity<NinjaGhostDisguiseComponent> ent)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid())
            return;

        if (IsDisguised(ent.Comp))
            SyncWearer(wearer, ent.Owner, ent.Comp);
        else
            ClearWearer(wearer);
    }

    private static bool IsDisguised(NinjaGhostDisguiseComponent comp)
    {
        return comp.Disguised && comp.ActiveIndex >= 0 && comp.ActiveOutfit.Count > 0;
    }

    private void SyncWearer(EntityUid wearer, EntityUid suit, NinjaGhostDisguiseComponent comp)
    {
        if (!TryComp<InventoryComponent>(wearer, out var inventory) ||
            !TryComp<SpriteComponent>(wearer, out var sprite))
        {
            return;
        }

        var outfit = BuildOutfit(comp.ActiveOutfit);
        var outfitKey = OutfitKey(comp.ActiveOutfit);

        WearerDisguise? state = null;
        if (_active.TryGetValue(wearer, out var current))
            state = current;

        if (state != null && (state.Suit != suit || state.OutfitKey != outfitKey))
        {
            ClearWearer(wearer);
            state = null;
        }

        if (state == null)
        {
            state = new WearerDisguise { Suit = suit, OutfitKey = outfitKey };
            _active[wearer] = state;
        }

        var wornItems = new HashSet<EntityUid>();
        var occupiedSlots = new HashSet<string>();

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var item, out var slot))
        {
            wornItems.Add(item);
            occupiedSlots.Add(slot.Name);

            if (!HasComp<ClothingComponent>(item))
                continue;

            EnsureItemSubstitution(state, item, wearer, slot.Name, ResolvePrototype(outfit, slot.Name));
        }

        // Items that are no longer worn must not keep the replacement visuals.
        _staleItems.Clear();
        foreach (var item in state.MarkedItems)
        {
            if (wornItems.Contains(item) && HasComp<NinjaGhostDisguiseItemComponent>(item))
                continue;

            _staleItems.Add(item);
        }

        foreach (var item in _staleItems)
        {
            state.MarkedItems.Remove(item);

            if (Exists(item))
                RemoveItemSubstitution(item);
        }

        _staleItems.Clear();

        // Slots the scanned crew member had clothing in but the ninja has no own item for: draw the layers directly.
        var desiredSlots = new Dictionary<string, EntProtoId>(outfit);
        if (!desiredSlots.ContainsKey(OuterClothingSlot) && outfit.TryGetValue(JumpsuitSlot, out var jumpsuit))
            desiredSlots[OuterClothingSlot] = jumpsuit;

        foreach (var (slotName, protoId) in desiredSlots)
        {
            if (occupiedSlots.Contains(slotName))
            {
                RemoveGapLayers(wearer, sprite, state, slotName);
                continue;
            }

            if (state.GapLayers.ContainsKey(slotName))
                continue;

            DrawGapLayers(wearer, sprite, state, slotName, protoId);
        }
    }

    private void EnsureItemSubstitution(
        WearerDisguise state,
        EntityUid item,
        EntityUid wearer,
        string slot,
        EntProtoId? prototype)
    {
        state.MarkedItems.Add(item);

        if (TryComp<NinjaGhostDisguiseItemComponent>(item, out var marker) &&
            marker.Wearer == wearer &&
            marker.Slot == slot &&
            SamePrototype(marker.Prototype, prototype))
        {
            return;
        }

        if (marker == null)
            marker = AddComp<NinjaGhostDisguiseItemComponent>(item);

        marker.Wearer = wearer;
        marker.Slot = slot;
        marker.Prototype = prototype;
        marker.Layers = BuildLayers(prototype, wearer, slot);

        // Ask the clothing system to redraw the slot with the new layers.
        _item.VisualsChanged(item);
    }

    private bool RemoveItemSubstitution(EntityUid item)
    {
        if (!HasComp<NinjaGhostDisguiseItemComponent>(item))
            return false;

        RemComp<NinjaGhostDisguiseItemComponent>(item);
        _item.VisualsChanged(item);
        return true;
    }

    private void ClearWearer(EntityUid wearer)
    {
        if (!_active.Remove(wearer, out var state))
            return;

        foreach (var item in state.MarkedItems)
        {
            if (!Exists(item))
                continue;

            RemoveItemSubstitution(item);
        }

        state.MarkedItems.Clear();

        if (!Exists(wearer) || !TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        foreach (var keys in state.GapLayers.Values)
        {
            foreach (var key in keys)
                _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);
        }

        state.GapLayers.Clear();
    }

    private void DrawGapLayers(
        EntityUid wearer,
        SpriteComponent sprite,
        WearerDisguise state,
        string slot,
        EntProtoId prototype)
    {
        var layers = BuildLayers(prototype, wearer, slot);

        if (layers.Count == 0 || !_inventory.TryGetSlot(wearer, slot, out var slotDefinition))
            return;

        // Only equipment slots of the appearance are drawn directly; pocket contents never show on the sprite.
        if ((slotDefinition.SlotFlags & SlotFlags.WITHOUT_POCKET) == 0)
            return;

        var offset = slotDefinition.Offset;
        var hasBookmark = _sprite.LayerMapTryGet((wearer, sprite), slot, out var index, false);
        var keys = new List<string>(layers.Count);

        for (var i = 0; i < layers.Count; i++)
        {
            var key = $"{GapLayerPrefix}-{slot}-{i}";
            var data = layers[i].Layer;

            if (hasBookmark)
            {
                index++;
                _sprite.AddBlankLayer((wearer, sprite), index);
                _sprite.LayerMapSet((wearer, sprite), key, index);
                _sprite.LayerSetData((wearer, sprite), index, data);
                SetGapLayerOffset(wearer, sprite, index, data, offset);
                _sprite.LayerSetVisible((wearer, sprite), key, true);
            }
            else
            {
                index = _sprite.LayerMapReserve((wearer, sprite), key);
                _sprite.LayerSetData((wearer, sprite), index, data);
                SetGapLayerOffset(wearer, sprite, index, data, offset);
                _sprite.LayerSetVisible((wearer, sprite), key, true);
            }

            keys.Add(key);
        }

        state.GapLayers[slot] = keys;
    }

    private void SetGapLayerOffset(
        EntityUid wearer,
        SpriteComponent sprite,
        int index,
        PrototypeLayerData data,
        Vector2 slotOffset)
    {
        if (!_sprite.TryGetLayer((wearer, sprite), index, out var layer, false))
            return;

        _sprite.LayerSetOffset(layer!, (data.Offset ?? Vector2.Zero) + slotOffset);
    }

    private void RemoveGapLayers(EntityUid wearer, SpriteComponent sprite, WearerDisguise state, string slot)
    {
        if (!state.GapLayers.Remove(slot, out var keys))
            return;

        foreach (var key in keys)
            _sprite.RemoveLayer((wearer, sprite), key, logMissing: false);
    }

    private void OnGetEquipmentVisuals(Entity<NinjaGhostDisguiseItemComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        // Replace the visuals of the real item with the scanned clothing, or hide it when the slot was empty.
        args.Layers.Clear();

        // The marker may be left on an item that is now worn by somebody else: show the real item then.
        if (args.Equipee != ent.Comp.Wearer)
            return;

        foreach (var layer in ent.Comp.Layers)
            args.Layers.Add(layer);
    }

    private void OnEquipmentVisualsUpdated(Entity<NinjaGhostDisguiseItemComponent> ent,
        ref EquipmentVisualsUpdatedEvent args)
    {
        if (!ent.Comp.Prototype.HasValue)
            return;

        if (!TryComp<SpriteComponent>(args.Equipee, out var sprite))
            return;

        // The worn ninja suit hides the jumpsuit layer through HideClothingLayerClothing, but the disguise
        // must show the scanned jumpsuit.
        foreach (var key in args.RevealedLayers)
            _sprite.LayerSetVisible((args.Equipee, sprite), key, true);
    }

    /// <summary>
    /// Precomputes the equipment layers of a clothing prototype by asking the clothing system for them,
    /// using a temporary client side copy of the prototype.
    /// </summary>
    private List<(string Key, PrototypeLayerData Layer)> BuildLayers(EntProtoId? prototype, EntityUid wearer, string slot)
    {
        var result = new List<(string Key, PrototypeLayerData Layer)>();

        if (prototype is not { } protoId || !_proto.HasIndex<EntityPrototype>(protoId.Id))
            return result;

        var proxy = Spawn(protoId.Id, MapCoordinates.Nullspace);
        if (!proxy.IsValid())
            return result;

        var visualEvent = new GetEquipmentVisualsEvent(wearer, slot);
        RaiseLocalEvent(proxy, visualEvent);
        result.AddRange(visualEvent.Layers);
        QueueDel(proxy);

        return result;
    }

    private static Dictionary<string, EntProtoId> BuildOutfit(List<NinjaGhostDisguiseClothing> outfit)
    {
        var result = new Dictionary<string, EntProtoId>(outfit.Count);

        foreach (var clothing in outfit)
        {
            if (!string.IsNullOrEmpty(clothing.Slot))
                result[clothing.Slot] = clothing.Prototype;
        }

        return result;
    }

    private static string OutfitKey(List<NinjaGhostDisguiseClothing> outfit)
    {
        var builder = new StringBuilder();

        foreach (var clothing in outfit)
        {
            builder.Append(clothing.Slot);
            builder.Append('=');
            builder.Append(clothing.Prototype.Id);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static EntProtoId? ResolvePrototype(Dictionary<string, EntProtoId> outfit, string slot)
    {
        if (outfit.TryGetValue(slot, out var prototype))
            return prototype;

        // The worn ninja suit covers the jumpsuit slot, so a scanned crew member in a jumpsuit and no outer
        // clothing is drawn with the jumpsuit in the outer slot instead.
        if (slot == OuterClothingSlot && outfit.TryGetValue(JumpsuitSlot, out var jumpsuit))
            return jumpsuit;

        return null;
    }

    private static bool SamePrototype(EntProtoId? left, EntProtoId? right)
    {
        if (!left.HasValue || !right.HasValue)
            return left.HasValue == right.HasValue;

        return left.Value.Id == right.Value.Id;
    }

    /// <summary>
    /// State applied to one disguised wearer.
    /// </summary>
    private sealed class WearerDisguise
    {
        public EntityUid Suit;
        public string OutfitKey = string.Empty;
        public readonly HashSet<EntityUid> MarkedItems = new();
        public readonly Dictionary<string, List<string>> GapLayers = new();
    }
}
