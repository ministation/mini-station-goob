// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Trauma.Genetics.Speech;

[RegisterComponent, NetworkedComponent, Access(typeof(CharactersAccentSystem))]
public sealed partial class CharactersAccentComponent : Component
{
    [DataField(required: true)]
    public Dictionary<char, List<string>> Chars = new();
}
