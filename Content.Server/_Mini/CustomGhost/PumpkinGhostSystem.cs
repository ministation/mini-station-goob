using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared._Mini.CustomGhost;
using Robust.Server.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Mini.CustomGhost;

/// <summary>
/// Уникальная способность тыквенного призрака: раз в 5 минут разбивает одну
/// целую лампочку поблизости. Компонент выдаётся только темой GhostThemePumpkin.
/// </summary>
public sealed class PumpkinGhostSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PumpkinGhostComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PumpkinGhostComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PumpkinGhostComponent, PumpkinBreakLightActionEvent>(OnBreakLight);
    }

    // Тема применяется уже после MapInit (AddComponents при выдаче темы),
    // поэтому экшен выдаём в ComponentStartup, как это делает ApplyTheme.
    private void OnStartup(Entity<PumpkinGhostComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.ActionEntity != null)
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnShutdown(Entity<PumpkinGhostComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnBreakLight(Entity<PumpkinGhostComponent> ent, ref PumpkinBreakLightActionEvent args)
    {
        if (args.Handled)
            return;

        // Лампочки-кандидаты перемешиваются: гаснет случайная из целых в радиусе,
        // а не ближайшая, так пугает непредсказуемее.
        var lights = new List<Entity<PoweredLightComponent>>();
        foreach (var uid in _lookup.GetEntitiesInRange(args.Performer, ent.Comp.LightBreakRadius))
        {
            if (TryComp<PoweredLightComponent>(uid, out var light))
                lights.Add((uid, light));
        }
        _random.Shuffle(lights);

        foreach (var light in lights)
        {
            if (!_lights.TryDestroyBulb(light, light.Comp, args.Performer))
                continue;

            args.Handled = true;

            // Искры, оранжевая вспышка и хоррор-звук на месте лампочки: звук через
            // EmitSoundOnSpawn слышат все игроки в радиусе PVS - и живые, и призраки.
            Spawn("EffectPumpkinBulbBurst", Transform(light).Coordinates);

            _popup.PopupEntity(Loc.GetString("pumpkin-ghost-light-broken"), ent, ent);
            return;
        }

        // Нет цели - кулдаун не запускаем, чтобы не тратить попытку в пустой комнате.
        _popup.PopupEntity(Loc.GetString("pumpkin-ghost-no-lights"), ent, ent);
    }
}
