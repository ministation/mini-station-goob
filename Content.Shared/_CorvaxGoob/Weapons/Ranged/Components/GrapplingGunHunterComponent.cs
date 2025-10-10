using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._CorvaxGoob.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GrapplingGunHunterComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ReelRate = 2.5f;

    [DataField, AutoNetworkedField]
    public float JointSlack = 0.2f;

    [DataField, AutoNetworkedField]
    public float JointMinLength = 0.35f;

    [DataField, AutoNetworkedField]
    public float PullStopTolerance = 0.05f;

    [DataField, AutoNetworkedField]
    public float MaxRange = 12f;

    [DataField, AutoNetworkedField]
    public EntityUid? Projectile;

    [DataField, AutoNetworkedField]
    public EntityUid? HookedTarget;

    [DataField, AutoNetworkedField]
    public bool RequireWieldedHands = true;

    [ViewVariables(VVAccess.ReadWrite), DataField("reeling"), AutoNetworkedField]
    public bool Reeling;

    [ViewVariables(VVAccess.ReadWrite), DataField("reelSound"), AutoNetworkedField]
    public SoundSpecifier? ReelSound = new SoundPathSpecifier("/Audio/Weapons/reel.ogg")
    {
        Params = AudioParams.Default.WithLoop(true)
    };

    [ViewVariables(VVAccess.ReadWrite), DataField("cycleSound"), AutoNetworkedField]
    public SoundSpecifier? CycleSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/kinetic_reload.ogg");

    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("_CorvaxGoob/Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    public EntityUid? Stream;
}
