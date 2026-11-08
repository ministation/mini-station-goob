// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Sprite;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// TG's frost miner enrage: below a quarter health it roars, heals back to full health,
/// locks movement for a moment and switches to its second sprite. Ported from
/// TG's demonic_frost_miner/check_enraged.
/// </summary>
[RegisterComponent]
public sealed partial class FrostMinerEnrageComponent : Component
{
    /// <summary>
    /// Health fraction at which the enrage triggers.
    /// </summary>
    [DataField]
    public float HealthFraction = 0.25f;

    /// <summary>
    /// Sprite state switched to on enrage, on <see cref="Layer"/>.
    /// </summary>
    [DataField]
    public string EnragedState = "demonic_miner_phase2";

    [DataField]
    public string Layer = "enum.DamageStateVisualLayers.Base";

    /// <summary>
    /// How long the boss is stunned while enraging.
    /// </summary>
    [DataField]
    public TimeSpan LockDuration = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/explosion3.ogg");

    [DataField]
    public SoundSpecifier? SecondSound = new SoundPathSpecifier("/Audio/_Lavaland/Effects/meteorimpact.ogg");

    [ViewVariables]
    public bool Enraged;
}

public sealed class FrostMinerEnrageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FrostMinerEnrageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<FrostMinerEnrageComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Enraged || !args.DamageIncreased)
            return;

        if (!_threshold.TryGetThresholdForState(ent.Owner, MobState.Dead, out var threshold)
            || threshold == null)
            return;

        if (args.Damageable.TotalDamage < threshold.Value * (1f - ent.Comp.HealthFraction))
            return;

        Enrage(ent);
    }

    private void Enrage(Entity<FrostMinerEnrageComponent> ent)
    {
        var comp = ent.Comp;
        comp.Enraged = true;

        _damageable.ClearAllDamage(ent.Owner);

        _stun.TryAddStunDuration(ent.Owner, comp.LockDuration);
        _audio.PlayPvs(comp.Sound, ent.Owner);

        // shortcut: server-side sprite state swap via RandomSprite, until megafauna get a visualizer
        var sprite = EnsureComp<RandomSpriteComponent>(ent);
        sprite.Selected[comp.Layer] = (comp.EnragedState, (Color?) null);
        Dirty(ent, sprite);

        Timer.Spawn(comp.LockDuration, () =>
        {
            if (!TerminatingOrDeleted(ent.Owner))
                _audio.PlayPvs(comp.SecondSound, ent.Owner);
        });
    }
}
