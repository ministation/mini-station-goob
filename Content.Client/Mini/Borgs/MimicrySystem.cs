using Content.Shared.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Borgs
{
    public sealed class MimicrySystem : EntitySystem
    {
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MimicryComponent, AppearanceChangeEvent>(OnAppearanceChange);
        }
        private void OnAppearanceChange(EntityUid uid, MimicryComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;
            if (_appearance.TryGetData<bool>(uid, MiniBorgVisuals.Eng, out var eng, args.Component) && eng)
            {
                args.Sprite.LayerSetState(0, component.EngState);
                args.Sprite.LayerSetState(BorgVisualLayers.Light, component.EngState1);
                args.Sprite.LayerSetState("light", component.EngState2);
            }
            else if (_appearance.TryGetData<bool>(uid, MiniBorgVisuals.Real, out var real, args.Component) && real)
            {
                args.Sprite.LayerSetState(0, component.RealState);
                args.Sprite.LayerSetState(BorgVisualLayers.Light, component.RealState1);
                args.Sprite.LayerSetState("light", component.RealState2);
            }
        }
    }
}
