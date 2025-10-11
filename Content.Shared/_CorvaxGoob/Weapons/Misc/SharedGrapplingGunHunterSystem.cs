using System.Numerics;
using Content.Shared._CorvaxGoob.Weapons.Ranged.Components;
using Content.Shared.CombatMode;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CorvaxGoob.Weapons.Misc;

public abstract class SharedGrapplingGunHunterSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public const string GrapplingJoint = "corvax-goob-hunter-grappling";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrapplingGunHunterComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<GrapplingGunHunterComponent, ActivateInWorldEvent>(OnGunActivate);
        SubscribeLocalEvent<GrapplingGunHunterComponent, HandDeselectedEvent>(OnGunDeselected);
        SubscribeLocalEvent<GrapplingGunHunterComponent, GotUnequippedHandEvent>(OnGunGotUnequipped);
        SubscribeLocalEvent<GrapplingGunHunterComponent, ItemUnwieldedEvent>(OnGunUnwielded);
        SubscribeLocalEvent<GrapplingGunHunterComponent, JointRemovedEvent>(OnGunJointRemoved);
        SubscribeLocalEvent<GrapplingGunHunterComponent, ComponentShutdown>(OnGunShutdown);
        
        SubscribeLocalEvent<GrapplingHookHunterComponent, ProjectileEmbedEvent>(OnHookEmbed);
        SubscribeLocalEvent<GrapplingHookHunterComponent, ComponentShutdown>(OnHookShutdown);

        SubscribeLocalEvent<GrapplingHookedHunterComponent, DidEquipHandEvent>(OnHookedEquipHand);

        SubscribeAllEvent<RequestGrapplingHunterReelMessage>(OnGrapplingReel);
    }

    private void OnGunShot(EntityUid uid, GrapplingGunHunterComponent component, ref GunShotEvent args)
    {
        foreach (var (shotUid, _) in args.Ammo)
        {
            if (shotUid is null || !TryComp<GrapplingHookHunterComponent>(shotUid, out var hook))
                continue;

            component.Projectile = shotUid.Value;
            component.HookedTarget = null;
            component.Reeling = false;
            Dirty(uid, component);

            hook.Gun = uid;
            hook.Shooter = args.User;
            hook.Target = null;
            Dirty(shotUid.Value, hook);

            var visuals = EnsureComp<JointVisualsComponent>(shotUid.Value);
            visuals.Sprite = component.RopeSprite;
            visuals.OffsetA = new Vector2(0f, 0.5f);
            visuals.Target = GetNetEntity(uid);
            Dirty(shotUid.Value, visuals);
        }

        component.Stream = _audio.Stop(component.Stream);
        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, false, appearance);
        Dirty(uid, component);
    }

    private void OnGunActivate(EntityUid uid, GrapplingGunHunterComponent component, ActivateInWorldEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled || !args.Complex || component.Projectile is not { })
            return;

        _audio.PlayPredicted(component.CycleSound, uid, args.User);
        ReturnHook(uid, component, args.User);
        args.Handled = true;
    }

    private void OnGunDeselected(EntityUid uid, GrapplingGunHunterComponent component, HandDeselectedEvent args)
    {
        SetReeling(uid, component, false, args.User);
    }

    private void OnGunGotUnequipped(EntityUid uid, GrapplingGunHunterComponent component, GotUnequippedHandEvent args)
    {
        if (component.Projectile == null && component.HookedTarget == null)
            return;

        ReturnHook(uid, component, args.User);
    }

    private void OnGunUnwielded(EntityUid uid, GrapplingGunHunterComponent component, ref ItemUnwieldedEvent args)
    {
        if (!component.RequireWieldedHands)
            return;

        if (component.Projectile == null && component.HookedTarget == null)
            return;

        ReturnHook(uid, component, args.User);
    }

    private void OnGunJointRemoved(EntityUid uid, GrapplingGunHunterComponent component, JointRemovedEvent args)
    {
        if (args.Joint.ID != GrapplingJoint)
            return;

        if (component.Projectile == null && component.HookedTarget == null)
            return;

        ReturnHook(uid, component, null);
    }

    private void OnGunShutdown(EntityUid uid, GrapplingGunHunterComponent component, ref ComponentShutdown args)
    {
        ReturnHook(uid, component, null, false);
    }

    private void OnHookEmbed(EntityUid uid, GrapplingHookHunterComponent component, ref ProjectileEmbedEvent args)
    {
        if (component.Gun is not { } gun || !TryComp(gun, out GrapplingGunHunterComponent? gunComp))
            return;

        if (component.Target != null)
            return;

        if (!EntityManager.EntityExists(args.Embedded) || args.Embedded == gun)
            return;

        if (!TryComp<MobStateComponent>(args.Embedded, out _))
        {
            ReturnHook(gun, gunComp, null);
            return;
        }

        if (!TryComp<PhysicsComponent>(args.Embedded, out var physics) ||
            (physics.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0x0)
        {
            ReturnHook(gun, gunComp, null);
            return;
        }

        component.Target = args.Embedded;
        Dirty(uid, component);

        gunComp.Projectile = uid;
        gunComp.HookedTarget = args.Embedded;
        gunComp.Reeling = false;
        Dirty(gun, gunComp);

        var hooked = EnsureComp<GrapplingHookedHunterComponent>(args.Embedded);
        hooked.Gun = gun;
        hooked.Hook = uid;
        Dirty(args.Embedded, hooked);

        var gunXform = Transform(gun);
        var targetXform = Transform(args.Embedded);

        if (gunXform.MapID != targetXform.MapID)
        {
            ReturnHook(gun, gunComp, null);
            return;
        }

        var distance = Vector2.Distance(gunXform.MapPosition.Position, targetXform.MapPosition.Position);

        if (distance > gunComp.MaxRange)
        {
            ReturnHook(gun, gunComp, null);
            return;
        }

        var joint = _joints.CreateDistanceJoint(gun, args.Embedded, anchorA: new Vector2(0f, 0.5f), id: GrapplingJoint);
        joint.MinLength = gunComp.JointMinLength;
        var maxLength = MathF.Min(gunComp.MaxRange, MathF.Max(gunComp.JointMinLength, distance + gunComp.JointSlack));
        joint.MaxLength = maxLength;
        joint.Length = maxLength;
        joint.Stiffness = 1f;

        if (TryComp<JointComponent>(gun, out var jointComp))
            Dirty(gun, jointComp);
    }

    private void OnHookShutdown(EntityUid uid, GrapplingHookHunterComponent component, ref ComponentShutdown args)
    {
        if (component.Gun is not { } gun || !TryComp(gun, out GrapplingGunHunterComponent? gunComp))
            return;

        if (gunComp.Projectile != uid)
            return;

        ReturnHook(gun, gunComp, null);
    }

    private void OnHookedEquipHand(EntityUid uid, GrapplingHookedHunterComponent component, DidEquipHandEvent args)
    {
        if (component.Gun is not { } gun)
            return;

        if (!HasComp<GrapplingGunHunterComponent>(args.Equipped))
            return;

        if (!TryComp(gun, out GrapplingGunHunterComponent? gunComp))
            return;

        ReturnHook(gun, gunComp, args.User);
    }

    private void OnGrapplingReel(RequestGrapplingHunterReelMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!_hands.TryGetActiveItem(player, out var activeItem) ||
            !TryComp<GrapplingGunHunterComponent>(activeItem, out var grappling))
        {
            return;
        }

        if (grappling.Projectile == null || grappling.HookedTarget == null)
            return;

        if (msg.Reeling &&
            (!TryComp<CombatModeComponent>(player, out var combatMode) ||
             !combatMode.IsInCombatMode))
        {
            return;
        }

        SetReeling(activeItem.Value, grappling, msg.Reeling, player);
    }

    private void SetReeling(EntityUid uid, GrapplingGunHunterComponent component, bool value, EntityUid? user)
    {
        if (component.Reeling == value)
            return;

        if (value)
        {
            if (Timing.IsFirstTimePredicted)
                component.Stream = _audio.PlayPredicted(component.ReelSound, uid, user)?.Entity;
        }
        else
        {
            if (Timing.IsFirstTimePredicted)
                component.Stream = _audio.Stop(component.Stream);
        }

        component.Reeling = value;
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GrapplingGunHunterComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Projectile == null)
            {
                if (component.Reeling)
                    SetReeling(uid, component, false, null);
                continue;
            }

            if (!EntityManager.EntityExists(component.Projectile.Value))
            {
                ReturnHook(uid, component, null);
                continue;
            }

            if (component.HookedTarget == null)
            {
                if (component.Reeling)
                    SetReeling(uid, component, false, null);

                var gunXform = Transform(uid);
                var projectileXform = Transform(component.Projectile.Value);

                if (projectileXform.MapID != gunXform.MapID)
                {
                    ReturnHook(uid, component, null);
                    continue;
                }

                var distance = Vector2.Distance(gunXform.MapPosition.Position, projectileXform.MapPosition.Position);

                if (distance > component.MaxRange)
                    ReturnHook(uid, component, null);

                continue;
            }

            if (!EntityManager.EntityExists(component.HookedTarget.Value))
            {
                ReturnHook(uid, component, null);
                continue;
            }

            if (!TryComp<JointComponent>(uid, out var jointComp) ||
                !jointComp.GetJoints.TryGetValue(GrapplingJoint, out var joint) ||
                joint is not DistanceJoint distanceJoint)
            {
                SetReeling(uid, component, false, null);
                continue;
            }

            if (!component.Reeling)
                continue;

            distanceJoint.MaxLength = MathF.Max(component.JointMinLength, distanceJoint.MaxLength - component.ReelRate * frameTime);
            distanceJoint.Length = MathF.Min(distanceJoint.MaxLength, distanceJoint.Length);

            _physics.WakeBody(joint.BodyAUid);
            _physics.WakeBody(joint.BodyBUid);

            Dirty(uid, jointComp);

            if (distanceJoint.MaxLength <= component.JointMinLength + component.PullStopTolerance)
            {
                SetReeling(uid, component, false, null);
            }
        }
    }

    private void ReturnHook(EntityUid uid, GrapplingGunHunterComponent component, EntityUid? user, bool restoreAmmo = true)
    {
        var projectile = component.Projectile;
        var target = component.HookedTarget;
        var hadProjectile = projectile is not null;
        var showLoaded = restoreAmmo && hadProjectile;

        SetReeling(uid, component, false, user);

        component.Projectile = null;
        component.HookedTarget = null;
        Dirty(uid, component);

        if (TryComp<JointComponent>(uid, out var jointComp) && jointComp.GetJoints.ContainsKey(GrapplingJoint))
        {
            _joints.RemoveJoint(uid, GrapplingJoint);
        }

        if (target is { } targetUid && EntityManager.EntityExists(targetUid) &&
            TryComp<GrapplingHookedHunterComponent>(targetUid, out var hooked) &&
            hooked.Gun == uid)
        {
            RemCompDeferred<GrapplingHookedHunterComponent>(targetUid);
        }

        TryComp<AppearanceComponent>(uid, out var appearance);
        _appearance.SetData(uid, SharedTetherGunSystem.TetherVisualsStatus.Key, showLoaded, appearance);

        if (_netManager.IsServer && projectile is { } proj && EntityManager.EntityExists(proj))
        {
            QueueDel(proj);
        }

        if (restoreAmmo && hadProjectile)
        {
            _gun.ChangeBasicEntityAmmoCount(uid, 1);

            var updateAmmoEvent = new UpdateClientAmmoEvent();
            RaiseLocalEvent(uid, ref updateAmmoEvent);
        }
    }

    [Serializable, NetSerializable]
    protected sealed class RequestGrapplingHunterReelMessage : EntityEventArgs
    {
        public bool Reeling;

        public RequestGrapplingHunterReelMessage(bool reeling)
        {
            Reeling = reeling;
        }
    }
}
