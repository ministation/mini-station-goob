using Content.Shared._Arcane.ERP;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Arcane.ERP;

public sealed class CumOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CumOverlayComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CumOverlayComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CumOverlayComponent, AfterAutoHandleStateEvent>(OnStateChanged);
    }

    private void OnStartup(Entity<CumOverlayComponent> ent, ref ComponentStartup args)
    {
        UpdateLayer(ent);
    }

    private void OnStateChanged(Entity<CumOverlayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLayer(ent);
    }

    private void OnShutdown(Entity<CumOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        var sprite = (ent.Owner, spriteComp);
        if (_sprite.LayerMapTryGet(sprite, CumOverlayComponent.Layer.Base, out var layer, false))
            _sprite.RemoveLayer(sprite, layer);
    }

    private void UpdateLayer(Entity<CumOverlayComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        var sprite = (ent.Owner, spriteComp);
        var state = ent.Comp.Count >= 2 ? "cum_large" : "cum_normal";
        var spec = new SpriteSpecifier.Rsi(CumOverlayComponent.OverlayRsi, state);

        if (_sprite.LayerMapTryGet(sprite, CumOverlayComponent.Layer.Base, out var existing, false))
            _sprite.RemoveLayer(sprite, existing);

        var layer = _sprite.AddLayer(sprite, spec);
        _sprite.LayerMapSet(sprite, CumOverlayComponent.Layer.Base, layer);
    }
}
