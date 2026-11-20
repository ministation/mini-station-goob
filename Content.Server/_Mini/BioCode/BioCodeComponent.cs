// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server._Mini.BioCode;

[RegisterComponent]
public sealed partial class BioCodeComponent : Component
{
    [DataField("autoRecordDNA")]
    public bool AutoRecordDNA = false;

    [DataField("allowOverwriting")]
    public bool AllowOverwriting = true;

    [DataField("beepInterval")]
    public float BeepInterval = 1;

    [DataField("beepSound")]
    public SoundSpecifier? BeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");

    [DataField("damagePerRecord")]
    public DamageSpecifier DamagePerRecord = new ()
    {
        DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Piercing", 2 },
        },
    };

    [DataField("delay")]
    public float Delay = 3f;

    [DataField("hardDefense")]
    public bool HardDefense = false;

    [DataField("initialBeepDelay")]
    public float? InitialBeepDelay = 0;

    [DataField("itemBioCode")]
    public bool ItemBioCode = false;

    [DataField("recordedDNA")]
    public string? RecordedDNA = null;
}
