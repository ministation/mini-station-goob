// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
