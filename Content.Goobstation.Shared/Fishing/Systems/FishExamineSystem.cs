// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Shared.Fishing.Components;
using Content.Goobstation.Shared.Fishing.Prototypes;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Fishing.Systems;

/// <summary>
/// Shows what a fish gives: fillets, beauty and traits.
/// </summary>
public sealed partial class FishExamineSystem : EntitySystem
{
    [Dependency] private FishSystem _fish = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FishComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<FishComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var proto = _fish.GetProto(ent.Comp);
        if (proto == null)
            return;

        // Same formula as FishSystem.UpdateButcherable.
        if (proto.Fillet is { } fillet)
        {
            var amount = Math.Max(1, (int) MathF.Round(proto.Fillets * ent.Comp.Size / 50f));
            var filletName = Loc.TryGetString($"ent-{fillet}", out var localized) ? localized : fillet.Id;
            args.PushMarkup(Loc.GetString("fish-examine-fillet", ("amount", amount), ("fillet", filletName)));
        }

        if (proto.Beauty > 0)
            args.PushMarkup(Loc.GetString("fish-examine-beauty", ("beauty", proto.Beauty)));

        var traits = new List<string>();
        foreach (var traitId in _fish.GetTraits(ent.Comp))
        {
            if (!_proto.TryIndex(traitId, out FishTraitPrototype? trait))
                continue;

            if (Loc.TryGetString(trait.Name, out var traitName))
                traits.Add(traitName);
        }

        if (traits.Count > 0)
            args.PushMarkup(Loc.GetString("fish-examine-traits", ("traits", string.Join(", ", traits))));
    }
}
