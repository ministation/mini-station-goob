// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     Фракция каторги: определяет снаряжение, цвет и точку спавна заключённого.
///     Килл заключённого чужой фракции сокращает срок, своей — увеличивает.
/// </summary>
[Prototype]
public sealed partial class KatorgaFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Название фракции для выбора в лобби и осмотра.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Описание фракции в окне выбора.</summary>
    [DataField(required: true)]
    public LocId Feature;

    /// <summary>Фракционное снаряжение поверх базового комплекта заключённого.</summary>
    [DataField(required: true)]
    public ProtoId<StartingGearPrototype> Gear;

    /// <summary>Должность фракции: заключённому её выдают вместо прежней работы.</summary>
    [DataField(required: true)]
    public ProtoId<JobPrototype> Job;

    /// <summary>Метка фракции рядом с заключённым.</summary>
    [DataField]
    public ProtoId<FactionIconPrototype> Icon = default!;

    /// <summary>Цвет фракции в окне выбора.</summary>
    [DataField]
    public Color Color = Color.White;

    /// <summary>Порядок вывода в окне выбора.</summary>
    [DataField]
    public int Order;
}
