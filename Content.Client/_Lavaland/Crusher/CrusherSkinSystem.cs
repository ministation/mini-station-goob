// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Client.Items.Systems;
using Content.Shared._Lavaland.Weapons.CrusherSkin;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Storage.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;

namespace Content.Client._Lavaland.Crusher;

/// <summary>
/// Applies the cosmetic crusher skin: item sprite, upgrade overlays and the in-hand layers.
/// The server only stores which skin is selected.
/// </summary>
public sealed class CrusherSkinSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrusherSkinComponent, AfterAutoHandleStateEvent>(OnSkinChanged);
        SubscribeLocalEvent<CrusherSkinComponent, GetInhandVisualsEvent>(OnGetInhand, after: new[] { typeof(ItemSystem) });
    }

    private void OnSkinChanged(Entity<CrusherSkinComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Index < 0 || ent.Comp.Index >= ent.Comp.Skins.Count)
            return;

        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var skin = ent.Comp.Skins[ent.Comp.Index];
        var rsiPath = SpriteSpecifierSerializer.TextureRoot / skin.Sprite;
        _sprite.SetBaseRsi((ent.Owner, sprite), _cache.GetResource<RSIResource>(rsiPath).RSI);

        // Upgrade overlays carry their own RSI, so they have to be pointed at the new set as well.
        if (TryComp<ItemMapperComponent>(ent.Owner, out var mapper))
        {
            mapper.RSIPath = rsiPath;
            foreach (var layer in mapper.SpriteLayers)
                _sprite.LayerSetSprite((ent.Owner, sprite), layer, new SpriteSpecifier.Rsi(rsiPath, layer));
        }

        _item.VisualsChanged(ent.Owner);
    }

    private void OnGetInhand(Entity<CrusherSkinComponent> ent, GetInhandVisualsEvent args)
    {
        if (ent.Comp.Index < 0 || ent.Comp.Index >= ent.Comp.Skins.Count)
            return;

        if (!TryComp<ItemComponent>(ent.Owner, out var item))
            return;

        var state = item.HeldPrefix == null
            ? $"inhand-{args.Location.ToString().ToLowerInvariant()}"
            : $"{item.HeldPrefix}-inhand-{args.Location.ToString().ToLowerInvariant()}";

        args.Layers.Clear();
        args.Layers.Add((state, new PrototypeLayerData
        {
            RsiPath = ent.Comp.Skins[ent.Comp.Index].Inhand,
            State = state,
            MapKeys = new() { state },
        }));
    }
}
