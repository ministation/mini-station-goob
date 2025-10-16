using Content.Shared.Borgs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server.Actions;
using System;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Mobs;
using Robust.Shared.Player;

namespace Content.Server.Borgs
{
    public sealed class MimicrySystem : EntitySystem
    {
        [Dependency] private readonly IComponentFactory _factory = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly ActionsSystem _actions = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly PointLightSystem _pointLightSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MimicryComponent, PlayerAttachedEvent>(OnPlayerAttached);
            SubscribeLocalEvent<MimicryComponent, MimicryEvent>(OnMimicry);
            SubscribeLocalEvent<MimicryComponent, MobStateChangedEvent>(OnStateChanged);
        }

        private void OnPlayerAttached(EntityUid uid, MimicryComponent component, PlayerAttachedEvent args)
        {
            _actions.AddAction(uid, component.MimicryAction);
        }
        private void OnMimicry(EntityUid uid, MimicryComponent component, MimicryEvent args)
        {
            if (args.Handled) { return; }
            _popup.PopupEntity(Loc.GetString("borg-mimicry"), uid, PopupType.Small);
            TryComp<PointLightComponent>(uid, out var pointLightComponent);
            if (!component.Mimicked)
            {
                _appearance.SetData(uid, MiniBorgVisuals.Eng, true);
                _appearance.SetData(uid, MiniBorgVisuals.Real, false);
                _appearance.SetData(uid, MiniBorgVisuals.Dead, false);
                component.Mimicked = true;
                Dirty(uid, component);
                if (pointLightComponent != null)
                {
                    _pointLightSystem.SetColor(uid, Color.Green, pointLightComponent);
                }
                Dirty(uid, component);
            }
            else
            {
                _appearance.SetData(uid, MiniBorgVisuals.Real, true);
                _appearance.SetData(uid, MiniBorgVisuals.Eng, false);
                _appearance.SetData(uid, MiniBorgVisuals.Dead, false);
                component.Mimicked = false;
                Dirty(uid, component);
                if (pointLightComponent != null)
                {
                    _pointLightSystem.SetColor(uid, Color.Blue, pointLightComponent);
                }
                Dirty(uid, component);
            }
            args.Handled = true;
        }
        private void OnStateChanged(EntityUid uid, MimicryComponent component, MobStateChangedEvent args)
        {
            component.Mimicked = false;
            if (args.NewMobState != MobState.Alive)
            {
                _appearance.SetData(uid, MiniBorgVisuals.Real, false);
                _appearance.SetData(uid, MiniBorgVisuals.Eng, false);
                _appearance.SetData(uid, MiniBorgVisuals.Dead, true);
            }
            else
            {
                _appearance.SetData(uid, MiniBorgVisuals.Real, true);
                _appearance.SetData(uid, MiniBorgVisuals.Eng, false);
                _appearance.SetData(uid, MiniBorgVisuals.Dead, false);
            }
            Dirty(uid, component);
        }
    }
}
