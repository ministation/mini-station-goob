// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Damage.Components;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Camera;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Sprite;
using Content.Shared.Throwing;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Handles <see cref="MegafaunaSwoopComponent"/>: keeps the boss flying to the live target for the
/// blink delay, makes it untouchable on the way and drops the slam spawner on the landing tile.
/// The teleport, the dash sound and the fire trail along the dash stay in <see cref="MegafaunaBlinkSystem"/>.
/// </summary>
public sealed class MegafaunaSwoopSystem : EntitySystem
{
    private const string DrakeLayer = "enum.DamageStateVisualLayers.Base";
    private const float SwoopScale = 0.7f;
    private const float SwoopAlpha = 0.85f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MegafaunaSwoopComponent, MegafaunaBlinkActionEvent>(OnSwoopAction);
        SubscribeLocalEvent<MegafaunaSwoopComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnSwoopAction(Entity<MegafaunaSwoopComponent> ent, ref MegafaunaBlinkActionEvent args)
    {
        // Handled is left alone on purpose: the blink handler of the same event still has to
        // schedule the landing teleport and the fire trail.
        if (ent.Comp.LandAt != null
            || !args.Target.IsValid(EntityManager)
            || !TryComp<MegafaunaBlinkComponent>(ent, out var blink))
            return;

        var flight = (float) blink.Delay.TotalSeconds;
        if (flight <= 0f)
            return;

        var comp = ent.Comp;
        comp.OwnsGodmode = !EnsureComp<GodmodeComponent>(ent.Owner, out _);
        comp.Origin = _xform.GetMapCoordinates(ent.Owner);
        comp.Target = args.Target;
        comp.FlightSeconds = flight;
        comp.LandAt = _timing.CurTime + blink.Delay;

        // TG keeps the swoop aimed at the target's current tile, not at the tile it was cast on.
        comp.TargetEnt = null;
        if (TryComp<MegafaunaAiTargetingComponent>(ent.Owner, out var targeting))
            comp.TargetEnt = targeting.TargetEnt;

        var coords = GetLandingCoords(ent.Owner, comp);
        comp.LandingMarkerEnt = Spawn(comp.LandingMarker, coords);

        // The boss rises: smaller, slightly transparent, with a shadow left on the ground.
        _scale.SetSpriteScale(ent.Owner, new Vector2(SwoopScale));
        var tint = EnsureComp<RandomSpriteComponent>(ent.Owner);
        tint.Selected[DrakeLayer] = (comp.FlightState, Color.White.WithAlpha(SwoopAlpha));
        Dirty(ent.Owner, tint);
        comp.OwnsVisuals = true;
        comp.ShadowEnt = Spawn(comp.Shadow, Transform(ent.Owner).Coordinates);

        // A swooping boss cannot cast its other abilities, TG freezes the mob for the attack.
        if (TryComp<MegafaunaAiComponent>(ent.Owner, out var ai))
        {
            comp.AiWasActive = ai.Active;
            comp.PausedAi = true;
            ai.Active = false;
            Dirty(ent.Owner, ai);
        }

        if (TryComp<PhysicsComponent>(ent.Owner, out var body))
            _physics.SetLinearVelocity(ent.Owner, Vector2.Zero, body: body);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MegafaunaSwoopComponent>();
        while (query.MoveNext(out var uid, out var swoop))
        {
            if (swoop.LandAt is not { } landAt)
                continue;

            var landing = GetLandingCoords(uid, swoop);
            var target = _xform.ToMapCoordinates(landing);
            if (swoop.FlightSeconds <= 0f || target.MapId != swoop.Origin.MapId)
                continue;

            // The blink system performs the actual teleport on its own schedule. Point it at the
            // live tile every tick, so it cannot land the boss on the tile the swoop was cast on.
            if (TryComp<MegafaunaActiveBlinkComponent>(uid, out var pendingBlink)
                && pendingBlink.BlinkTime > now)
            {
                pendingBlink.Coordinates = landing;
                Dirty(uid, pendingBlink);
            }

            if (now >= landAt)
            {
                Land((uid, swoop), landing);
                continue;
            }

            if (swoop.LandingMarkerEnt is { } marker && !TerminatingOrDeleted(marker))
                _xform.SetCoordinates(marker, landing);

            if (swoop.ShadowEnt is { } shadow && !TerminatingOrDeleted(shadow))
                _xform.SetCoordinates(shadow, Transform(uid).Coordinates);

            var left = (float) (landAt - now).TotalSeconds;
            var progress = Math.Clamp(1f - left / swoop.FlightSeconds, 0f, 1f);
            _xform.SetMapCoordinates(uid, new MapCoordinates(
                Vector2.Lerp(swoop.Origin.Position, target.Position, progress), swoop.Origin.MapId));
        }
    }

