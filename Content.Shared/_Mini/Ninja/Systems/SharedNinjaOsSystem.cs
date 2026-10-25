// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared._Mini.Ninja.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Systems;

/// <summary>
/// Common logic of the Spider OS: module lookup and the suit boot checks.
/// </summary>
public abstract partial class SharedNinjaOsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly SharedTransformSystem Xform = default!;
    [Dependency] protected readonly InventorySystem Inventory = default!;
    [Dependency] protected readonly SharedSpaceNinjaSystem Ninja = default!;
    [Dependency] protected readonly PowerCellSystem PowerCell = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;

    /// <summary>
    /// Inventory slot of every hardware part that has to be worn to activate the suit.
    /// </summary>
    public static readonly Dictionary<NinjaOsBootCheck, string> HardwareSlots = new()
    {
        [NinjaOsBootCheck.VisorSecure] = "eyes",
        [NinjaOsBootCheck.HelmetSecure] = "head",
        [NinjaOsBootCheck.MaskSecure] = "mask",
        [NinjaOsBootCheck.GlovesSecure] = "gloves",
        [NinjaOsBootCheck.BootsSecure] = "shoes",
    };

    /// <summary>
    /// All modules available to the given OS, in prototype order.
    /// </summary>
    public List<NinjaModulePrototype> GetModules(NinjaOsComponent comp)
    {
        var result = new List<NinjaModulePrototype>();

        if (!Proto.TryIndex(comp.Os.Id, out NinjaOsPrototype? os))
            return result;

        if (os.Modules.Count > 0)
        {
            foreach (var id in os.Modules)
            {
                if (Proto.TryIndex(id, out NinjaModulePrototype? module))
                    result.Add(module);
            }

            return result;
        }

        foreach (var module in Proto.EnumeratePrototypes<NinjaModulePrototype>())
        {
            result.Add(module);
        }

        result.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.Path.CompareTo(b.Path));
        return result;
    }

    public int GetMaxTier(NinjaOsComponent comp)
    {
        if (!Proto.TryIndex(comp.Os.Id, out NinjaOsPrototype? os))
            return 5;

        return os.MaxTier;
    }

    public bool TryGetModule(NinjaOsComponent comp, NinjaPath path, int tier, [NotNullWhen(true)] out NinjaModulePrototype? module)
    {
        module = null;
        if (tier < 1)
            return false;

        foreach (var candidate in GetModules(comp))
        {
            if (candidate.Tier == tier && candidate.Path == path)
            {
                module = candidate;
                return true;
            }
        }

        return false;
    }

    public bool AllModulesSelected(NinjaOsComponent comp)
    {
        var maxTier = GetMaxTier(comp);
        for (var tier = 1; tier <= maxTier; tier++)
        {
            if (!comp.SelectedModules.ContainsKey(tier))
                return false;
        }

        return true;
    }

    /// <summary>
    /// The actor is the wearer of this suit and a space ninja bound to it.
    /// </summary>
    public bool IsAuthorized(EntityUid suitUid, EntityUid actor)
    {
        if (!actor.IsValid() || Xform.GetParentUid(suitUid) != actor)
            return false;

        return TryComp<SpaceNinjaComponent>(actor, out var ninja) && ninja.Suit == suitUid;
    }

    public bool HasSuitHardware(EntityUid wearer, string slot)
    {
        return Inventory.TryGetSlotEntity(wearer, slot, out var item) &&
               HasComp<NinjaSuitItemComponent>(item.Value);
    }

    /// <summary>
    /// Run a single boot check, returning false and the localized fail reason when it fails.
    /// </summary>
    public bool RunBootCheck(EntityUid suitUid, EntityUid actor, NinjaOsBootCheck check, out LocId failReason)
    {
        failReason = string.Empty;
        TryComp<NinjaOsComponent>(suitUid, out var comp);

        switch (check)
        {
            case NinjaOsBootCheck.SelfTest:
                return true;
            case NinjaOsBootCheck.Authorized:
                if (IsAuthorized(suitUid, actor))
                    return true;

                failReason = "ninja-os-boot-fail-auth";
                return false;
            case NinjaOsBootCheck.NotActivated:
                if (comp == null || !comp.SuitActivated)
                    return true;

                failReason = "ninja-os-boot-fail-already-active";
                return false;
            case NinjaOsBootCheck.ModulesReady:
                if (comp != null && AllModulesSelected(comp))
                    return true;

                failReason = "ninja-os-boot-fail-modules-incomplete";
                return false;
            case NinjaOsBootCheck.VisorSecure:
            case NinjaOsBootCheck.HelmetSecure:
            case NinjaOsBootCheck.MaskSecure:
            case NinjaOsBootCheck.GlovesSecure:
            case NinjaOsBootCheck.BootsSecure:
                if (IsAuthorized(suitUid, actor) && HasSuitHardware(actor, HardwareSlots[check]))
                    return true;

                failReason = check switch
                {
                    NinjaOsBootCheck.VisorSecure => "ninja-os-boot-fail-no-visor",
                    NinjaOsBootCheck.HelmetSecure => "ninja-os-boot-fail-no-helmet",
                    NinjaOsBootCheck.MaskSecure => "ninja-os-boot-fail-no-mask",
                    NinjaOsBootCheck.GlovesSecure => "ninja-os-boot-fail-no-gloves",
                    _ => "ninja-os-boot-fail-no-boots",
                };
                return false;
            case NinjaOsBootCheck.SuitFasten:
                if (IsAuthorized(suitUid, actor))
                    return true;

                failReason = "ninja-os-boot-fail-not-worn";
                return false;
            case NinjaOsBootCheck.BatteryReport:
                if (PowerCell.TryGetBatteryFromSlot(suitUid, out _))
                    return true;

                failReason = "ninja-os-boot-fail-no-power";
                return false;
            default:
                return true;
        }
    }

    public bool RunBootScriptChecks(EntityUid suitUid, EntityUid actor, NinjaOsBootPrototype boot)
    {
        foreach (var step in boot.Steps)
        {
            if (step.Check is { } check && !RunBootCheck(suitUid, actor, check, out _))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Raised on the suit and on the wearer when the suit power state changes.
/// </summary>
[ByRefEvent]
public readonly record struct NinjaSuitPowerChangedEvent(EntityUid Suit, EntityUid Wearer, bool Activated);
