// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Lavaland.Aggression;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Handles Legion splitting into smaller legions on death.
/// </summary>
public sealed class LegionSplitSystem : EntitySystem
{
    [Dependency] private readonly AggressorsSystem _aggressors = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LegionSplitComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<LegionSplitComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.SplitsRemaining <= 0)
            return;

        var comp = ent.Comp;
        var coords = Transform(ent).Coordinates;

        var protos = comp.SpawnOnDeathList;
        if (protos is not { Count: > 0 })
        {
            if (comp.SpawnOnDeath is not { } protoId)
                return;

            protos = new List<EntProtoId>(comp.SpawnCount);
            for (var i = 0; i < comp.SpawnCount; i++)
                protos.Add(protoId);
        }

        foreach (var protoId in protos)
        {
            var offset = _random.NextVector2Box(-0.8f, -0.8f, 0.8f, 0.8f);
            var child = Spawn(protoId, coords.Offset(offset));

            // The boss HUD follows the nearest boss with live aggressors, so the shards have
            // to inherit the fight instead of looking like a finished boss.
            if (TryComp<AggressiveComponent>(ent, out var aggressive)
                && TryComp<AggressiveComponent>(child, out var childAggressive))
            {
                foreach (var aggressor in aggressive.Aggressors)
                    _aggressors.AddAggressor((child, childAggressive), aggressor);
            }

            if (TryComp<LegionSplitComponent>(child, out var childSplit))
                childSplit.SplitsRemaining = comp.SplitsRemaining - 1;
        }

        _audio.PlayPvs("/Audio/_Lavaland/Mobs/legion_spawn.ogg", ent, AudioParams.Default.WithVolume(-4f));
        _popup.PopupEntity(Loc.GetString("legion-split"), ent);
    }
}
