// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Ninja.Appearance;

[Serializable, NetSerializable]
public enum NinjaColorway : byte
{
    Red = 0,
    Blue = 1,
    Green = 2,
}

[Serializable, NetSerializable]
public enum NinjaStyle : byte
{
    Old = 0,
    New = 1,
}

[Serializable, NetSerializable]
public enum NinjaAppearanceItemType : byte
{
    Color = 0,
    Scarf = 1,
    Helmet = 2,
}
