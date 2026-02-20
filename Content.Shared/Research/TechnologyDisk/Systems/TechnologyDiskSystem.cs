using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared._Mini.Converter;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Research.TechnologyDisk.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Research.TechnologyDisk.Systems;

public sealed class TechnologyDiskSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedResearchSystem _research = default!;
    [Dependency] private readonly SharedLatheSystem _lathe = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TechnologyDiskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TechnologyDiskComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TechnologyDiskComponent, ExaminedEvent>(OnExamine);
    }

    private void OnMapInit(Entity<TechnologyDiskComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Recipes != null)
            return;

        var weightedRandom = _protoMan.Index(ent.Comp.TierWeightPrototype);
        var tier = int.Parse(weightedRandom.Pick(_random));

        var techs = new HashSet<ProtoId<LatheRecipePrototype>>();
        foreach (var tech in _protoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.Tier != tier)
                continue;

            techs.UnionWith(tech.RecipeUnlocks);
        }

        if (techs.Count == 0)
            return;

        ent.Comp.Recipes = new();
        ent.Comp.Recipes.Add(_random.Pick(techs));
        Dirty(ent);
    }

    private void OnAfterInteract(Entity<TechnologyDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<ConverterComponent>(target, out var converter))
        {
            if (!_net.IsServer)
                return;

            if (converter.PointsPerTelecrystal <= 0)
            {
                _popup.PopupClient(Loc.GetString("mini-converter-examine-disabled"), target, args.User);
                return;
            }

            if (!_powerReceiver.IsPowered(target))
            {
                _popup.PopupClient(Loc.GetString("tech-disk-converter-no-power-popup"), target, args.User);
                return;
            }

            if (TryComp<AccessReaderComponent>(target, out var reader) &&
                !_accessReader.IsAllowed(args.User, target, reader))
            {
                _popup.PopupClient(Loc.GetString("tech-disk-converter-no-access-popup"), target, args.User);
                return;
            }

            var value = ent.Comp.TierWeightPrototype == "RareTechDiskTierWeights"
                ? converter.RareTechnologyDiskPoints
                : converter.TechnologyDiskPoints;

            if (value < 0)
                value = 0;

            converter.StoredPoints += value;

            var payout = 0;
            if (converter.PointsPerTelecrystal > 0)
            {
                payout = converter.StoredPoints / converter.PointsPerTelecrystal;
                converter.StoredPoints %= converter.PointsPerTelecrystal;
            }

            if (payout > 0)
            {
                var telecrystalStack = Spawn("Telecrystal1", Transform(target).Coordinates);
                _stack.SetCount(telecrystalStack, payout);
                _stack.TryMergeToContacts(telecrystalStack);

                _popup.PopupClient(Loc.GetString("tech-disk-exchanged-yield",
                        ("amount", payout),
                        ("progress", converter.StoredPoints),
                        ("needed", converter.PointsPerTelecrystal)),
                    target,
                    args.User);
            }
            else
            {
                _popup.PopupClient(Loc.GetString("tech-disk-exchanged",
                        ("value", value),
                        ("progress", converter.StoredPoints),
                        ("needed", converter.PointsPerTelecrystal)),
                    target,
                    args.User);
            }

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_Mini/Misc/convert.ogg"), target, AudioParams.Default.WithVolume(-11f));
            QueueDel(ent);
            args.Handled = true;
            return;
        }

        if (!HasComp<ResearchServerComponent>(target) ||
            !TryComp<TechnologyDatabaseComponent>(target, out var database))
            return;

        if (ent.Comp.Recipes != null)
        {
            foreach (var recipe in ent.Comp.Recipes)
            {
                _research.AddLatheRecipe(target, recipe, database);
            }
        }
        _popup.PopupClient(Loc.GetString("tech-disk-inserted"), target, args.User);

        if (_net.IsServer)
            QueueDel(ent);

        args.Handled = true;
    }

    private void OnExamine(Entity<TechnologyDiskComponent> ent, ref ExaminedEvent args)
    {
        var message = Loc.GetString("tech-disk-examine-none");
        if (ent.Comp.Recipes != null && ent.Comp.Recipes.Count > 0)
        {
            var prototype = _protoMan.Index(ent.Comp.Recipes[0]);
            message = Loc.GetString("tech-disk-examine", ("result", _lathe.GetRecipeName(prototype)));

            if (ent.Comp.Recipes.Count > 1)
                message += " " + Loc.GetString("tech-disk-examine-more");
        }
        args.PushMarkup(message);
    }
}
