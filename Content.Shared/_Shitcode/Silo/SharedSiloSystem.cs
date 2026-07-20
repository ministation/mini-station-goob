// SPDX-FileCopyrightText: 2024 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Common.Silo;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Materials;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Goobstation.Silo;

public abstract class SharedSiloSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] protected readonly SharedDeviceLinkSystem DeviceLink = default!;
    [Dependency] protected readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private bool _siloEnabled;

    protected ProtoId<SourcePortPrototype> SourcePort = "MaterialSilo";
    protected ProtoId<SinkPortPrototype> SinkPort = "MaterialSiloUtilizer";

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(GoobCVars.SiloEnabled, enabled => _siloEnabled = enabled, true);

        SubscribeLocalEvent<SiloComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<SiloUtilizerComponent, PortDisconnectedEvent>(OnPortDisconnected);
        SubscribeLocalEvent<SiloUtilizerComponent, GetStoredMaterialsEvent>(OnGetStoredMaterials);
        SubscribeLocalEvent<SiloUtilizerComponent, ConsumeStoredMaterialsEvent>(OnConsumeStoredMaterials);
    }

    private void OnPortDisconnected(Entity<SiloUtilizerComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != SinkPort)
            return;

        ent.Comp.Silo = null;
        Dirty(ent);
    }

    private void OnNewLink(Entity<SiloComponent> ent, ref NewLinkEvent args)
    {
        if (args.SinkPort != SinkPort || args.SourcePort != SourcePort)
            return;

        if (!TryComp(args.Sink, out SiloUtilizerComponent? utilizer))
            return;

        if (utilizer.Silo != null)
            DeviceLink.RemoveSinkFromSource(utilizer.Silo.Value, args.Sink);

        utilizer.Silo = null;

        if (TryComp(args.Sink, out MaterialStorageComponent? utilizerStorage) &&
            utilizerStorage.Storage.Count != 0 &&
            TryComp(ent, out MaterialStorageComponent? siloStorage))
        {
            foreach (var material in utilizerStorage.Storage.Keys.ToArray())
            {
                var materialAmount = utilizerStorage.Storage.GetValueOrDefault(material, 0);
                if (_materialStorage.TryChangeMaterialAmount(ent, material, materialAmount, siloStorage))
                    _materialStorage.TryChangeMaterialAmount(args.Sink, material, -materialAmount, utilizerStorage);
            }
        }

        utilizer.Silo = ent;
        Dirty(args.Sink, utilizer);
    }

    private void OnGetStoredMaterials(Entity<SiloUtilizerComponent> ent, ref GetStoredMaterialsEvent args)
    {
        if (args.LocalOnly || args.Entity.Owner != ent.Owner)
            return;

        var silo = GetSilo(ent);
        if (silo == null || !CanTransmitMaterials(silo.Value.Owner, ent.Owner))
            return;

        var materials = _materialStorage.GetStoredMaterials((silo.Value.Owner, silo.Value.Comp), localOnly: true);

        foreach (var (mat, amount) in materials)
        {
            if (!_materialStorage.IsMaterialWhitelisted((args.Entity.Owner, args.Entity.Comp), mat))
                continue;

            var existing = args.Materials.GetOrNew(mat);
            args.Materials[mat] = existing + amount;
        }
    }

    private void OnConsumeStoredMaterials(Entity<SiloUtilizerComponent> ent, ref ConsumeStoredMaterialsEvent args)
    {
        if (args.LocalOnly || args.Entity.Owner != ent.Owner)
            return;

        var silo = GetSilo(ent);
        if (silo == null || !CanTransmitMaterials(silo.Value.Owner, ent.Owner))
            return;

        foreach (var (mat, amount) in args.Materials)
        {
            if (!_materialStorage.TryChangeMaterialAmount(silo.Value.Owner, mat, amount, silo.Value.Comp))
                continue;

            args.Materials[mat] = 0;
        }
    }

    private bool CanTransmitMaterials(EntityUid silo, EntityUid utilizer)
    {
        if (!_powerReceiver.IsPowered((silo, null)))
            return false;

        if (_transform.GetGrid(utilizer) != _transform.GetGrid(silo))
            return false;

        return true;
    }

    public bool TryGetMaterialAmount(EntityUid machine, string material, out int amount)
    {
        amount = 0;
        var silo = GetSilo(machine);
        if (silo == null)
            return false;

        amount = silo.Value.Comp.Storage.GetValueOrDefault(material, 0);
        return true;
    }

    public bool TryGetTotalMaterialAmount(EntityUid machine, out int amount)
    {
        amount = 0;
        var silo = GetSilo(machine);
        if (silo == null)
            return false;

        amount = silo.Value.Comp.Storage.Values.Sum();
        return true;
    }

    public void DirtySilo(EntityUid machine)
    {
        var silo = GetSilo(machine);
        if (silo == null)
            return;
        Dirty(silo.Value);
    }

    public Entity<MaterialStorageComponent>? GetSilo(EntityUid machine)
    {
        if (!_siloEnabled)
            return null;

        if (!TryComp(machine, out SiloUtilizerComponent? utilizer))
            return null;

        if (!TryComp(utilizer.Silo, out MaterialStorageComponent? storage))
            return null;

        return (utilizer.Silo.Value, storage);
    }
}