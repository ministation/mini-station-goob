// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._CorvaxGoob.TTS;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Ghost tier 4: the chameleon. Scans a nearby humanoid, stores the appearance and lets the ninja wear it.
/// The server only changes the appearance, the name, the description and the ID card: the worn gear can not be
/// repainted here because the clothing visuals of an item are not networked. The outfit of the disguise is
/// replicated instead and drawn by <c>NinjaGhostDisguiseClientSystem</c>.
/// The disguise is dropped on damage, on being revealed, when the suit is turned off or runs out of charge.
/// </summary>
public sealed partial class NinjaGhostDisguiseSystem : EntitySystem
{
    private const float RevealDamageThreshold = 5f;

    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaGhostDisguiseScanEvent>(OnScan);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaGhostDisguiseMenuEvent>(OnMenu);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaGhostDisguiseScanDoAfterEvent>(OnScanDoAfter);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaGhostDisguiseApplyDoAfterEvent>(OnApplyDoAfter);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaSuitPowerChangedEvent>(OnSuitPowerChanged);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, NinjaSuitRevealedEvent>(OnRevealed);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaGhostDisguiseComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<SpaceNinjaComponent, DamageChangedEvent>(OnNinjaDamaged);
    }

    private void OnScan(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaGhostDisguiseScanEvent args)
    {
        if (args.Handled)
            return;

        var suit = ent.Owner;
        var user = args.Performer;
        var comp = ent.Comp;

        if (Transform(suit).ParentUid != user)
            return;

        if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            return;

        args.Handled = true;

        var target = args.Target;
        if (!HasComp<HumanoidAppearanceComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-scan-fail-not-humanoid"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!_xform.InRange(user, target, comp.ScanRange))
        {
            _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-scan-fail-out-of-range"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!_suitPower.HasCharge(user, comp.EnergyCost))
        {
            _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-scan-fail-energy"), user, user, PopupType.MediumCaution);
            return;
        }

        if (!_suitPower.TryUseCharge(user, comp.EnergyCost))
            return;

        comp.PendingScanTarget = target;

        var doAfter = new DoAfterArgs(EntityManager, user, comp.ScanDelay, new NinjaGhostDisguiseScanDoAfterEvent(), suit, target: target, used: suit)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = comp.ScanRange,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            comp.PendingScanTarget = null;
            return;
        }

        comp.ActiveDoAfter = id;
    }

    private void OnMenu(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaGhostDisguiseMenuEvent args)
    {
        if (args.Handled)
            return;

        var suit = ent.Owner;
        var user = args.Performer;
        var comp = ent.Comp;

        if (Transform(suit).ParentUid != user)
            return;

        if (!TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            return;

        args.Handled = true;

        if (comp.Disguised)
        {
            ResetDisguise(ent, user);
            return;
        }

        if (comp.Entries.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-no-entries"), user, user, PopupType.MediumCaution);
            return;
        }

        comp.PendingIndex = comp.Entries.Count - 1;

        var doAfter = new DoAfterArgs(EntityManager, user, comp.ApplyDelay, new NinjaGhostDisguiseApplyDoAfterEvent(), suit, used: suit)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var id))
        {
            comp.PendingIndex = -1;
            return;
        }

        comp.ActiveDoAfter = id;
    }

    private void OnScanDoAfter(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaGhostDisguiseScanDoAfterEvent args)
    {
        var comp = ent.Comp;
        comp.ActiveDoAfter = null;

        var target = comp.PendingScanTarget;
        comp.PendingScanTarget = null;

        if (args.Cancelled || target is not { } targetUid || !Exists(targetUid))
            return;

        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid())
            return;

        SaveScan(ent, wearer, targetUid);
    }

    private void OnApplyDoAfter(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaGhostDisguiseApplyDoAfterEvent args)
    {
        var comp = ent.Comp;
        comp.ActiveDoAfter = null;

        var index = comp.PendingIndex;
        comp.PendingIndex = -1;

        if (args.Cancelled || index < 0)
            return;

        var wearer = Transform(ent.Owner).ParentUid;
        if (!wearer.IsValid())
            return;

        ApplyDisguise(ent, wearer, index);
    }

    private void SaveScan(Entity<NinjaGhostDisguiseComponent> ent, EntityUid wearer, EntityUid target)
    {
        var comp = ent.Comp;

        if (!HasComp<HumanoidAppearanceComponent>(target) || !_xform.InRange(wearer, target, comp.ScanRange))
            return;

        var entry = CaptureEntry(target);

        comp.Entries.Add(entry);
        if (comp.Entries.Count > comp.MaxEntries)
            comp.Entries.RemoveAt(0);

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-scan-success", ("name", entry.Name)), wearer, wearer);
    }

    private void ApplyDisguise(Entity<NinjaGhostDisguiseComponent> ent, EntityUid wearer, int index)
    {
        var comp = ent.Comp;

        if (index < 0 || index >= comp.Entries.Count)
            return;

        var entry = comp.Entries[index];
        if (entry.Appearance == null)
            return;

        if (!comp.Disguised)
            comp.Original = CaptureEntry(wearer);

        ApplyEntry(wearer, entry);
        ReplicateOutfit(comp, entry);

        comp.Disguised = true;
        comp.ActiveIndex = index;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-applied", ("name", entry.Name)), wearer, wearer);
    }

    private void ResetDisguise(Entity<NinjaGhostDisguiseComponent> ent, EntityUid wearer)
    {
        var comp = ent.Comp;

        if (comp.ActiveDoAfter is { } doAfterId)
        {
            _doAfter.Cancel(doAfterId);
            comp.ActiveDoAfter = null;
        }

        comp.PendingIndex = -1;
        comp.PendingScanTarget = null;

        if (!comp.Disguised)
            return;

        if (wearer.IsValid() && comp.Original is { } original)
            ApplyEntry(wearer, original);

        comp.Original = null;
        comp.Disguised = false;
        comp.ActiveIndex = -1;
        comp.ActiveOutfit.Clear();
        Dirty(ent);

        if (wearer.IsValid())
            _popup.PopupEntity(Loc.GetString("ninja-ghost-disguise-reset"), wearer, wearer);
    }

    private NinjaGhostDisguiseEntry CaptureEntry(EntityUid target)
    {
        var meta = MetaData(target);
        var entry = new NinjaGhostDisguiseEntry
        {
            Name = meta.EntityName,
            Description = meta.EntityDescription,
        };

        if (TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
            entry.Appearance = CaptureAppearance(humanoid);

        if (_idCard.TryFindIdCard(target, out var card))
        {
            entry.HasIdCard = true;
            entry.IdCardName = card.Comp.FullName;
            entry.IdCardJobTitle = card.Comp.JobTitle?.Id;
            entry.IdCardJobPrototype = card.Comp.JobPrototype;
        }

        // Every worn item is saved, not only the clothing slots: the client hides whatever the scanned
        // target did not wear and draws the prototypes below in the same slots.
        if (TryComp<InventoryComponent>(target, out var inventory))
        {
            var enumerator = _inventory.GetSlotEnumerator((target, inventory));
            while (enumerator.NextItem(out var item, out var slot))
            {
                if (MetaData(item).EntityPrototype is { } prototype)
                    entry.Clothing[slot.Name] = prototype.ID;
            }
        }

        return entry;
    }

    private static NinjaGhostDisguiseAppearance CaptureAppearance(HumanoidAppearanceComponent humanoid)
    {
        return new NinjaGhostDisguiseAppearance
        {
            MarkingSet = new MarkingSet(humanoid.MarkingSet),
            Species = humanoid.Species,
            SkinColor = humanoid.SkinColor,
            EyeColor = humanoid.EyeColor,
            Sex = humanoid.Sex,
            Gender = humanoid.Gender,
            Age = humanoid.Age,
            Voice = humanoid.Voice,
            PermanentlyHidden = new HashSet<HumanoidVisualLayers>(humanoid.PermanentlyHidden),
            HiddenLayers = new Dictionary<HumanoidVisualLayers, Content.Shared.Inventory.SlotFlags>(humanoid.HiddenLayers),
            CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
            Height = humanoid.Height,
            Width = humanoid.Width,
            UndergarmentTop = humanoid.UndergarmentTop,
            UndergarmentBottom = humanoid.UndergarmentBottom,
        };
    }

    private void ApplyEntry(EntityUid wearer, NinjaGhostDisguiseEntry entry)
    {
        _metaData.SetEntityName(wearer, entry.Name);
        _metaData.SetEntityDescription(wearer, entry.Description);

        if (entry.Appearance is { } appearance)
        {
            var humanoid = EnsureComp<HumanoidAppearanceComponent>(wearer);
            ApplyAppearance(wearer, humanoid, appearance);
        }

        MimicIdCard(wearer, entry);
    }

    private void ApplyAppearance(EntityUid wearer, HumanoidAppearanceComponent humanoid, NinjaGhostDisguiseAppearance appearance)
    {
        if (!string.IsNullOrEmpty(appearance.Species.Id))
            _humanoid.SetSpecies(wearer, appearance.Species.Id, sync: false, humanoid);

        humanoid.MarkingSet = new MarkingSet(appearance.MarkingSet);
        humanoid.SkinColor = appearance.SkinColor;
        humanoid.EyeColor = appearance.EyeColor;
        humanoid.Age = appearance.Age;
        humanoid.Height = appearance.Height;
        humanoid.Width = appearance.Width;
        humanoid.PermanentlyHidden = new HashSet<HumanoidVisualLayers>(appearance.PermanentlyHidden);
        humanoid.HiddenLayers = new Dictionary<HumanoidVisualLayers, Content.Shared.Inventory.SlotFlags>(appearance.HiddenLayers);
        humanoid.CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(appearance.CustomBaseLayers);
        humanoid.UndergarmentTop = appearance.UndergarmentTop;
        humanoid.UndergarmentBottom = appearance.UndergarmentBottom;

        _humanoid.SetSex(wearer, appearance.Sex, sync: false, humanoid);
        _humanoid.SetGender(wearer, appearance.Gender, sync: false, humanoid);

        if (!string.IsNullOrEmpty(appearance.Voice.Id))
            _humanoid.SetTTSVoice(wearer, appearance.Voice.Id, humanoid);

        Dirty(wearer, humanoid);
    }

    /// <summary>
    /// Publishes the outfit of the disguise: the clients hide the real gear layers and draw these prototypes.
    /// </summary>
    private void ReplicateOutfit(NinjaGhostDisguiseComponent comp, NinjaGhostDisguiseEntry entry)
    {
        comp.ActiveOutfit.Clear();

        foreach (var (slot, prototype) in entry.Clothing)
            comp.ActiveOutfit.Add(new NinjaGhostDisguiseClothing { Slot = slot, Prototype = prototype });
    }

    private void MimicIdCard(EntityUid wearer, NinjaGhostDisguiseEntry entry)
    {
        if (!_idCard.TryFindIdCard(wearer, out var card))
            return;

        if (entry.HasIdCard)
        {
            _idCard.TryChangeFullName(wearer, entry.IdCardName, card);
            _idCard.TryChangeJobTitle(wearer, entry.IdCardJobTitle, card);

            if (entry.IdCardJobPrototype is { } jobProto && _proto.TryIndex(jobProto, out JobPrototype? job))
                _idCard.TryChangeJobDepartment(wearer, job, card);
        }
        else
        {
            _idCard.TryChangeFullName(wearer, null, card);
            _idCard.TryChangeJobTitle(wearer, null, card);
        }

        card.Comp.JobPrototype = entry.HasIdCard ? entry.IdCardJobPrototype : null;
        Dirty(card.Owner, card.Comp);
    }

    private void OnSuitPowerChanged(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaSuitPowerChangedEvent args)
    {
        if (!args.Activated)
            ResetDisguise(ent, args.Wearer);
    }

    private void OnRevealed(Entity<NinjaGhostDisguiseComponent> ent, ref NinjaSuitRevealedEvent args)
    {
        if (ent.Comp.Disguised)
            ResetDisguise(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<NinjaGhostDisguiseComponent> ent, ref GotUnequippedEvent args)
    {
        ResetDisguise(ent, args.Equipee);
    }

    private void OnShutdown(Entity<NinjaGhostDisguiseComponent> ent, ref ComponentShutdown args)
    {
        var wearer = Transform(ent.Owner).ParentUid;
        ResetDisguise(ent, wearer);
    }

    private void OnNinjaDamaged(Entity<SpaceNinjaComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || args.DamageDelta.GetTotal() <= RevealDamageThreshold)
            return;

        if (ent.Comp.Suit is not { } suit || !TryComp<NinjaGhostDisguiseComponent>(suit, out var disguise) || !disguise.Disguised)
            return;

        ResetDisguise((suit, disguise), ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaGhostDisguiseComponent>();
        while (query.MoveNext(out var suit, out var comp))
        {
            if (!comp.Disguised)
                continue;

            var wearer = Transform(suit).ParentUid;

            if (!wearer.IsValid() || !TryComp<NinjaOsComponent>(suit, out var os) || !os.SuitActivated)
            {
                ResetDisguise((suit, comp), wearer);
                continue;
            }

            var cost = comp.DrainRate * frameTime;

            if (!_suitPower.HasCharge(wearer, cost) || !_suitPower.TryUseCharge(wearer, cost))
                ResetDisguise((suit, comp), wearer);
        }
    }
}