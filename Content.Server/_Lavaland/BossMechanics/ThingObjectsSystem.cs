// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.BossMechanics;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Marker for the spike field spawned by The Thing.
/// </summary>
[RegisterComponent]
public sealed partial class ThingSpikeComponent : Component;

/// <summary>
/// A patch of stomach acid that burns whoever steps into it.
/// </summary>
[RegisterComponent]
public sealed partial class ThingAcidComponent : Component
{
    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>
/// Acid glob that falls for a moment and then splashes into a 3x3 patch of acid.
/// </summary>
[RegisterComponent]
public sealed partial class ThingAcidGlobComponent : Component
{
    [DataField]
    public float FallTime = 0.85f;

    [ViewVariables]
    public TimeSpan LandTime;
}

/// <summary>
/// Lifetime of the acid globs and puddles of The Thing.
/// </summary>
public sealed class ThingObjectsSystem : EntitySystem
{
    private static readonly SoundPathSpecifier SizzleSound = new("/Audio/Effects/sizzle.ogg");
    private static readonly SoundPathSpecifier SpikeSound = new("/Audio/Animals/Blob/blobattack.ogg");

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThingAcidGlobComponent, MapInitEvent>(OnGlobMapInit);
    }

    private void OnGlobMapInit(Entity<ThingAcidGlobComponent> ent, ref MapInitEvent args)
        => ent.Comp.LandTime = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.FallTime);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var globQuery = EntityQueryEnumerator<ThingAcidGlobComponent>();
        while (globQuery.MoveNext(out var uid, out var glob))
        {
            if (now < glob.LandTime)
                continue;

            Splash(Transform(uid).Coordinates);
            QueueDel(uid);
        }

        var acidQuery = EntityQueryEnumerator<ThingAcidComponent>();
        while (acidQuery.MoveNext(out var uid, out var acid))
        {
            if (now < acid.NextCheck)
                continue;

            acid.NextCheck = now + TimeSpan.FromSeconds(0.3);

            if (TryBurn(uid, Transform(uid).Coordinates))
                QueueDel(uid);
        }
    }

    private void Splash(EntityCoordinates coords)
    {
        _audio.PlayPvs(SizzleSound, coords);

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
                Spawn("ThingAcid", coords.Offset(new Vector2(x, y)));
        }
    }

    private bool TryBurn(EntityUid acid, EntityCoordinates coords)
    {
        var damage = new DamageSpecifier { DamageDict = { { "Burn", 25f } } };

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.4f))
        {
            if (HasComp<MegafaunaAiComponent>(mob))
                continue;

            _damageable.TryChangeDamage(mob.Owner, damage);
            _popup.PopupEntity(Loc.GetString("thing-acid-burn"), mob.Owner, mob.Owner);
            _audio.PlayPvs(SizzleSound, acid);
            return true;
        }

        return false;
    }
}
