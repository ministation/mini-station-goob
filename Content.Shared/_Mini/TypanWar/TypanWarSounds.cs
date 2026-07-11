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
