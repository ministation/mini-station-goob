using Content.Goobstation.Shared.Bingle;
using Content.Goobstation.Shared.Blob.Components;
using Content.Goobstation.Shared.DarkLord;
using Content.Goobstation.Shared.Devil;
using Content.Goobstation.Shared.Hastur.Components;
using Content.Goobstation.Shared.Shadowling.Components;
using Content.Goobstation.Shared.SlaughterDemon;
using Content.Goobstation.Shared.Slasher.Components;
using Content.Goobstation.Shared.Wraith.Components;
using Content.Shared._Goobstation.Wizard;
using Content.Shared._Mini.Ghost;
using Content.Shared._Orion.CorticalBorer.Components;
using Content.Shared._Shitmed.Antags.Abductor;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.Ninja.Components;
using Content.Shared.NukeOps;
using Content.Shared.Revenant.Components;
using Content.Shared.WhiteDream.BloodCult.Components;
using Content.Shared.Zombies;

namespace Content.Server._Mini.Ghost;

/// <summary>
/// Система, динамически выдающая <see cref="GhostPanelAntagonistMarkerComponent"/>.
/// YAML на AntagSelection/мобах тоже может задавать маркер; PinSystem — страховка от потери при мержах.
/// Культисты/зомби появляются только после видимого превращения, без лишней меты.
/// Не помечаем «скрытых» антагов (предатель, генокрад, еретик, вор) — они остаются в списке живых.
///
/// Robust допускает только одну directed-подписку на пару Comp+Event на всём сервере.
/// ComponentStartup/MapInit у многих антагов уже заняты — используем ComponentInit
/// (у Xenomorph занят ComponentInit → MapInitEvent).
/// </summary>
public sealed class GhostPanelAntagonistMarkerPinSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NukeOperativeComponent, ComponentInit>(OnNukeOperative);
        SubscribeLocalEvent<SpaceNinjaComponent, ComponentInit>(OnSpaceNinja);
        SubscribeLocalEvent<WizardComponent, ComponentInit>(OnWizard);
        SubscribeLocalEvent<AbductorComponent, ComponentInit>(OnAbductor);
        SubscribeLocalEvent<CorticalBorerComponent, ComponentInit>(OnCorticalBorer);
        SubscribeLocalEvent<OmnipresenceComponent, ComponentInit>(OnHastur);
        SubscribeLocalEvent<SlaughterDemonComponent, ComponentInit>(OnSlaughterDemon);
        SubscribeLocalEvent<WraithComponent, ComponentInit>(OnWraith);
        SubscribeLocalEvent<RevenantComponent, ComponentInit>(OnRevenant);
        SubscribeLocalEvent<SlasherComponent, ComponentInit>(OnSlasher);
        SubscribeLocalEvent<BingleComponent, ComponentInit>(OnBingle);
        SubscribeLocalEvent<BlobObserverComponent, ComponentInit>(OnBlob);
        SubscribeLocalEvent<DevilComponent, ComponentInit>(OnDevil);
        SubscribeLocalEvent<DarkLordMarkerComponent, ComponentInit>(OnDarkLord);
        SubscribeLocalEvent<ShadowlingComponent, ComponentInit>(OnShadowling);
        // XenomorphComponent.ComponentInit already claimed by XenomorphsRuleSystem.
        SubscribeLocalEvent<XenomorphComponent, MapInitEvent>(OnXenomorph);

        SubscribeLocalEvent<MetaDataComponent, EntityZombifiedEvent>(OnZombify);

        SubscribeLocalEvent<PentagramComponent, ComponentInit>(OnCultistAscent);
        SubscribeLocalEvent<PentagramComponent, ComponentRemove>(OnCultistDescent);
    }

    private void OnNukeOperative(Entity<NukeOperativeComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-nukeops-name",
            "ghost-panel-antagonist-nukeops-description",
            priority: 10);

    private void OnSpaceNinja(Entity<SpaceNinjaComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-ninja-name",
            "ghost-panel-antagonist-ninja-description",
            priority: 40);

    private void OnWizard(Entity<WizardComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-wizard-name",
            "ghost-panel-antagonist-wizard-description",
            priority: 5);

    private void OnAbductor(Entity<AbductorComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-abductor-name",
            "ghost-panel-antagonist-abductor-description",
            priority: 25);

    private void OnCorticalBorer(Entity<CorticalBorerComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-cortical-borer-name",
            "ghost-panel-antagonist-cortical-borer-description",
            priority: 55);

    private void OnHastur(Entity<OmnipresenceComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-hastur-name",
            "ghost-panel-antagonist-hastur-description",
            priority: 8);

    private void OnSlaughterDemon(Entity<SlaughterDemonComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-slaughter-name",
            "ghost-panel-antagonist-slaughter-description",
            priority: 12);

    private void OnWraith(Entity<WraithComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-wraith-name",
            "ghost-panel-antagonist-wraith-description",
            priority: 15);

    private void OnRevenant(Entity<RevenantComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-revenant-name",
            "ghost-panel-antagonist-revenant-description",
            priority: 70);

    private void OnSlasher(Entity<SlasherComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-slasher-name",
            "ghost-panel-antagonist-slasher-description",
            priority: 18);

    private void OnBingle(Entity<BingleComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-bingle-name",
            "ghost-panel-antagonist-bingle-description",
            priority: 60);

    private void OnBlob(Entity<BlobObserverComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-blob-name",
            "ghost-panel-antagonist-blob-description",
            priority: 9);

    private void OnXenomorph(Entity<XenomorphComponent> ent, ref MapInitEvent args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-xenomorph-name",
            "ghost-panel-antagonist-xenomorph-description",
            priority: 22);

    private void OnDevil(Entity<DevilComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-devil-name",
            "ghost-panel-antagonist-devil-description",
            priority: 14);

    private void OnDarkLord(Entity<DarkLordMarkerComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-dark-lord-name",
            "ghost-panel-antagonist-dark-lord-description",
            priority: 16);

    private void OnShadowling(Entity<ShadowlingComponent> ent, ref ComponentInit args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-shadowling-name",
            "ghost-panel-antagonist-shadowling-description",
            priority: 17);

    private void OnZombify(Entity<MetaDataComponent> ent, ref EntityZombifiedEvent args)
        => Pin(ent.Owner,
            "ghost-panel-antagonist-zombie-name",
            "ghost-panel-antagonist-zombie-description",
            priority: 50);

    private void OnCultistAscent(Entity<PentagramComponent> ent, ref ComponentInit args)
        => Pin(ent.Owner,
            "ghost-panel-antagonist-cult-name",
            "ghost-panel-antagonist-cult-description",
            priority: 30);

    private void OnCultistDescent(Entity<PentagramComponent> ent, ref ComponentRemove args)
        => RemComp<GhostPanelAntagonistMarkerComponent>(ent);

    private void PinUnlessPresent(EntityUid uid, string name, string description, int priority)
    {
        if (HasComp<GhostPanelAntagonistMarkerComponent>(uid))
            return;

        Pin(uid, name, description, priority);
    }

    private void Pin(EntityUid uid, string name, string description, int priority)
    {
        var marker = EnsureComp<GhostPanelAntagonistMarkerComponent>(uid);
        marker.Name = name;
        marker.Description = description;
        marker.Priority = priority;
        Dirty(uid, marker);
    }
}
