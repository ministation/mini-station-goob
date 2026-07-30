using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Inventory;
using Content.Server.Prayer;
using Content.Shared.Body;
using Content.Shared.Buckle;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Genetics.System;

public sealed partial class DnaModifierSystem : SharedDnaModifierSystem
{
    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private EnsureMarkingSystem _ensureMarking = default!;
    [Dependency] private StructuralEnzymesIndexerSystem _enzymesIndexer = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ServerInventorySystem _inventory = default!;
    [Dependency] private MarkingPrototypesIndexerSystem _markingIndexer = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PrayerSystem _prayerSystem = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<EmotePrototype> Scream = "Scream";

    public override void Initialize()
    {
        base.Initialize();

        InitializeInjector();
        InitializeMap();

        SubscribeLocalEvent<DnaModifierComponent, ComponentInit>(OnDnaModifierInit);
        SubscribeLocalEvent<DnaModifierDeviationComponent, ComponentStartup>(OnDnaDeviation);

        SubscribeLocalEvent<DnaModifierComponent, CureDnaDiseaseAttemptEvent>(OnTryCureDnaDisease);
        SubscribeLocalEvent<DnaModifierComponent, MutateDnaAttemptEvent>(OnTryMutateDna);

        SubscribeLocalEvent<DnaModifierComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var instabilityQuery = EntityQueryEnumerator<DnaInstabilityComponent>();
        while (instabilityQuery.MoveNext(out var uid, out var instabilityComponent))
        {
            if (instabilityComponent.NextTimeTick <= 0)
            {
                instabilityComponent.NextTimeTick = 10;
                if (!TryComp<MobThresholdsComponent>(uid, out var uidThresholds)
                    || uidThresholds.CurrentThresholdState is MobState.Dead)
                    return;

                switch (instabilityComponent.Stage)
                {
                    case 1: InstabilityStageOne(uid); break;
                    case 2: InstabilityStageTwo(uid); break;
                    case 3: InstabilityStageThree(uid); break;
                    default: break;
                }
            }
            instabilityComponent.NextTimeTick -= frameTime;
        }
    }

    private void OnDnaModifierInit(EntityUid uid, DnaModifierComponent component, ComponentInit args)
    {
        InitializeStructuralEnzymes(uid, component);
        InitializeUniqueIdentifiers(uid, component);
        CheckDeviations(uid, component);
        Dirty(uid, component);
    }

    private void OnDnaDeviation(EntityUid uid, DnaModifierDeviationComponent component, ComponentStartup args)
    {
        if (!TryComp<DnaModifierComponent>(uid, out var dnaModifier) || dnaModifier.EnzymesPrototypes == null)
            return;

        var diseaseEnzymes = dnaModifier.EnzymesPrototypes
            .Where(enzyme =>
            {
                if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                    return false;

                return enzymePrototype.TypeDeviation == EnzymesType.Disease;
            })
            .ToList();

        if (diseaseEnzymes.Count == 0)
            return;

        int countToModify = _random.Next(1, Math.Min(3, diseaseEnzymes.Count + 1));

        var enzymesToModify = diseaseEnzymes
            .OrderBy(_ => _random.Next())
            .Take(countToModify)
            .ToList();

        foreach (var enzyme in enzymesToModify)
        {
            enzyme.HexCode = GetHexCodeDisease();
        }

        TryChangeStructuralEnzymes((uid, dnaModifier));

        Dirty(uid, dnaModifier);
    }

    #region Deep Cloning
    public UniqueIdentifiersData? CloneUniqueIdentifiers(UniqueIdentifiersData? source)
    {
        if (source == null)
            return null;

        return source.Clone(source);
    }

    public List<EnzymesPrototypeInfo>? CloneEnzymesPrototypes(List<EnzymesPrototypeInfo>? source)
    {
        if (source == null)
            return null;

        return source.Select(e => (EnzymesPrototypeInfo)e.Clone()).ToList();
    }
    #endregion