    private void Land(Entity<MegafaunaSwoopComponent> ent, EntityCoordinates coords)
    {
        _xform.SetCoordinates(ent.Owner, coords);
        Finish(ent);

        // TG slams every mob around the drake except the drake itself, while our damage tiles hit
        // everything standing on them, so the landing boss gets a short damage square immunity.
        EnsureComp<DamageSquareImmunityComponent>(ent.Owner).ImmunityEndTime =
            _timing.CurTime + TimeSpan.FromSeconds(0.5f);

        SpawnAtPosition(ent.Comp.Slam, coords);

        ThrowVictims(ent.Owner, coords);
        ShakeCamera(coords);
    }

    /// <summary>
    /// TG throws everyone in orange(1) three tiles away from the landing drake.
    /// </summary>
    private void ThrowVictims(EntityUid boss, EntityCoordinates coords)
    {
        var epicenterMap = _xform.ToMapCoordinates(coords);
        var epicenter = epicenterMap.Position;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var victim, out var mobState, out var xform))
        {
            if (victim == boss
                || _mobState.IsDead(victim, mobState)
                || xform.MapID != epicenterMap.MapId)
                continue;

            var delta = _xform.GetWorldPosition(xform) - epicenter;
            // Tg throws victims from the whole 3x3 around the drake, not only from its own tile.
            if (MathF.Abs(delta.X) > 1.5f || MathF.Abs(delta.Y) > 1.5f)
                continue;

            var dir = delta.Length() > 0.05f
                ? Vector2.Normalize(delta)
                : new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f)).Normalized();

            _throwing.TryThrow(victim, dir * 3f, baseThrowSpeed: 8f, user: boss);
        }
    }

    /// <summary>
    /// TG shakes the camera of everyone within seven tiles of the impact.
    /// </summary>
    private void ShakeCamera(EntityCoordinates coords)
    {
        var epicenter = _xform.ToMapCoordinates(coords);
        var players = Filter.Empty();
        players.AddInRange(epicenter, 7f, _playerManager, EntityManager);

        foreach (var session in players.Recipients)
        {
            if (session.AttachedEntity is not { } player)
                continue;

            _recoil.KickCamera(player, new Vector2(_random.NextFloat(-6f, 6f), _random.NextFloat(-6f, 6f)));
        }
    }

    private EntityCoordinates GetLandingCoords(EntityUid uid, MegafaunaSwoopComponent swoop)
    {
        if (swoop.TargetEnt is { } target
            && !TerminatingOrDeleted(target)
            && TryComp<TransformComponent>(target, out var targetXform)
            && targetXform.MapID == Transform(uid).MapID)
            return targetXform.Coordinates.SnapToGrid(EntityManager);

        return swoop.Target.SnapToGrid(EntityManager);
    }

    private void Finish(Entity<MegafaunaSwoopComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.OwnsGodmode)
            RemComp<GodmodeComponent>(ent.Owner);
        comp.OwnsGodmode = false;

        if (comp.OwnsVisuals)
        {
            if (TryComp<RandomSpriteComponent>(ent.Owner, out var tint))
            {
                tint.Selected[DrakeLayer] = (comp.BaseState, null);
                Dirty(ent.Owner, tint);
            }

            RemComp<ScaleVisualsComponent>(ent.Owner);
        }
        comp.OwnsVisuals = false;

        if (comp.PausedAi)
        {
            if (TryComp<MegafaunaAiComponent>(ent.Owner, out var ai))
            {
                ai.Active = comp.AiWasActive;
                Dirty(ent.Owner, ai);
            }
        }
        comp.PausedAi = false;

        if (!TerminatingOrDeleted(comp.LandingMarkerEnt))
            QueueDel(comp.LandingMarkerEnt);
        comp.LandingMarkerEnt = null;

        if (!TerminatingOrDeleted(comp.ShadowEnt))
            QueueDel(comp.ShadowEnt);
        comp.ShadowEnt = null;

        comp.LandAt = null;
        comp.TargetEnt = null;
    }

    private void OnShutdown(Entity<MegafaunaSwoopComponent> ent, ref ComponentShutdown args)
    {
        // A terminating boss loses the components anyway, only a live one would keep godmode and the AI pause.
        if (TerminatingOrDeleted(ent.Owner))
            return;

        Finish(ent);
    }
}
