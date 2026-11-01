// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Handles Legion splitting into smaller legions on death.
/// </summary>
public sealed class LegionSplitSystem : EntitySystem
{
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
        var protoId = comp.SpawnOnDeath;
        var coords = Transform(ent).Coordinates;

        for (var i = 0; i < comp.SpawnCount; i++)
        {
            var offset = _random.NextVector2Box(-0.8f, -0.8f, 0.8f, 0.8f);
            var child = Spawn(protoId, coords.Offset(offset));

            if (TryComp<LegionSplitComponent>(child, out var childSplit))
                childSplit.SplitsRemaining = comp.SplitsRemaining - 1;
        }

        _audio.PlayPvs("/Audio/_Lavaland/Mobs/legion_spawn.ogg", ent, AudioParams.Default.WithVolume(-4f));
        _popup.PopupEntity(Loc.GetString("legion-split"), ent);
    }
}
