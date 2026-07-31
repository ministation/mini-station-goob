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
/// </summary>
public sealed class GhostPanelAntagonistMarkerPinSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NukeOperativeComponent, ComponentStartup>(OnNukeOperative);
        SubscribeLocalEvent<SpaceNinjaComponent, ComponentStartup>(OnSpaceNinja);
        SubscribeLocalEvent<WizardComponent, ComponentStartup>(OnWizard);
        SubscribeLocalEvent<AbductorComponent, ComponentStartup>(OnAbductor);
        SubscribeLocalEvent<CorticalBorerComponent, ComponentStartup>(OnCorticalBorer);
        SubscribeLocalEvent<OmnipresenceComponent, ComponentStartup>(OnHastur);
        SubscribeLocalEvent<SlaughterDemonComponent, ComponentStartup>(OnSlaughterDemon);
        SubscribeLocalEvent<WraithComponent, ComponentStartup>(OnWraith);
        SubscribeLocalEvent<RevenantComponent, ComponentStartup>(OnRevenant);
        SubscribeLocalEvent<SlasherComponent, ComponentStartup>(OnSlasher);
        SubscribeLocalEvent<BingleComponent, ComponentStartup>(OnBingle);
        SubscribeLocalEvent<BlobObserverComponent, ComponentStartup>(OnBlob);
        SubscribeLocalEvent<XenomorphComponent, ComponentStartup>(OnXenomorph);
        SubscribeLocalEvent<DevilComponent, ComponentStartup>(OnDevil);
        SubscribeLocalEvent<DarkLordMarkerComponent, ComponentStartup>(OnDarkLord);
        SubscribeLocalEvent<ShadowlingComponent, ComponentStartup>(OnShadowling);

        SubscribeLocalEvent<MetaDataComponent, EntityZombifiedEvent>(OnZombify);

        SubscribeLocalEvent<PentagramComponent, ComponentStartup>(OnCultistAscent);
        SubscribeLocalEvent<PentagramComponent, ComponentRemove>(OnCultistDescent);
    }

    private void OnNukeOperative(Entity<NukeOperativeComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-nukeops-name",
            "ghost-panel-antagonist-nukeops-description",
            priority: 10);

    private void OnSpaceNinja(Entity<SpaceNinjaComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-ninja-name",
            "ghost-panel-antagonist-ninja-description",
            priority: 40);

    private void OnWizard(Entity<WizardComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-wizard-name",
            "ghost-panel-antagonist-wizard-description",
            priority: 5);

    private void OnAbductor(Entity<AbductorComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-abductor-name",
            "ghost-panel-antagonist-abductor-description",
            priority: 25);

    private void OnCorticalBorer(Entity<CorticalBorerComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-cortical-borer-name",
            "ghost-panel-antagonist-cortical-borer-description",
            priority: 55);

    private void OnHastur(Entity<OmnipresenceComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-hastur-name",
            "ghost-panel-antagonist-hastur-description",
            priority: 8);

    private void OnSlaughterDemon(Entity<SlaughterDemonComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-slaughter-name",
            "ghost-panel-antagonist-slaughter-description",
            priority: 12);

    private void OnWraith(Entity<WraithComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-wraith-name",
            "ghost-panel-antagonist-wraith-description",
            priority: 15);

    private void OnRevenant(Entity<RevenantComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-revenant-name",
            "ghost-panel-antagonist-revenant-description",
            priority: 70);

    private void OnSlasher(Entity<SlasherComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-slasher-name",
            "ghost-panel-antagonist-slasher-description",
            priority: 18);

    private void OnBingle(Entity<BingleComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-bingle-name",
            "ghost-panel-antagonist-bingle-description",
            priority: 60);

    private void OnBlob(Entity<BlobObserverComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-blob-name",
            "ghost-panel-antagonist-blob-description",
            priority: 9);

    private void OnXenomorph(Entity<XenomorphComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-xenomorph-name",
            "ghost-panel-antagonist-xenomorph-description",
            priority: 22);

    private void OnDevil(Entity<DevilComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-devil-name",
            "ghost-panel-antagonist-devil-description",
            priority: 14);

    private void OnDarkLord(Entity<DarkLordMarkerComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-dark-lord-name",
            "ghost-panel-antagonist-dark-lord-description",
            priority: 16);

    private void OnShadowling(Entity<ShadowlingComponent> ent, ref ComponentStartup args)
        => PinUnlessPresent(ent.Owner,
            "ghost-panel-antagonist-shadowling-name",
            "ghost-panel-antagonist-shadowling-description",
            priority: 17);

    private void OnZombify(Entity<MetaDataComponent> ent, ref EntityZombifiedEvent args)
        => Pin(ent.Owner,
            "ghost-panel-antagonist-zombie-name",
            "ghost-panel-antagonist-zombie-description",
            priority: 50);

    private void OnCultistAscent(Entity<PentagramComponent> ent, ref ComponentStartup args)
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
