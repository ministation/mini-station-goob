// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Trauma.Genetics.Events;

namespace Content.Shared._Trauma.Genetics.Speech;

public sealed class SpeechFontOverrideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeechFontOverrideComponent, SpeechFontOverrideEvent>(OnOverride);
    }

    private void OnOverride(Entity<SpeechFontOverrideComponent> ent, ref SpeechFontOverrideEvent args)
    {
        if (ent.Comp.SourceOnly && args.Source != ent.Owner)
            return;

        args.Font = ent.Comp.Font;
    }
}