    #region Initialize U.I.
    private void InitializeUniqueIdentifiers(EntityUid uid, DnaModifierComponent component, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
        {
            InitializeEmptyUniqueIdentifiers(uid, component);
            return;
        }

        // Mini: seed UI from HumanoidAppearance (Wega used VisualBody profiles).
        var uniqueIdentifiers = new UniqueIdentifiersData
        {
            ID = $"UniqueIdentifiers{uid}",
            SkinTone = ConvertSkinToneToHexArray(humanoid.SkinColor),
            FurColorR = ConvertColorToHexArray(humanoid.SkinColor).Take(3).ToArray(),
            FurColorG = ConvertColorToHexArray(humanoid.SkinColor).Skip(3).Take(3).ToArray(),
            FurColorB = ConvertColorToHexArray(humanoid.SkinColor).Skip(6).Take(3).ToArray(),
            EyeColorR = ConvertColorToHexArray(humanoid.EyeColor).Take(3).ToArray(),
            EyeColorG = ConvertColorToHexArray(humanoid.EyeColor).Skip(3).Take(3).ToArray(),
            EyeColorB = ConvertColorToHexArray(humanoid.EyeColor).Skip(6).Take(3).ToArray(),
            Gender = GenerateRandomGenderHexValue(0, 4095),
            HairColorR = GenerateRandomHexValues(),
            HairColorG = GenerateRandomHexValues(),
            HairColorB = GenerateRandomHexValues(),
            SecondaryHairColorR = GenerateRandomHexValues(),
            SecondaryHairColorG = GenerateRandomHexValues(),
            SecondaryHairColorB = GenerateRandomHexValues(),
            BeardColorR = GenerateRandomHexValues(),
            BeardColorG = GenerateRandomHexValues(),
            BeardColorB = GenerateRandomHexValues(),
            HeadAccessoryColorR = GenerateRandomHexValues(),
            HeadAccessoryColorG = GenerateRandomHexValues(),
            HeadAccessoryColorB = GenerateRandomHexValues(),
            HeadMarkingColorR = GenerateRandomHexValues(),
            HeadMarkingColorG = GenerateRandomHexValues(),
            HeadMarkingColorB = GenerateRandomHexValues(),
            BodyMarkingColorR = GenerateRandomHexValues(),
            BodyMarkingColorG = GenerateRandomHexValues(),
            BodyMarkingColorB = GenerateRandomHexValues(),
            TailMarkingColorR = GenerateRandomHexValues(),
            TailMarkingColorG = GenerateRandomHexValues(),
            TailMarkingColorB = GenerateRandomHexValues(),
            HairStyle = new[] { "0", "0", "0" },
            BeardStyle = new[] { "0", "0", "0" },
            HeadAccessoryStyle = new[] { "0", "0", "0" },
            HeadMarkingStyle = new[] { "0", "0", "0" },
            BodyMarkingStyle = new[] { "0", "0", "0" },
            TailMarkingStyle = new[] { "0", "0", "0" },
        };

        component.UniqueIdentifiers = uniqueIdentifiers;
        Dirty(uid, component);
    }

    private void InitializeEmptyUniqueIdentifiers(EntityUid uid, DnaModifierComponent component)
    {
        var empty = new[] { "0", "0", "0" };
        var uniqueIdentifiers = new UniqueIdentifiersData
        {
            ID = $"StructuralEnzymes{uid}",
            HairColorR = GenerateRandomHexValues(),
            HairColorG = GenerateRandomHexValues(),
            HairColorB = GenerateRandomHexValues(),
            SecondaryHairColorR = GenerateRandomHexValues(),
            SecondaryHairColorG = GenerateRandomHexValues(),
            SecondaryHairColorB = GenerateRandomHexValues(),
            BeardColorR = GenerateRandomHexValues(),
            BeardColorG = GenerateRandomHexValues(),
            BeardColorB = GenerateRandomHexValues(),
            SkinTone = GenerateRandomToneValues(),
            FurColorR = GenerateRandomHexValues(),
            FurColorG = GenerateRandomHexValues(),
            FurColorB = GenerateRandomHexValues(),
            HeadAccessoryColorR = GenerateRandomHexValues(),
            HeadAccessoryColorG = GenerateRandomHexValues(),
            HeadAccessoryColorB = GenerateRandomHexValues(),
            HeadMarkingColorR = GenerateRandomHexValues(),
            HeadMarkingColorG = GenerateRandomHexValues(),
            HeadMarkingColorB = GenerateRandomHexValues(),
            BodyMarkingColorR = GenerateRandomHexValues(),
            BodyMarkingColorG = GenerateRandomHexValues(),
            BodyMarkingColorB = GenerateRandomHexValues(),
            TailMarkingColorR = GenerateRandomHexValues(),
            TailMarkingColorG = GenerateRandomHexValues(),
            TailMarkingColorB = GenerateRandomHexValues(),
            EyeColorR = GenerateRandomHexValues(),
            EyeColorG = GenerateRandomHexValues(),
            EyeColorB = GenerateRandomHexValues(),
            Gender = _random.Next(0, 2) == 0
                ? GenerateRandomGenderHexValue(0x000, 0x23D) // Male
                : GenerateRandomGenderHexValue(0x23E, 0x320), // Female
            HairStyle = GenerateRandomHexValues(),
            BeardStyle = GenerateRandomHexValues(),
            HeadAccessoryStyle = empty,
            HeadMarkingStyle = empty,
            BodyMarkingStyle = empty,
            TailMarkingStyle = empty
        };

        component.UniqueIdentifiers = uniqueIdentifiers;
    }
    #endregion

