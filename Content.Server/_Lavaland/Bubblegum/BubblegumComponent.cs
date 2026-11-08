// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Goobstation.Wizard.Projectiles;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Bubblegum;

[RegisterComponent]
public sealed partial class BubblegumComponent : Component
{
    [DataField]
    public TimeSpan RangedCooldown = TimeSpan.FromSeconds(2.6);

    [DataField]
    public TimeSpan ForcePressureAfter = TimeSpan.FromSeconds(5.5);

    [DataField]
    public TimeSpan TargetSwitchCooldown = TimeSpan.FromSeconds(3.5);

    [DataField]
    public TimeSpan TargetPressureMemory = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan BloodSmackDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public TimeSpan BloodGrabDelay = TimeSpan.FromSeconds(0.65);

    [DataField]
    public TimeSpan BloodWarpDelay = TimeSpan.FromSeconds(0.35);

    [DataField]
    public TimeSpan BloodHandRecover = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan BloodReactionCooldown = TimeSpan.FromSeconds(1.85);

    /// <summary>
    /// How long Bubblegum stays enraged after a blood warp.
    /// </summary>
    [DataField]
    public TimeSpan EnrageDuration = TimeSpan.FromSeconds(7);

    [DataField]
    public float EnrageSpeedMultiplier = 1.5f;

    [DataField]
    public EntProtoId EnrageCloudPrototype = "LavalandBubblegumEnrageCloud";

    [DataField]
    public TimeSpan BloodSprayStepDelay = TimeSpan.FromSeconds(0.055);

    [DataField]
    public TimeSpan ChargeWindup = TimeSpan.FromSeconds(0.45);

    [DataField]
    public TimeSpan ChargeStepDelay = TimeSpan.FromSeconds(0.05);

    [DataField]
    public TimeSpan ChargeRecover = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan ChainedChargeDelay = TimeSpan.FromSeconds(0.3);

    [DataField]
    public TimeSpan SummonCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan MovementCooldown = TimeSpan.FromSeconds(4.5);

    [DataField]
    public TimeSpan MovementCriticalCooldown = TimeSpan.FromSeconds(3.25);

    [DataField]
    public int MovementDistance = 5;

    [DataField]
    public int MovementCriticalDistance = 4;

    [DataField]
    public int BloodSprayBaseRange = 8;

    [DataField]
    public float BloodSprayRageRangeMultiplier = 0.4f;

    [DataField]
    public int MaxBloodPools = 90;

    [DataField]
    public int MaxPendingBloodTiles = 80;

    [DataField]
    public int ChargeMaxSteps = 36;

    [DataField]
    public int TripleChargeSteps = 28;

    [DataField]
    public int MaxActiveSlaughterlings = 6;

    [DataField]
    public int MaxSummonsPerCast = 6;

    [DataField]
    public float ChargeThrowSpeed = 7f;

    [DataField]
    public float BloodGrabChance = 0.25f;

    [DataField]
    public float BloodGrabChanceBelowHalf = 0.4f;

    [DataField]
    public int CombatRadius = 35;

    [ViewVariables]
    public EntityUid CombatGrid;

    [ViewVariables]
    public Vector2i CombatOrigin;

    [DataField]
    public string BloodReagent = "Blood";

    [DataField]
    public FixedPoint2 BloodSpillVolume = FixedPoint2.New(25);

    [DataField]
    public string BloodSplatterPrototype = "LavalandBubblegumBloodSplatter";

    [DataField]
    public string BloodGibsPrototype = "LavalandBubblegumBloodGibs";

    [DataField]
    public string LandingPrototype = "LavalandBubblegumLanding";

    [DataField]
    public string RightSmackPrototype = "LavalandBubblegumRightSmack";

    [DataField]
    public string LeftSmackPrototype = "LavalandBubblegumLeftSmack";

    [DataField]
    public string RightPawPrototype = "LavalandBubblegumRightPaw";

    [DataField]
    public string LeftPawPrototype = "LavalandBubblegumLeftPaw";

    [DataField]
    public string RightThumbPrototype = "LavalandBubblegumRightThumb";

    [DataField]
    public string LeftThumbPrototype = "LavalandBubblegumLeftThumb";

    [DataField]
    public string SlaughterlingPrototype = "MobLavalandBubblegumSlaughterling";

    [DataField]
    public string ClonePrototype = "LavalandBubblegumClone";

    [DataField]
    public float CloneHealthThreshold = 0.5f;

    [DataField]
    public float CloneCriticalHealthThreshold = 0.25f;

    [DataField]
    public int CloneCount = 2;

    [DataField]
    public int CloneCriticalCount = 3;

