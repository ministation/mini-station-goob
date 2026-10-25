// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared.Chat;
using Robust.Shared.Random;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Server part of the chain kunai: the ninja says a short phrase on every shot.
/// </summary>
public sealed class NinjaJohyoSystem : SharedNinjaJohyoSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void SayPhrase(EntityUid user, NinjaJohyoComponent comp)
    {
        if (comp.ShotPhrases.Count == 0)
            return;

        var phrase = _random.Pick(comp.ShotPhrases);
        _chat.TrySendInGameICMessage(user, Loc.GetString(phrase), InGameICChatType.Speak, hideChat: false);
    }
}