    #region Initialize S.E.
    private void InitializeStructuralEnzymes(EntityUid uid, DnaModifierComponent component)
    {
        var enzymesPrototypes = _enzymesIndexer.GetAllEnzymesPrototypes();
        var uniqueEnzymesPrototypes = new List<EnzymesPrototypeInfo>();
        bool hasHumanoidAppearance = HasComp<HumanoidAppearanceComponent>(uid);
        foreach (var enzymePrototype in enzymesPrototypes)
        {
            var uniqueEnzyme = new EnzymesPrototypeInfo
            {
                EnzymesPrototypeId = enzymePrototype.EnzymesPrototypeId,
                Order = enzymePrototype.Order,
                HexCode = enzymePrototype.Order == 55
                    ? (hasHumanoidAppearance ? GenerateLastHexCode() : GenerateHexCode())
                    : GenerateHexCode()
            };

            uniqueEnzymesPrototypes.Add(uniqueEnzyme);
        }

        component.EnzymesPrototypes = uniqueEnzymesPrototypes;
    }

    private string[] GenerateHexCode()
    {
        var firstDigit = _random.Next(0, 3).ToString("X1");
        var secondDigit = _random.Next(0, 16).ToString("X1");
        var thirdDigit = _random.Next(0, 16).ToString("X1");

        return new[] { firstDigit, secondDigit, thirdDigit };
    }

    private string[] GenerateLastHexCode()
    {
        var firstDigit = _random.Next(8, 16).ToString("X1");
        var secondDigit = _random.Next(0, 16).ToString("X1");
        var thirdDigit = _random.Next(0, 16).ToString("X1");

        return new[] { firstDigit, secondDigit, thirdDigit };
    }
    #endregion

    #region Instability
    private void UpdateInstability(EntityUid uid, DnaModifierComponent component, int totalInstability)
    {
        component.Instability = totalInstability;
        if (totalInstability <= 20)
        {
            if (HasComp<DnaInstabilityComponent>(uid))
                RemComp<DnaInstabilityComponent>(uid);
            return;
        }

        var instabilityComp = EnsureComp<DnaInstabilityComponent>(uid);
        switch (totalInstability)
        {
            case > 20 and <= 35:
                instabilityComp.Stage = 1;
                break;

            case > 35 and <= 65:
                instabilityComp.Stage = 2;
                break;

            case > 65:
                instabilityComp.Stage = 3;
                break;
        }

        Dirty(uid, component);
    }

    private void CheckDeviations(EntityUid uid, DnaModifierComponent component)
    {
        if (component.EnzymesPrototypes == null)
            return;

        int totalInstability = component.Instability;
        foreach (var enzyme in component.EnzymesPrototypes)
        {
            if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                continue;

            bool hasComponent = enzymePrototype.AddComponent != null && enzymePrototype.AddComponent
                .Any(componentEntry =>
                {
                    var componentType = componentEntry.Value.Component?.GetType();
                    return componentType != null && HasComp(uid, componentType);
                });

            if (hasComponent)
            {
                enzyme.HexCode = GetHexCodeForType(enzymePrototype.TypeDeviation);
                totalInstability += enzymePrototype.CostInstability;

                if (enzymePrototype.TypeDeviation != EnzymesType.Disease
                    && enzymePrototype.AddComponent != null)
                {
                    foreach (var componentEntry in enzymePrototype.AddComponent)
                    {
                        var componentType = componentEntry.Value.Component?.GetType();
                        if (componentType != null && HasComp(uid, componentType))
                            component.InitialAbilities.Add(componentType);
                    }
                }
            }
        }

        UpdateInstability(uid, component, totalInstability);
    }

