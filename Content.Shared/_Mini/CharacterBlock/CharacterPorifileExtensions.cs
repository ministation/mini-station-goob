// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Content.Shared.Preferences;

namespace Content.Shared._Mini.CharacterBlock;

public static class CharacterPorifileExtensions
{
    public static string BuildId(this HumanoidCharacterProfile profile)
    {
        return profile.Name + profile.Sex + profile.Species;
    }
}
