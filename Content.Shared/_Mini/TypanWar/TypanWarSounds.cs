// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Audio;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Audio used by Typan station war announcements and events.
/// </summary>
public static class TypanWarSounds
{
    /// <summary>Short tactical HQ incoming-transmission sting for штаб announcements.</summary>
    public static readonly SoundPathSpecifier HeadquartersAlert =
        new("/Audio/_Mini/TypanWar/hq_transmission.wav");

    /// <summary>Longer dramatic sting when combat begins.</summary>
    public static readonly SoundPathSpecifier WarDeclaration =
        new("/Audio/_Mini/TypanWar/war_declaration.ogg");
}