    private string[] GetHexCodeForType(EnzymesType type)
    {
        int firstDigit;
        switch (type)
        {
            case EnzymesType.Disease:
            case EnzymesType.Minor:
                firstDigit = 9;
                break;

            case EnzymesType.Intermediate:
                firstDigit = 0xC;
                break;

            case EnzymesType.Base:
                firstDigit = 0xE;
                break;

            default:
                firstDigit = _random.Next(0, 16);
                break;
        }

        return new[]
        {
            firstDigit.ToString("X1"),
            _random.Next(0, 16).ToString("X1"),
            _random.Next(0, 16).ToString("X1")
        };
    }

    private string[] GetHexCodeDisease()
    {
        return new[]
        {
            _random.Next(9, 16).ToString("X1"),
            _random.Next(0, 16).ToString("X1"),
            _random.Next(2, 16).ToString("X1")
        };
    }

    private void InstabilityStageOne(EntityUid uid)
    {
        if (_random.NextFloat() < 0.05f)
        {
            var damage = new DamageSpecifier { DamageDict = { { "Heat", 2.5 } } };
            _damage.TryChangeDamage(uid, damage, true);

            _popup.PopupEntity(Loc.GetString("dna-instability-stage-one"), uid, uid, PopupType.SmallCaution);
        }
    }

    private void InstabilityStageTwo(EntityUid uid)
    {
        if (_random.NextFloat() < 0.25f)
        {
            var damage = new DamageSpecifier { DamageDict = { { "Heat", 2.5 }, { "Blunt", 10 }, { "Structural", 2 } } };

            _damage.TryChangeDamage(uid, damage, true);

            _chat.TryEmoteWithoutChat(uid, _prototype.Index(Scream), true);
            _popup.PopupEntity(Loc.GetString("dna-instability-stage-two"), uid, uid, PopupType.SmallCaution);
        }
    }

    private void InstabilityStageThree(EntityUid uid)
    {
        if (_random.NextFloat() < 0.5f)
        {
            var damage = new DamageSpecifier { DamageDict = { { "Heat", 5 }, { "Blunt", 50 }, { "Structural", 4 } } };

            _damage.TryChangeDamage(uid, damage, true);

            _chat.TryEmoteWithoutChat(uid, _prototype.Index(Scream), true);
            _popup.PopupEntity(Loc.GetString("dna-instability-stage-three"), uid, uid, PopupType.LargeCaution);
        }
    }
    #endregion

    public void ChangeDna(Entity<DnaModifierComponent> ent, EnzymeInfo enzyme)
    {
        if (enzyme.Identifier != null) ent.Comp.UniqueIdentifiers = enzyme.Identifier;
        if (enzyme.Info != null) ent.Comp.EnzymesPrototypes = enzyme.Info;

        Dirty(ent, ent.Comp);

        TryChangeUniqueIdentifiers(ent);
        TryChangeStructuralEnzymes(ent);
    }

    public void ChangeDna(Entity<DnaModifierComponent> ent, int type)
    {
        switch (type)
        {
            case 0: TryChangeUniqueIdentifiers(ent); break;
            case 1: TryChangeStructuralEnzymes(ent); break;
        }
    }

    public void ChangeDna(Entity<DnaModifierComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return;

        TryChangeUniqueIdentifiers((uid, uid.Comp));
        TryChangeStructuralEnzymes((uid, uid.Comp));
    }

    #region Modify U.I.

    private void TryChangeUniqueIdentifiers(Entity<DnaModifierComponent> ent, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(ent, ref humanoid) || ent.Comp.UniqueIdentifiers == null)
            return;

        var uniqueIdentifiers = ent.Comp.UniqueIdentifiers;
        UpdateSkin((ent, humanoid), uniqueIdentifiers);
        UpdateMarkings((ent, humanoid), uniqueIdentifiers);
        UpdateEyeColor((ent, humanoid), uniqueIdentifiers);
        UpdateGender((ent, humanoid), uniqueIdentifiers);

