// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Shatters the mayhem bottle, enraging all aggressive fauna in range at each other.
/// </summary>
public sealed class MayhemBottleSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MayhemBottleComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<MayhemBottleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var comp = ent.Comp;
        var coords = Transform(args.User).Coordinates;

        var enraged = _lookup.GetEntitiesInRange<AggressiveComponent>(coords, comp.Range);
        var alive = new List<EntityUid>();
        foreach (var mob in enraged)
        {
            if (TryComp<MobStateComponent>(mob, out var state) && state.CurrentState == MobState.Dead)
                continue;
            alive.Add(mob);
        }

        foreach (var mob in alive)
        {
            if (!TryComp<AggressiveComponent>(mob, out var aggressive))
                continue;

            foreach (var other in alive)
            {
                if (other == mob)
                    continue;
                aggressive.Aggressors.Add(other);
            }
        }

        _audio.PlayPvs("/Audio/Effects/glassbr1.ogg", ent, AudioParams.Default.WithVolume(-2f));
        _popup.PopupEntity(Loc.GetString("mayhem-bottle-shattered"), ent);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):user} shattered a mayhem bottle at {coords:coords}, enraging {alive.Count} fauna");

        QueueDel(ent);
    }
}
