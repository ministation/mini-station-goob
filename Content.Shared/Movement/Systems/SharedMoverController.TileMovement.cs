using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared._vg.TileMovement;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private TimeSpan CurrentTileMoveTime => PhysicsSystem.EffectiveCurTime ?? Timing.CurTime;

    public bool HandleTileMovement(
        EntityUid uid,
        EntityUid physicsUid,
        TileMovementComponent tileMovement,
        InputMoverComponent inputMover,
        PhysicsComponent physicsComponent,
        TransformComponent targetTransform,
        ContentTileDefinition? tileDef,
        MovementRelayTargetComponent? relayTarget,
        float frameTime)
    {
        if (tileMovement.WasWeightlessLastTick)
        {
            InitializeSlideToCenter(physicsUid, tileMovement);
            UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
        }
        else if (StripWalk(inputMover.HeldMoveButtons) == MoveButtons.None && !tileMovement.SlideActive)
        {
            var movementVelocity = physicsComponent.LinearVelocity;
            var movementSpeedComponent = ModifierQuery.CompOrNull(uid);
            var friction = GetTileEntityFriction(inputMover, movementSpeedComponent, tileDef);
            var minimumFrictionSpeed = movementSpeedComponent?.MinimumFrictionSpeed ??
                MovementSpeedModifierComponent.DefaultMinimumFrictionSpeed;
            Friction(minimumFrictionSpeed, frameTime, friction, ref movementVelocity);
            PhysicsSystem.SetLinearVelocity(physicsUid, movementVelocity, body: physicsComponent);
            PhysicsSystem.SetAngularVelocity(physicsUid, 0, body: physicsComponent);
        }
        else
        {
            if (MobMoverQuery.TryGetComponent(uid, out var mobMover) &&
                TryGetSound(false, uid, inputMover, mobMover, targetTransform, out var sound, tileDef: tileDef))
            {
                var soundModifier = inputMover.Sprinting ? 3.5f : 1.5f;
                var volume = sound.Params.Volume + soundModifier;
                var audioParams = sound.Params
                    .WithVolume(volume)
                    .WithVariation(sound.Params.Variation ?? mobMover.FootstepVariation);

                if (relayTarget != null)
                    _audio.PlayPredicted(sound, uid, relayTarget.Source, audioParams);
                else
                    _audio.PlayPredicted(sound, uid, uid, audioParams);
            }

            if (tileMovement.SlideActive)
            {
                var movementSpeed = GetTileEntityMoveSpeed(uid, inputMover.Sprinting);

                if (CheckForSlideEnd(
                        StripWalk(inputMover.HeldMoveButtons),
                        targetTransform,
                        tileMovement,
                        movementSpeed))
                {
                    EndSlide(uid, tileMovement);

                    if (StripWalk(inputMover.HeldMoveButtons) != MoveButtons.None)
                    {
                        InitializeSlide(physicsUid, tileMovement, inputMover);
                        UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
                        tileMovement.FailureSlideActive = false;
                    }
                    else if (!tileMovement.FailureSlideActive && !targetTransform.LocalPosition.EqualsApprox(tileMovement.Destination, 0.04))
                    {
                        InitializeSlideToTarget(physicsUid, tileMovement, targetTransform.LocalPosition, MoveButtons.None);
                        UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
                        tileMovement.FailureSlideActive = true;
                    }
                    else
                    {
                        ForceSnapToTile(uid, inputMover);
                        tileMovement.FailureSlideActive = false;
                    }
                }
                else if (tileMovement.Origin.EntityId != targetTransform.ParentUid)
                {
                    var previousButtons = tileMovement.CurrentSlideMoveButtons;
                    var previousInitialKeyDownTime = tileMovement.MovementKeyInitialDownTime;
                    InitializeSlideToCenter(physicsUid, tileMovement);
                    tileMovement.CurrentSlideMoveButtons = previousButtons;
                    tileMovement.MovementKeyInitialDownTime = previousInitialKeyDownTime;
                    UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
                }
                else
                {
                    UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
                }
            }
            else
            {
                InitializeSlide(physicsUid, tileMovement, inputMover);
                UpdateSlide(physicsUid, physicsUid, tileMovement, inputMover);
            }

            if (!NoRotateQuery.HasComponent(uid) && !tileMovement.FailureSlideActive)
            {
                if (tileMovement.SlideActive && TryComp(inputMover.RelativeEntity, out TransformComponent? parentTransform))
                {
                    var delta = tileMovement.Destination - tileMovement.Origin.Position;
                    var worldRot = _transform.GetWorldRotation(parentTransform).RotateVec(delta).ToWorldAngle();
                    _transform.SetWorldRotation(targetTransform, worldRot);
                }
            }
        }

        tileMovement.LastTickLocalCoordinates = targetTransform.LocalPosition;
        return true;
    }

    private bool CheckForSlideEnd(
        MoveButtons pressedButtons,
        TransformComponent transform,
        TileMovementComponent tileMovement,
        float movementSpeed)
    {
        var distanceToDestination = (tileMovement.Destination - tileMovement.Origin.Position).Length();
        var minPressedTime = Math.Min((1.05f / movementSpeed) * distanceToDestination, 20);
        var destinationTolerance = movementSpeed / 100f;
        var reachedDestination = transform.LocalPosition.EqualsApprox(tileMovement.Destination, destinationTolerance);
        var stoppedPressing = pressedButtons != tileMovement.CurrentSlideMoveButtons;
        var minDurationPassed = CurrentTileMoveTime - tileMovement.MovementKeyInitialDownTime >= TimeSpan.FromSeconds(minPressedTime);
        var noProgress = tileMovement.LastTickLocalCoordinates != null
            && transform.LocalPosition.EqualsApprox(tileMovement.LastTickLocalCoordinates.Value, destinationTolerance / 3);
        var hardDurationLimitPassed = CurrentTileMoveTime - tileMovement.MovementKeyInitialDownTime >= TimeSpan.FromSeconds(minPressedTime) * 3;
        return reachedDestination || (stoppedPressing && (minDurationPassed || noProgress)) || hardDurationLimitPassed;
    }

    private void InitializeSlideToTarget(
        EntityUid uid,
        TileMovementComponent tileMovement,
        Vector2 localPositionTarget,
        MoveButtons heldMoveButtons)
    {
        var transform = Transform(uid);
        var localPosition = transform.LocalPosition;

        tileMovement.SlideActive = true;
        tileMovement.Origin = new EntityCoordinates(transform.ParentUid, localPosition);
        tileMovement.Destination = SnapCoordinatesToTile(localPositionTarget);
        tileMovement.MovementKeyInitialDownTime = CurrentTileMoveTime;
        tileMovement.CurrentSlideMoveButtons = heldMoveButtons;
    }

    private void InitializeSlideToCenter(EntityUid uid, TileMovementComponent tileMovement)
    {
        var localPosition = Transform(uid).LocalPosition;
        InitializeSlideToTarget(uid, tileMovement, SnapCoordinatesToTile(localPosition), MoveButtons.None);
    }

    private void InitializeSlide(EntityUid uid, TileMovementComponent tileMovement, InputMoverComponent inputMover)
    {
        var transform = Transform(uid);
        var localPosition = transform.LocalPosition;
        var offset = DirVecForButtons(inputMover.HeldMoveButtons);
        offset = inputMover.TargetRelativeRotation.RotateVec(offset);
        InitializeSlideToTarget(uid, tileMovement, localPosition + offset, StripWalk(inputMover.HeldMoveButtons));
    }

    private void UpdateSlide(
        EntityUid uid,
        EntityUid physicsUid,
        TileMovementComponent tileMovement,
        InputMoverComponent inputMover)
    {
        var targetTransform = Transform(uid);

        if (!PhysicsQuery.TryComp(physicsUid, out var physicsComponent))
            return;

        var moveSpeedComponent = ModifierQuery.CompOrNull(uid);
        var parentRotation = Angle.Zero;
        if (XformQuery.TryGetComponent(targetTransform.GridUid, out var relativeTransform))
            parentRotation = _transform.GetWorldRotation(relativeTransform);

        var movementVelocity = tileMovement.Destination - targetTransform.LocalPosition;
        movementVelocity.Normalize();
        if (inputMover.Sprinting)
        {
            movementVelocity *= moveSpeedComponent?.CurrentSprintSpeed ??
                MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
        }
        else
        {
            movementVelocity *= moveSpeedComponent?.CurrentWalkSpeed ??
                MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
        }

        movementVelocity = parentRotation.RotateVec(movementVelocity);
        PhysicsSystem.SetLinearVelocity(physicsUid, movementVelocity, body: physicsComponent);
        PhysicsSystem.SetAngularVelocity(physicsUid, 0, body: physicsComponent);
    }

    private void EndSlide(EntityUid uid, TileMovementComponent tileMovement)
    {
        tileMovement.SlideActive = false;
        tileMovement.MovementKeyInitialDownTime = null;
        var physicsComponent = PhysicsQuery.GetComponent(uid);
        PhysicsSystem.SetLinearVelocity(uid, Vector2.Zero, body: physicsComponent);
        PhysicsSystem.SetAngularVelocity(uid, 0, body: physicsComponent);
    }

    private void ForceSnapToTile(EntityUid uid, InputMoverComponent inputMover)
    {
        if (!TryComp(inputMover.RelativeEntity, out TransformComponent? _))
            return;

        var targetTransform = Transform(uid);
        var localCoordinates = targetTransform.LocalPosition;
        var snappedCoordinates = SnapCoordinatesToTile(localCoordinates);

        if (!localCoordinates.EqualsApprox(snappedCoordinates) && targetTransform.ParentUid.IsValid())
            _transform.SetLocalPosition(uid, snappedCoordinates);

        PhysicsSystem.WakeBody(uid);
    }

    private float GetTileEntityMoveSpeed(EntityUid uid, bool sprinting)
    {
        var moveSpeedComponent = ModifierQuery.CompOrNull(uid);
        if (sprinting)
            return moveSpeedComponent?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

        return moveSpeedComponent?.CurrentWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
    }

    private float GetTileEntityFriction(
        InputMoverComponent inputMover,
        MovementSpeedModifierComponent? movementSpeedComponent,
        ContentTileDefinition? tileDef)
    {
        if (inputMover.HeldMoveButtons != MoveButtons.None || movementSpeedComponent?.FrictionNoInput == null)
        {
            return tileDef?.MobFriction ??
                movementSpeedComponent?.Friction ?? MovementSpeedModifierComponent.DefaultFriction;
        }

        return movementSpeedComponent.FrictionNoInput;
    }

    private static MoveButtons StripWalk(MoveButtons input)
    {
        return input & ~MoveButtons.Walk;
    }

    public static Vector2 SnapCoordinatesToTile(Vector2 input)
    {
        return new Vector2((int)Math.Floor(input.X) + 0.5f, (int)Math.Floor(input.Y) + 0.5f);
    }
}