        Dirty(ent, humanoid);
    }

    private void UpdateSkin(Entity<HumanoidAppearanceComponent> humanoid, UniqueIdentifiersData uniqueIdentifiers)
    {
        var speciesProto = _prototype.Index(humanoid.Comp.Species);
        var skinColorationProto = _prototype.Index(speciesProto.SkinColoration);

        Color newSkinColor;
        switch (skinColorationProto.Strategy.InputType)
        {
            case SkinColorationStrategyInput.Unary:
                newSkinColor = ConvertSkinToneToColor(uniqueIdentifiers.SkinTone);
                break;
            case SkinColorationStrategyInput.Color:
                string redHex = uniqueIdentifiers.FurColorR[0] + uniqueIdentifiers.FurColorR[1];
                string greenHex = uniqueIdentifiers.FurColorG[0] + uniqueIdentifiers.FurColorG[1];
                string blueHex = uniqueIdentifiers.FurColorB[0] + uniqueIdentifiers.FurColorB[1];
                newSkinColor = new Color(
                    Convert.ToInt32(redHex, 16) / 255f,
                    Convert.ToInt32(greenHex, 16) / 255f,
                    Convert.ToInt32(blueHex, 16) / 255f);
                break;
            default:
                return;
        }

        humanoid.Comp.SkinColor = skinColorationProto.Strategy.EnsureVerified(newSkinColor);
        Dirty(humanoid.Owner, humanoid.Comp);
    }

    private void UpdateMarkings(Entity<HumanoidAppearanceComponent> humanoid, UniqueIdentifiersData uniqueIdentifiers)
    {
        var markingPrototypes = _markingIndexer.GetAllMarkingPrototypes();

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.Hair,
            uniqueIdentifiers.HairColorR, uniqueIdentifiers.HairColorG, uniqueIdentifiers.HairColorB,
            uniqueIdentifiers.HairStyle, humanoid.Comp.Species, markingPrototypes,
            uniqueIdentifiers.SecondaryHairColorR, uniqueIdentifiers.SecondaryHairColorG, uniqueIdentifiers.SecondaryHairColorB);

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.FacialHair,
            uniqueIdentifiers.BeardColorR, uniqueIdentifiers.BeardColorG, uniqueIdentifiers.BeardColorB,
            uniqueIdentifiers.BeardStyle, humanoid.Comp.Species, markingPrototypes);

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.HeadTop,
            uniqueIdentifiers.HeadAccessoryColorR, uniqueIdentifiers.HeadAccessoryColorG, uniqueIdentifiers.HeadAccessoryColorB,
            uniqueIdentifiers.HeadAccessoryStyle, humanoid.Comp.Species, markingPrototypes);

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.Head,
            uniqueIdentifiers.HeadMarkingColorR, uniqueIdentifiers.HeadMarkingColorG, uniqueIdentifiers.HeadMarkingColorB,
            uniqueIdentifiers.HeadMarkingStyle, humanoid.Comp.Species, markingPrototypes);

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.Chest,
            uniqueIdentifiers.BodyMarkingColorR, uniqueIdentifiers.BodyMarkingColorG, uniqueIdentifiers.BodyMarkingColorB,
            uniqueIdentifiers.BodyMarkingStyle, humanoid.Comp.Species, markingPrototypes);

        _ensureMarking.UpdateMarkingCategory(humanoid.Owner, HumanoidVisualLayers.Tail,
            uniqueIdentifiers.TailMarkingColorR, uniqueIdentifiers.TailMarkingColorG, uniqueIdentifiers.TailMarkingColorB,
            uniqueIdentifiers.TailMarkingStyle, humanoid.Comp.Species, markingPrototypes);
    }

    private void UpdateEyeColor(Entity<HumanoidAppearanceComponent> humanoid, UniqueIdentifiersData uniqueIdentifiers)
    {
        string redHex = uniqueIdentifiers.EyeColorR[0] + uniqueIdentifiers.EyeColorR[1];
        string greenHex = uniqueIdentifiers.EyeColorG[0] + uniqueIdentifiers.EyeColorG[1];
        string blueHex = uniqueIdentifiers.EyeColorB[0] + uniqueIdentifiers.EyeColorB[1];
        humanoid.Comp.EyeColor = new Color(
            Convert.ToInt32(redHex, 16) / 255f,
            Convert.ToInt32(greenHex, 16) / 255f,
            Convert.ToInt32(blueHex, 16) / 255f);
        Dirty(humanoid.Owner, humanoid.Comp);
    }

    private void UpdateGender(Entity<HumanoidAppearanceComponent> humanoid, UniqueIdentifiersData uniqueIdentifiers)
    {
        int[] values = uniqueIdentifiers.Gender
            .Select(hex => Convert.ToInt32(hex, 16))
            .ToArray();

        var currentGender = (values[0], values[1], values[2]) switch
        {
            ( <= 0x5, <= 0x7, <= 0x3) => Gender.Female,
            ( < 0x8, <= 0x7, < 0x9) => Gender.Male,
            ( >= 0x8, >= 0x7, >= 0x9) => Gender.Neuter,
            _ => Gender.Neuter
        };

        var currentSex = (values[0], values[1], values[2]) switch
        {
            ( <= 0x5, <= 0x7, <= 0x3) => Sex.Female,
            ( < 0x8, <= 0x7, < 0x9) => Sex.Male,
            ( >= 0x8, >= 0x7, >= 0x9) => Sex.Unsexed,
            _ => Sex.Unsexed
        };

        humanoid.Comp.Gender = currentGender;
        humanoid.Comp.Sex = currentSex;
    }
    #endregion Modify U.I.

    #region Modify S.E.
    private void TryChangeStructuralEnzymes(Entity<DnaModifierComponent> ent)
    {
        if (ent.Comp.EnzymesPrototypes == null)
            return;

        int totalInstability = ent.Comp.Instability;
        var enzymes = ent.Comp.EnzymesPrototypes;
        var messagesToShow = new List<string>();
        foreach (var enzyme in enzymes)
        {
            if (enzyme.Order == 55)
            {
                TryChangeLastBlock(ent, ent.Comp, enzyme);
                continue;
            }

            if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                continue;

            bool meetsCondition = CheckHexCodeCondition(enzyme.HexCode, enzymePrototype.TypeDeviation);
            if (enzymePrototype.AddComponent != null)
            {
                if (meetsCondition)
                {
                    bool hasAnyComponent = enzymePrototype.AddComponent
                        .Any(componentEntry =>
                        {
                            var componentType = componentEntry.Value.Component?.GetType();
                            return componentType != null && HasComp(ent, componentType);
                        });

                    if (!hasAnyComponent && _random.NextFloat() <= enzymePrototype.ChanceAssimilation)
                    {
                        EntityManager.AddComponents(ent, enzymePrototype.AddComponent, false);
                        totalInstability += enzymePrototype.CostInstability;

                        if (!string.IsNullOrEmpty(enzymePrototype.Message))
                            messagesToShow.Add(enzymePrototype.Message);

                        _admin.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):user} acquires a gene type: '{enzymePrototype.ID}'.");
                    }
                }
                else
                {
                    foreach (var componentEntry in enzymePrototype.AddComponent)
                    {
                        var componentType = componentEntry.Value.Component?.GetType();
                        if (componentType != null && HasComp(ent, componentType)
                            && !ent.Comp.InitialAbilities.Contains(componentType))
                        {
                            RemComp(ent, componentType);
                            totalInstability -= enzymePrototype.CostInstability;

                            _admin.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):user} loses the gene type: '{enzymePrototype.ID}'.");
                        }
                    }
                }
            }
        }

        UpdateInstability(ent, ent.Comp, totalInstability);
        if (messagesToShow.Count > 0)
        {
            _ = ShowMessagesWithDelay(ent, messagesToShow);
        }
    }

    private void TryChangeLastBlock(EntityUid target, DnaModifierComponent component, EnzymesPrototypeInfo enzyme)
    {
        if (string.IsNullOrEmpty(component.Upper) || string.IsNullOrEmpty(component.Lowest))
            return;

        // Monkeys/kobolds are HumanoidAppearance too — do not use that to pick form.
        // Compare prototype / DnaLowest or we recurse: ChangeDna → spawn Lowest → ChangeDna → …
        var protoId = MetaData(target).EntityPrototype?.ID;
        var isLowestForm = HasComp<DnaLowestComponent>(target) || protoId == component.Lowest;
        var isUpperForm = HasComp<DnaModifiedComponent>(target) || protoId == component.Upper;

        int hexValue = Convert.ToInt32(enzyme.HexCode[0], 16);
        if (hexValue < 8)
        {
            if (isLowestForm)
                return;

            // Zero add an entity
            _buckle.TryUnbuckle(target, target, true);
            var child = _entManager.SpawnEntity(component.Lowest, Transform(target).Coordinates);
            if (TryComp<DamageableComponent>(target, out var parentDamage) &&
                TryComp<DamageableComponent>(child, out var childDamage))
                _damage.SetDamage(child, childDamage, parentDamage.Damage);

            EnsureComp<DnaLowestComponent>(child).Parent = target;

            // First undress
            if (_inventory.TryGetContainerSlotEnumerator(target, out var enumerator))
            {
                while (enumerator.MoveNext(out var slot))
                {
                    _inventory.TryUnequip(target, slot.ID, true, true);
                }
            }

            foreach (var held in _hands.EnumerateHeld(target))
            {
                _hands.TryDrop(target, held);
            }

            // Second customization
            if (TryComp(target, out MetaDataComponent? targetMeta))
                _metaData.SetEntityName(child, targetMeta.EntityName);

            if (_mindSystem.TryGetMind(target, out var mindId, out var mind))
                _mindSystem.TransferTo(mindId, child, mind: mind);

            if (TryComp(target, out DnaComponent? targetDna))
                EnsureComp<DnaComponent>(child).DNA = targetDna.DNA;

            var childDnaModifier = EnsureComp<DnaModifierComponent>(child);
            childDnaModifier.UniqueIdentifiers = component.UniqueIdentifiers;
            childDnaModifier.EnzymesPrototypes = component.EnzymesPrototypes?.ToList();
            childDnaModifier.Instability = component.Instability;
            childDnaModifier.Upper = component.Upper;
            childDnaModifier.Lowest = component.Lowest;

            Dirty(child, childDnaModifier);
            ChangeDna(child);

            _admin.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(target):user} gene down up a step.");

            // Third clearing
            EnsurePausedMap();
            if (PausedMap != null)
            {
                _transform.SetParent(target, Transform(target), PausedMap.Value);
            }
        }
        else
        {
            // Minus one check parent
            if (TryComp<DnaLowestComponent>(target, out var dnaLowest) && dnaLowest.Parent != null)
            {
                var parent = dnaLowest.Parent.Value;
                if (_inventory.TryGetContainerSlotEnumerator(target, out var enumeratorLowest))
                {
                    while (enumeratorLowest.MoveNext(out var slot))
                    {
                        _inventory.TryUnequip(target, slot.ID, true, true);
                    }
                }

                foreach (var held in _hands.EnumerateHeld(target))
                {
                    _hands.TryDrop(target, held);
                }

                foreach (var held in _hands.EnumerateHeld(target))
                {
                    _hands.TryDrop(target, held);
                    _hands.TryPickupAnyHand(parent, held, checkActionBlocker: false);
                }

                if (_mindSystem.TryGetMind(target, out var mindIdLowest, out var mindLowest))
                    _mindSystem.TransferTo(mindIdLowest, parent, mind: mindLowest);

                if (TryComp<DamageableComponent>(target, out var lowestDamage) &&
                    TryComp<DamageableComponent>(parent, out var parentDmg))
                    _damage.SetDamage(parent, parentDmg, lowestDamage.Damage);

                if (TryComp<DnaModifierComponent>(parent, out var dnaModifier))
                {
                    dnaModifier.UniqueIdentifiers = component.UniqueIdentifiers;
                    dnaModifier.EnzymesPrototypes = component.EnzymesPrototypes?.ToList();
                    dnaModifier.Instability = component.Instability;
                    dnaModifier.Upper = component.Upper;
                    dnaModifier.Lowest = component.Lowest;

                    Dirty(parent, dnaModifier);
                    ChangeDna(parent);
                }

                var parentXform = Transform(parent);
                _transform.SetCoordinates(parent, parentXform, Transform(target).Coordinates);
                _transform.AttachToGridOrMap(parent, parentXform);

                _entManager.DeleteEntity(target);
                return;
            }

            if (isUpperForm || !isLowestForm)
                return;

            // Zero add an entity
            _buckle.TryUnbuckle(target, target, true);
            var child = _entManager.SpawnEntity(component.Upper, Transform(target).Coordinates);
            if (TryComp<DamageableComponent>(target, out var parentDamage) &&
                TryComp<DamageableComponent>(child, out var childDamage))
                _damage.SetDamage(child, childDamage, parentDamage.Damage);

            // First undress
            if (_inventory.TryGetContainerSlotEnumerator(target, out var enumerator))
            {
                while (enumerator.MoveNext(out var slot))
                {
                    _inventory.TryUnequip(target, slot.ID, true, true);
                }
            }

            foreach (var held in _hands.EnumerateHeld(target))
            {
                _hands.TryDrop(target, held);
            }

            // Second customization
            if (TryComp(target, out MetaDataComponent? targetMeta))
                _metaData.SetEntityName(child, targetMeta.EntityName);

            if (_mindSystem.TryGetMind(target, out var mindId, out var mind))
                _mindSystem.TransferTo(mindId, child, mind: mind);

            if (TryComp(target, out DnaComponent? targetDna))
                EnsureComp<DnaComponent>(child).DNA = targetDna.DNA;

            EnsureComp<DnaModifiedComponent>(child);

            var childDnaModifier = EnsureComp<DnaModifierComponent>(child);
            childDnaModifier.UniqueIdentifiers = component.UniqueIdentifiers;
            childDnaModifier.EnzymesPrototypes = component.EnzymesPrototypes?.ToList();
            childDnaModifier.Instability = component.Instability;
            childDnaModifier.Upper = component.Upper;
            childDnaModifier.Lowest = component.Lowest;

            Dirty(child, childDnaModifier);
            ChangeDna(child);

            _admin.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(target):user} gene went up a step.");

            // Third clearing
            _entManager.DeleteEntity(target); // Bye
        }
    }

    private async Task ShowMessagesWithDelay(EntityUid target, List<string> messages)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        foreach (var message in messages)
        {
            _prayerSystem.SendSubtleMessage(actor.PlayerSession, actor.PlayerSession, string.Empty, Loc.GetString(message));
            await Task.Delay(2000);
        }
    }

    private bool CheckHexCodeCondition(string[] hexCode, EnzymesType type)
    {
        int[] values = hexCode.Select(hex => Convert.ToInt32(hex, 16)).ToArray();

        switch (type)
        {
            case EnzymesType.Disease:
            case EnzymesType.Minor:
                return values[0] > 8 || (values[0] == 8 && values[1] >= 0 && values[2] >= 2);

            case EnzymesType.Intermediate:
                return values[0] > 0xB || (values[0] == 0xB && values[1] >= 0xE && values[2] >= 0xA);

            case EnzymesType.Base:
                return values[0] > 0xD || (values[0] == 0xD && values[1] >= 0xA && values[2] >= 0xC);

            default: return false;
        }
    }
    #endregion Modify S.E.

    #region Chemistry
    private void OnTryCureDnaDisease(EntityUid uid, DnaModifierComponent component, CureDnaDiseaseAttemptEvent args)
    {
        if (component.EnzymesPrototypes == null)
            return;

        foreach (var enzyme in component.EnzymesPrototypes)
        {
            if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                continue;

            if (enzymePrototype.TypeDeviation == EnzymesType.Disease)
            {
                int[] values = enzyme.HexCode.Select(hex => Convert.ToInt32(hex, 16)).ToArray();
                if (values[0] >= 8 && values[1] >= 0 && values[2] >= 2)
                {
                    enzyme.HexCode = GenerateHexCode();
                }
            }
        }

        TryChangeStructuralEnzymes((uid, component));

        Dirty(uid, component);
    }

    private void OnTryMutateDna(EntityUid uid, DnaModifierComponent component, MutateDnaAttemptEvent args)
    {
        if (component.EnzymesPrototypes == null)
            return;

        foreach (var enzyme in component.EnzymesPrototypes)
        {
            if (enzyme.Order == 55)
            {
                enzyme.HexCode = GenerateLastHexCode();
                continue;
            }

            if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                continue;

            if (enzymePrototype.TypeDeviation == EnzymesType.Disease)
            {
                enzyme.HexCode = GetHexCodeDisease();
            }
        }

        TryChangeStructuralEnzymes((uid, component));

        Dirty(uid, component);
    }
    #endregion

    private void OnDamageChanged(EntityUid uid, DnaModifierComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || !args.DamageIncreased || !args.DamageDelta.DamageDict.ContainsKey("Radiation"))
            return;

        var radiationDamage = args.DamageDelta.DamageDict["Radiation"];
        if (radiationDamage < 1.5f)
            return;

        if (component.EnzymesPrototypes == null)
            return;

        if (_random.Prob(0.05f))
        {
            int countToModify = 1;

            var diseaseEnzymes = component.EnzymesPrototypes
                .Where(enzyme =>
                {
                    if (!_prototype.TryIndex<StructuralEnzymesPrototype>(enzyme.EnzymesPrototypeId, out var enzymePrototype))
                        return false;

                    return enzymePrototype.TypeDeviation == EnzymesType.Disease;
                })
                .ToList();

            var enzymesToModify = diseaseEnzymes
                .OrderBy(_ => _random.Next())
                .Take(countToModify)
                .ToList();

            foreach (var enzyme in enzymesToModify)
            {
                enzyme.HexCode = GetHexCodeDisease();
            }

            TryChangeStructuralEnzymes((uid, component));

            Dirty(uid, component);
        }
    }
}