    [DataField]
    public int CloneMinOffset = 2;

    [DataField]
    public int CloneMaxOffset = 5;

    [DataField]
    public TimeSpan CloneLinger = TimeSpan.FromSeconds(0.25);

    [DataField]
    public float CloneSwapChance = 0.25f;

    [DataField]
    public float CloneSwapCriticalChance = 0.4f;

    [DataField]
    public TimeSpan CloneSwapCooldown = TimeSpan.FromSeconds(1.25);

    [DataField]
    public DamageSpecifier SmackDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", FixedPoint2.New(30) },
        },
    };

    [DataField]
    public DamageSpecifier GrabDamage = new()
    {
        DamageDict = new()
        {
            { "Slash", FixedPoint2.New(15) },
        },
    };

    [DataField]
    public DamageSpecifier ChargeDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", FixedPoint2.New(40) },
        },
    };

    [DataField]
    public SoundSpecifier AttackSound = new SoundPathSpecifier("/Audio/_Lavaland/Mobs/Bosses/Bubblegum/demon_attack1.ogg");

    [DataField]
    public SoundSpecifier EnterBloodSound = new SoundPathSpecifier("/Audio/_Lavaland/Mobs/Bosses/Bubblegum/enter_blood.ogg");

    [DataField]
    public SoundSpecifier ExitBloodSound = new SoundPathSpecifier("/Audio/_Lavaland/Mobs/Bosses/Bubblegum/exit_blood.ogg");

    [DataField]
    public SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/_Lavaland/Mobs/Bosses/Bubblegum/meteorimpact.ogg");

    [DataField]
    public SoundSpecifier SplatSound = new SoundPathSpecifier("/Audio/_Lavaland/Mobs/Bosses/Bubblegum/splat.ogg");

    [ViewVariables]
    public TimeSpan NextAttack;

    [ViewVariables]
    public TimeSpan BusyUntil;

    [ViewVariables]
    public TimeSpan NextSummon;

    [ViewVariables]
    public TimeSpan NextBloodReaction;

    [ViewVariables]
    public TimeSpan LastPressureAt;

    [ViewVariables]
    public string LastAttackKind = string.Empty;

    [ViewVariables]
    public EntityUid? CurrentPrimaryTarget;

    [ViewVariables]
    public TimeSpan LastTargetSwitchAt;

    [ViewVariables]
    public readonly Dictionary<EntityUid, TimeSpan> LastPressureByTarget = new();

    [ViewVariables]
    public bool Charging;

    [ViewVariables]
    public Vector2i ChargeTargetTile;

    [ViewVariables]
    public int ChargeRemainingSteps;

    [ViewVariables]
    public TimeSpan NextChargeStep;

    [ViewVariables]
    public int PendingCharges;

    [ViewVariables]
    public int PendingChargeSteps;

    [ViewVariables]
    public TimeSpan NextQueuedCharge;

    [ViewVariables]
    public readonly HashSet<EntityUid> ChargeHitEntities = new();

    [ViewVariables]
    public readonly List<EntityUid> SpilledPuddles = new();

    [ViewVariables]
    public readonly List<EntityUid> Slaughterlings = new();

    [ViewVariables]
    public readonly List<BubblegumActiveClone> ActiveClones = new();

    [ViewVariables]
    public readonly List<BubblegumCloneCharge> CloneCharges = new();

    [ViewVariables]
    public TimeSpan NextCloneSwap;

    [ViewVariables]
    public TimeSpan LastMovementAt;

    [ViewVariables]
    public readonly List<BubblegumPendingBloodTile> PendingBloodTiles = new();

    [ViewVariables]
    public readonly List<BubblegumPendingHandAttack> PendingHandAttacks = new();

    [ViewVariables]
    public TrailComponent? ChargeTrail;

    [ViewVariables]
    public bool Enraged;

    [ViewVariables]
    public TimeSpan EnrageUntil;
}

public sealed class BubblegumPendingBloodTile
{
    public EntityUid Grid;
    public Vector2i Tile;
    public TimeSpan SpawnAt;
    public bool Fake;
}

public sealed class BubblegumPendingHandAttack
{
    public EntityUid Grid;
    public Vector2i Tile;
    public TimeSpan AttackAt;
    public bool Grab;
    public bool RightHand;
}

public sealed class BubblegumActiveClone
{
    public EntityUid Entity;
    public TimeSpan DespawnAt;
}

public sealed class BubblegumCloneCharge
{
    public EntityUid Entity;
    public Vector2i TargetTile;
    public int RemainingSteps;
    public TimeSpan NextStep;
}
