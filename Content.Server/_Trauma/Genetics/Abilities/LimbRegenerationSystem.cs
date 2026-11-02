// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared._Trauma.Genetics.Abilities;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Trauma.Genetics.Abilities;

/// <summary>
/// Regrows missing limbs and organs on entities with <see cref="LimbRegenerationComponent"/>
/// while they are unconscious and have enough hunger to spare.
/// Mirrors /tg/station's limb_regeneration mutation.
/// </summary>
public sealed partial class LimbRegenerationSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // Reused every update to avoid allocating per attempt.
    private readonly Dictionary<string, EntityUid> _mappedSlots = new();
    private readonly Queue<string> _frontier = new();
    private readonly List<MissingPart> _missingParts = new();
    private readonly List<MissingOrgan> _missingImportantOrgans = new();
    private readonly List<MissingOrgan> _missingCosmeticOrgans = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LimbRegenerationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var interval = (float) comp.AttemptInterval.TotalSeconds;
            if (interval <= 0f)
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < interval)
                continue;

            comp.Accumulator -= interval;
            if (!_random.Prob(comp.Chance))
                continue;

            TryRegenerate(uid, comp);
        }
    }

    private void TryRegenerate(EntityUid uid, LimbRegenerationComponent comp)
    {
        if (_mobState.IsDead(uid))
            return;

        if (!TryComp<BodyComponent>(uid, out var body) || body.Prototype is not { } bodyProtoId)
            return;

        if (!TryComp<HungerComponent>(uid, out var hunger))
            return;

        var prototype = ProtoMan.Index(bodyProtoId);
        if (!CollectMissing(uid, body, prototype, comp))
            return;

        if (_missingParts.Count == 0
            && _missingImportantOrgans.Count == 0
            && _missingCosmeticOrgans.Count == 0)
        {
            return;
        }

        var unconscious = HasComp<SleepingComponent>(uid) || _mobState.IsCritical(uid);
        var nutrition = _hunger.GetHunger(hunger);
        var fedThreshold = hunger.Thresholds[HungerThreshold.Okay];

        if (nutrition < fedThreshold * comp.NutritionThresholdFraction)
        {
            if (unconscious && !comp.Notified)
            {
                comp.Notified = true;
                _popup.PopupEntity(Loc.GetString("regeneration-popup-eat"), uid, uid);
            }

            return;
        }

        if (!unconscious)
        {
            if (!comp.Notified)
            {
                comp.Notified = true;
                _popup.PopupEntity(Loc.GetString("regeneration-popup-sleep"), uid, uid);
            }

            return;
        }

        comp.Notified = false;

        // /tg/ prioritizes limbs and important organs over cosmetic organs,
        // and prefers an important organ half of the time when both are missing.
        if (_missingImportantOrgans.Count > 0 || _missingParts.Count > 0)
        {
            if (_missingImportantOrgans.Count > 0 && (_missingParts.Count == 0 || _random.Prob(0.5f)))
            {
                if (TryRegrowOrgan(uid, comp, _random.Pick(_missingImportantOrgans), comp.OrganNutritionFraction))
                    return;
            }
            else if (_missingParts.Count > 0)
            {
                if (TryRegrowPart(uid, comp, _random.Pick(_missingParts), comp.LimbNutritionFraction))
                    return;
            }

            // The picked target failed to regrow for whatever reason, fall back to the other kind.
            if (_missingImportantOrgans.Count > 0
                && TryRegrowOrgan(uid, comp, _random.Pick(_missingImportantOrgans), comp.OrganNutritionFraction))
            {
                return;
            }

            if (_missingParts.Count > 0
                && TryRegrowPart(uid, comp, _random.Pick(_missingParts), comp.LimbNutritionFraction))
            {
                return;
            }
        }

        if (_missingCosmeticOrgans.Count > 0)
            TryRegrowOrgan(uid, comp, _random.Pick(_missingCosmeticOrgans), comp.CosmeticOrganNutritionFraction);
    }

    /// <summary>
    /// Walks the entity's body prototype from the root part and collects every missing part and organ.
    /// </summary>
    private bool CollectMissing(EntityUid uid, BodyComponent body, BodyPrototype prototype, LimbRegenerationComponent comp)
    {
        _mappedSlots.Clear();
        _frontier.Clear();
        _missingParts.Clear();
        _missingImportantOrgans.Clear();
        _missingCosmeticOrgans.Clear();

        if (!_body.TryGetRootPart(uid, out var rootPart, body))
            return false;

        _mappedSlots[prototype.Root] = rootPart.Value.Owner;
        _frontier.Enqueue(prototype.Root);

        while (_frontier.TryDequeue(out var slotId))
        {
            if (!_mappedSlots.TryGetValue(slotId, out var partUid)
                || !prototype.Slots.TryGetValue(slotId, out var protoSlot))
            {
                continue;
            }

            // Organs missing from an existing part.
            foreach (var (organSlotId, organProto) in protoSlot.Organs)
            {
                if (HasOrgan(partUid, organSlotId))
                    continue;

                var missing = new MissingOrgan(partUid, organSlotId, organProto);
                if (comp.CosmeticOrganSlots.Contains(organSlotId))
                    _missingCosmeticOrgans.Add(missing);
                else
                    _missingImportantOrgans.Add(missing);
            }

            // Child body parts missing from an existing part.
            foreach (var connection in protoSlot.Connections)
            {
                if (_mappedSlots.ContainsKey(connection)
                    || !prototype.Slots.TryGetValue(connection, out var childProtoSlot))
                {
                    continue;
                }

                if (_container.TryGetContainer(partUid, SharedBodySystem.GetPartSlotContainerId(connection), out var partContainer)
                    && partContainer.ContainedEntities.Count > 0)
                {
                    // The part is attached, keep walking the tree from it.
                    _mappedSlots[connection] = partContainer.ContainedEntities[0];
                    _frontier.Enqueue(connection);
                    continue;
                }

                if (comp.IgnoredPartSlots.Contains(connection))
                    continue;

                _missingParts.Add(new MissingPart(partUid, connection, childProtoSlot));
            }
        }

        return true;
    }

    private bool HasOrgan(EntityUid partUid, string organSlotId)
    {
        return _container.TryGetContainer(partUid, SharedBodySystem.GetOrganContainerId(organSlotId), out var organContainer)
               && organContainer.ContainedEntities.Count > 0;
    }

    private bool TryRegrowPart(
        EntityUid uid,
        LimbRegenerationComponent comp,
        MissingPart missing,
        float nutritionFraction)
    {
        if (missing.ProtoSlot.Part is not { } partProto
            || !TryComp<BodyPartComponent>(missing.Parent, out var parentPart))
        {
            return false;
        }

        var part = Spawn(partProto, new EntityCoordinates(missing.Parent, Vector2.Zero));
        if (!TryComp<BodyPartComponent>(part, out var partComp))
        {
            QueueDel(part);
            return false;
        }

        if (!_body.TryCreatePartSlotAndAttach(missing.Parent,
                missing.SlotId,
                part,
                partComp.PartType,
                partComp.Symmetry,
                parentPart,
                partComp))
        {
            QueueDel(part);
            return false;
        }

        // A regrown part starts empty, populate its organ slots from the body prototype as well.
        PopulatePartOrgans(part, missing.ProtoSlot);

        ConsumeNutrition(uid, comp, nutritionFraction);
        _audio.PlayPvs(comp.Sound, uid);

        var partName = Name(part);
        _popup.PopupEntity(Loc.GetString("regeneration-popup-limb", ("part", partName)), uid, uid);
        _popup.PopupEntity(Loc.GetString("regeneration-popup-visible", ("target", uid), ("part", partName)),
            uid,
            PopupType.MediumCaution);

        return true;
    }

    private void PopulatePartOrgans(EntityUid part, BodyPrototypeSlot protoSlot)
    {
        foreach (var (organSlotId, organProto) in protoSlot.Organs)
        {
            if (HasOrgan(part, organSlotId))
                continue;

            if (!_body.TryCreateOrganSlot(part, organSlotId, out _))
                continue;

            var organ = Spawn(organProto, new EntityCoordinates(part, Vector2.Zero));
            if (!_body.InsertOrgan(part, organ, organSlotId))
                QueueDel(organ);
        }
    }

    private bool TryRegrowOrgan(
        EntityUid uid,
        LimbRegenerationComponent comp,
        MissingOrgan missing,
        float nutritionFraction)
    {
        var organ = Spawn(missing.OrganProto, new EntityCoordinates(missing.Part, Vector2.Zero));
        if (!HasComp<OrganComponent>(organ))
        {
            QueueDel(organ);
            return false;
        }

        if (!_body.TryCreateOrganSlot(missing.Part, missing.SlotId, out _)
            || !_body.InsertOrgan(missing.Part, organ, missing.SlotId))
        {
            QueueDel(organ);
            return false;
        }

        ConsumeNutrition(uid, comp, nutritionFraction);
        _audio.PlayPvs(comp.Sound, uid);
        _popup.PopupEntity(Loc.GetString("regeneration-popup-organ", ("organ", Name(organ))), uid, uid);

        return true;
    }

    private void ConsumeNutrition(EntityUid uid, LimbRegenerationComponent comp, float fraction)
    {
        if (fraction <= 0f || !TryComp<HungerComponent>(uid, out var hunger))
            return;

        var fullHunger = hunger.Thresholds[HungerThreshold.Overfed];
        _hunger.ModifyHunger(uid, -fullHunger * fraction, hunger);
    }

    private readonly record struct MissingPart(EntityUid Parent, string SlotId, BodyPrototypeSlot ProtoSlot);

    private readonly record struct MissingOrgan(EntityUid Part, string SlotId, string OrganProto);
}
