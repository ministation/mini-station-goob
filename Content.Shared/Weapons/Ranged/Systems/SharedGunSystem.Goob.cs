using Content.Goobstation.Common.Weapons.NoWieldNeeded;
using Content.Shared._CorvaxGoob.Skills;
using Content.Shared._ES.Camera;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private SharedSkillsSystem _skills = default!;
    [Dependency] private ESScreenshakeSystem _shake = default!;

    private const float SpreadWithoutSkill = MathHelper.PiOver6; // CorvaxGoob-Skills

    private void GunScreenshake(EntityUid user, Entity<GunComponent> gun)
    {
        var rotation = new ESScreenshakeParameters(0.085f * gun.Comp.CameraRecoilScalarModified, 1.2f, 0.008f);
        _shake.Screenshake(user, null, rotation);
    }

    // CorvaxGoob-Skills-Start
    private void ApplySkillSpread(Entity<GunComponent> gun, EntityUid? user, ref Angle mapAngle, ref Angle angle)
    {
        if (!gun.Comp.RequiresSkill || user is null ||
            _skills.HasSkill(user.Value, Skills.Shooting) ||
            HasComp<NoWieldNeededComponent>(user.Value))
            return;

        var spread = -SpreadWithoutSkill / 2 + GetRandom(gun).NextFloat() * SpreadWithoutSkill;

        mapAngle += spread;
        angle += spread;
    }
    // CorvaxGoob-Skills-End
}
