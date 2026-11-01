// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Procedural.Components;
using Content.Shared._Lavaland.Weapons.StaffOfStorms;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.Weapons;

/// <summary>
/// Staff of storms: summons or disperses an ash storm on the lavaland planet.
/// </summary>
public sealed class StaffOfStormsSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaffOfStormsActionEvent>(OnAction);
    }

    private void OnAction(StaffOfStormsActionEvent args)
    {
        if (args.Handled)
            return;

        Entity<StaffOfStormsComponent>? staff = null;
        foreach (var held in _hands.EnumerateHeld(args.Performer))
        {
            if (TryComp<StaffOfStormsComponent>(held, out var staffComp))
            {
                staff = (held, staffComp);
                break;
            }
        }

        if (staff == null)
            return;

        args.Handled = true;
        UseStaff(args.Performer, staff.Value);
    }

    private void UseStaff(EntityUid user, Entity<StaffOfStormsComponent> staff)
    {
        var mapId = Transform(user).MapID;
        var mapUid = _map.GetMap(mapId);

        if (!HasComp<LavalandMapComponent>(mapUid))
        {
            _popup.PopupEntity(Loc.GetString("staff-of-storms-wrong-place"), staff, user);
            return;
        }

        var comp = staff.Comp;
        var stormProto = _proto.Index(comp.Storm);

        if (TryComp<WeatherComponent>(mapUid, out var weather) && weather.Weather.ContainsKey(comp.Storm))
        {
            _weather.SetWeather(mapId, null, null);
            _popup.PopupEntity(Loc.GetString("staff-of-storms-dispersed"), staff, user);
            return;
        }

        _weather.SetWeather(mapId, stormProto, _timing.CurTime + TimeSpan.FromSeconds(comp.StormDuration));
        _audio.PlayPvs("/Audio/Effects/explosionfar.ogg", staff, AudioParams.Default.WithVolume(-4f));
        _popup.PopupEntity(Loc.GetString("staff-of-storms-summoned"), staff, user);
    }
}
