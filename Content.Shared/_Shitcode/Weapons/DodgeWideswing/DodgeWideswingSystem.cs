// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Shared._Goobstation.Weapons.DodgeWideswing;

public sealed class DodgeWideswingSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<DodgeWideswingComponent, BeforeDamageChangedEvent>(OnDamageChanged);
    }

    


}
