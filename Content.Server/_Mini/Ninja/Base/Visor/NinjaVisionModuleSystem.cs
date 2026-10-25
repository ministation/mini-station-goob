// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Goobstation.Shared.Overlays;
using Content.Shared._Mini.Ninja.Base.Visor;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Mini.Ninja.Base.Visor;

/// <summary>
/// Applies the selected visor mode to the wearer: flash immunity, thermal vision or night vision.
/// </summary>
public sealed class NinjaVisionModuleSystem : SharedNinjaVisionModuleSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaVisionModuleComponent, NinjaCycleVisorEvent>(OnCycle);
        SubscribeLocalEvent<NinjaVisionModuleComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NinjaVisionModuleComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NinjaVisionModuleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnCycle(Entity<NinjaVisionModuleComponent> ent, ref NinjaCycleVisorEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var mode = ent.Comp.Mode switch
        {
            NinjaVisorMode.Protective => NinjaVisorMode.Thermal,
            NinjaVisorMode.Thermal => NinjaVisorMode.NightVision,
            _ => NinjaVisorMode.Protective,
        };

        SetMode(ent, mode);
    }

    private void SetMode(Entity<NinjaVisionModuleComponent> ent, NinjaVisorMode mode)
    {
        if (ent.Comp.Mode == mode)
            return;

        ent.Comp.Mode = mode;
        Dirty(ent);

        if (ent.Comp.CycleSound is { } sound)
            _audio.PlayPvs(sound, ent.Owner);

        ApplyFlashImmunity(ent);
        ApplyWearerEffect(ent);
    }

    private void OnEquipped(Entity<NinjaVisionModuleComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ApplyFlashImmunity(ent);
        ApplyWearerEffect(ent, args.Wearer);
    }

    private void OnUnequipped(Entity<NinjaVisionModuleComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ClearEffect(args.Wearer);
    }

    private void OnShutdown(Entity<NinjaVisionModuleComponent> ent, ref ComponentShutdown args)
    {
        if (Transform(ent.Owner).ParentUid is { Valid: true } wearer)
            ClearEffect(wearer);
    }

    private void ApplyFlashImmunity(Entity<NinjaVisionModuleComponent> ent)
    {
        if (ent.Comp.Mode == NinjaVisorMode.Protective)
            EnsureComp<FlashImmunityComponent>(ent.Owner);
        else
            RemComp<FlashImmunityComponent>(ent.Owner);
    }

    private void ApplyWearerEffect(Entity<NinjaVisionModuleComponent> ent)
    {
        if (Transform(ent.Owner).ParentUid is not { Valid: true } wearer)
            return;

        ApplyWearerEffect(ent, wearer);
    }

    private void ApplyWearerEffect(Entity<NinjaVisionModuleComponent> ent, EntityUid wearer)
    {
        ClearEffect(wearer);

        switch (ent.Comp.Mode)
        {
            case NinjaVisorMode.Thermal:
                var thermal = EnsureComp<ThermalVisionComponent>(wearer);
                thermal.IsActive = true;
                thermal.IsEquipment = false;
                RemoveToggleAction(wearer, thermal.ToggleActionEntity);
                thermal.ToggleActionEntity = null;
                Dirty(wearer, thermal);
                break;
            case NinjaVisorMode.NightVision:
                var night = EnsureComp<NightVisionComponent>(wearer);
                night.IsActive = true;
                night.IsEquipment = false;
                RemoveToggleAction(wearer, night.ToggleActionEntity);
                night.ToggleActionEntity = null;
                Dirty(wearer, night);
                break;
        }
    }

    private void RemoveToggleAction(EntityUid wearer, EntityUid? action)
    {
        if (action is { } actionUid && !TerminatingOrDeleted(actionUid))
            _actions.RemoveAction(wearer, actionUid);
    }

    private void ClearEffect(EntityUid wearer)
    {
        if (TerminatingOrDeleted(wearer) || !Exists(wearer))
            return;

        RemComp<ThermalVisionComponent>(wearer);
        RemComp<NightVisionComponent>(wearer);
    }
}
