// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Pinpointer;
using Content.Shared.Verbs;

namespace Content.Server._Mini.Pinpointer.Systems;

/// <summary>
/// Mini: переключение пинпоинтера ОБР между ядерной бомбой и шаттлом отряда.
/// </summary>
public sealed class ErtShuttlePinpointerSystem : EntitySystem
{
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ErtShuttlePinpointerComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerbs);
    }

    /// <summary>
    /// Привязывает пинпоинтер к шаттлу ОБР. Вызывается при загрузке шаттла отряда.
    /// </summary>
    public void AttachShuttleTarget(EntityUid pinUid, EntityUid shuttleMarker, EntityUid? defaultTarget)
    {
        var comp = EnsureComp<ErtShuttlePinpointerComponent>(pinUid);
        comp.ShuttleTarget = shuttleMarker;
        comp.DefaultTarget = defaultTarget;
    }

    private void OnGetVerbs(EntityUid uid, ErtShuttlePinpointerComponent comp, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!HasComp<PinpointerComponent>(uid)
            || comp.ShuttleTarget is not { } shuttle
            || TerminatingOrDeleted(shuttle))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString(comp.ShowingShuttle ? "ert-pinpointer-switch-to-bomb" : "ert-pinpointer-switch-to-shuttle"),
            Act = () => Toggle(uid, comp),
        });
    }

    /// <summary>
    /// Переключает цель: шаттл ОБР, затем обратно на штатную (бомба/вызвавший).
    /// internal для интеграционного теста.
    /// </summary>
    internal void Toggle(EntityUid uid, ErtShuttlePinpointerComponent comp)
    {
        if (!TryComp<PinpointerComponent>(uid, out var pin))
            return;

        if (comp.ShuttleTarget is { } shuttle && !TerminatingOrDeleted(shuttle))
        {
            if (!comp.ShowingShuttle)
            {
                comp.ShowingShuttle = true;
                _pinpointer.SetTargets(uid, new List<EntityUid> { shuttle }, pin);
                return;
            }
        }

        comp.ShowingShuttle = false;

        // Обратно на штатную цель: явную, если она была, иначе пустой список
        // снова включает поиск по whitelist (ядерный диск/бомба).
        var targets = comp.DefaultTarget is { } defaultTarget && EntityManager.EntityExists(defaultTarget)
            ? new List<EntityUid> { defaultTarget }
            : new List<EntityUid>();
        _pinpointer.SetTargets(uid, targets, pin);
    }
}
