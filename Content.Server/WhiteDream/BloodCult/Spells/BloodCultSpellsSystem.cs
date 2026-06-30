using Content.Server.Actions;
using Content.Server.Cuffs;
using Content.Server.DoAfter;
using Content.Server.Emp;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Shared.Actions.Components;
using Content.Shared.Stunnable;
using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared._White.ListViewSelector;
using Content.Shared._White.RadialSelector;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.WhiteDream.BloodCult.Spells;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.WhiteDream.BloodCult.Spells;

public sealed class BloodCultSpellsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly CuffableSystem _cuffable = default!;
    [Dependency] private readonly EmpSystem _empSystem = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BaseCultSpellComponent, EntityTargetActionEvent>(OnCultTargetEvent);
        SubscribeLocalEvent<BaseCultSpellComponent, ActionGettingDisabledEvent>(OnActionGettingDisabled);

        SubscribeLocalEvent<BloodCultSpellsHolderComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultSelectSpellsEvent>(OnSelectSpellsAction);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, BloodCultRemoveSpellsEvent>(OnRemoveSpellsAction);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, RadialSelectorSelectedMessage>(OnSpellSelected);
        SubscribeLocalEvent<BloodCultSpellsHolderComponent, CreateSpeellDoAfterEvent>(OnSpellCreated);

        SubscribeLocalEvent<BloodCultStunEvent>(OnStun);
        SubscribeLocalEvent<BloodCultEmpEvent>(OnEmp);
        SubscribeLocalEvent<BloodCultShacklesEvent>(OnShackles);
        SubscribeLocalEvent<CuffableComponent, BloodCultShacklesDoAfterEvent>(OnShacklesDoAfter);
        SubscribeLocalEvent<SummonEquipmentEvent>(OnSummonEquipment);
    }

    #region BaseHandlers

    private void OnCultTargetEvent(Entity<BaseCultSpellComponent> spell, ref EntityTargetActionEvent args)
    {
        if (_statusEffects.HasStatusEffect(args.Performer, "Muted"))
        {
            args.Handled = true;
            return;
        }

        if (spell.Comp.BypassProtection)
            return;

        if (HasComp<MindShieldComponent>(args.Target))
            args.Handled = true;
    }

    private void OnActionGettingDisabled(Entity<BaseCultSpellComponent> spell, ref ActionGettingDisabledEvent args)
    {
        if (TryComp(args.Performer, out BloodCultSpellsHolderComponent? spellsHolder))
            spellsHolder.SelectedSpells.Remove(spell);

        if (TryComp<ActionComponent>(spell, out var actionComp))
            _actions.RemoveAction(args.Performer, (spell, actionComp));
    }

    private void OnComponentStartup(Entity<BloodCultSpellsHolderComponent> cultist, ref ComponentStartup args)
    {
        EnsureCultUi(cultist);

        cultist.Comp.MaxSpells = cultist.Comp.DefaultMaxSpells;

        foreach (var actionId in cultist.Comp.ManagementActions)
        {
            var action = _actions.AddAction(cultist, actionId);
            if (action.HasValue)
                cultist.Comp.ManagementActionEnts.Add(action.Value);
        }
    }

    private void OnComponentShutdown(Entity<BloodCultSpellsHolderComponent> cultist, ref ComponentShutdown args)
    {
        foreach (var actionUid in cultist.Comp.ManagementActionEnts)
        {
            if (TryComp<ActionComponent>(actionUid, out var actionComp))
                _actions.RemoveAction(cultist.Owner, (actionUid, actionComp));
        }

        cultist.Comp.ManagementActionEnts.Clear();
    }

    private void OnSelectSpellsAction(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultSelectSpellsEvent args)
    {
        if (args.Handled)
            return;

        SelectBloodSpells(cultist);
        args.Handled = true;
    }

    private void OnRemoveSpellsAction(Entity<BloodCultSpellsHolderComponent> cultist, ref BloodCultRemoveSpellsEvent args)
    {
        if (args.Handled)
            return;

        RemoveBloodSpells(cultist);
        args.Handled = true;
    }

    private void OnSpellSelected(Entity<BloodCultSpellsHolderComponent> cultist, ref RadialSelectorSelectedMessage args)
    {
        if (!cultist.Comp.AddSpellsMode)
        {
            if (EntityUid.TryParse(args.SelectedItem, out var actionUid))
            {
                if (TryComp<ActionComponent>(actionUid, out var actionComp))
                    _actions.RemoveAction(cultist.Owner, (actionUid, actionComp));
                cultist.Comp.SelectedSpells.Remove(actionUid);
            }

            CloseSpellSelector(cultist);
            return;
        }

        if (cultist.Comp.SelectedSpells.Count >= cultist.Comp.MaxSpells)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-too-many"), cultist, cultist, PopupType.Medium);
            CloseSpellSelector(cultist);
            return;
        }

        var createSpellEvent = new CreateSpeellDoAfterEvent
        {
            ActionProtoId = args.SelectedItem
        };

        var doAfter = new DoAfterArgs(
            EntityManager,
            cultist.Owner,
            cultist.Comp.SpellCreationTime,
            createSpellEvent,
            cultist.Owner)
        {
            BreakOnMove = true
        };

        CloseSpellSelector(cultist);

        if (_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
            cultist.Comp.DoAfterId = doAfterId;
    }

    private void OnSpellCreated(Entity<BloodCultSpellsHolderComponent> cultist, ref CreateSpeellDoAfterEvent args)
    {
        cultist.Comp.DoAfterId = null;
        if (args.Handled || args.Cancelled)
            return;

        var action = _actions.AddAction(cultist, args.ActionProtoId);
        if (action.HasValue)
            cultist.Comp.SelectedSpells.Add(action.Value);
    }

    #endregion

    #region SpellsHandlers

    private void OnStun(BloodCultStunEvent ev)
    {
        if (ev.Handled)
            return;

        _statusEffects.TryAddStatusEffect<MutedComponent>(ev.Target, "Muted", ev.MuteDuration, true);
        _stun.TryAddParalyzeDuration(ev.Target, ev.ParalyzeDuration);
        ev.Handled = true;
    }

    private void OnEmp(BloodCultEmpEvent ev)
    {
        if (ev.Handled)
            return;

        _empSystem.EmpPulse(_transform.GetMapCoordinates(ev.Performer), ev.Range, ev.EnergyConsumption, ev.Duration);
        ev.Handled = true;
    }

    private void OnShackles(BloodCultShacklesEvent ev)
    {
        if (ev.Handled)
            return;

        if (!TryComp<CuffableComponent>(ev.Target, out _))
            return;

        var shackles = Spawn(ev.ShacklesProto, Transform(ev.Performer).Coordinates);

        if (!_hands.TryPickupAnyHand(ev.Performer, shackles))
        {
            QueueDel(shackles);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            ev.Performer,
            ev.CuffDuration,
            new BloodCultShacklesDoAfterEvent(),
            ev.Target,
            ev.Target,
            shackles)
        {
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 3f
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            _hands.TryDrop(ev.Performer, shackles);
            QueueDel(shackles);
            return;
        }

        ev.Handled = true;
    }

    private void OnShacklesDoAfter(Entity<CuffableComponent> target, ref BloodCultShacklesDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.Args.User;
        var shackles = args.Args.Used;

        if (args.Cancelled || shackles == null)
        {
            if (shackles != null)
            {
                _hands.TryDrop(user, shackles.Value);
                QueueDel(shackles.Value);
            }

            return;
        }

        if (!_cuffable.TryAddNewCuffs(target, user, shackles.Value, target))
        {
            _hands.TryDrop(user, shackles.Value);
            QueueDel(shackles.Value);
            return;
        }

        _stun.TryKnockdown(target.Owner, TimeSpan.FromSeconds(1), true);
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(5), true);
    }

    private void OnSummonEquipment(SummonEquipmentEvent ev)
    {
        if (ev.Handled)
            return;

        foreach (var (slot, protoId) in ev.Prototypes)
        {
            var entity = Spawn(protoId, _transform.GetMapCoordinates(ev.Performer));
            _hands.TryPickupAnyHand(ev.Performer, entity);
            if (!TryComp(entity, out ClothingComponent? _))
                continue;

            _inventory.TryUnequip(ev.Performer, slot);
            _inventory.TryEquip(ev.Performer, entity, slot, force: true);
        }

        ev.Handled = true;
    }

    #endregion

    #region Helpers

    private void SelectBloodSpells(Entity<BloodCultSpellsHolderComponent> cultist)
    {
        if (!_proto.TryIndex(cultist.Comp.PowersPoolPrototype, out var pool))
            return;

        if (cultist.Comp.SelectedSpells.Count >= cultist.Comp.MaxSpells)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-spells-too-many"), cultist, cultist, PopupType.Medium);
            return;
        }

        cultist.Comp.AddSpellsMode = true;

        var radialList = new List<RadialSelectorEntry>();
        foreach (var spellId in pool.Powers)
        {
            var entry = new RadialSelectorEntry
            {
                Prototype = spellId,
                Icon = GetActionPrototypeIcon(spellId)
            };

            radialList.Add(entry);
        }

        var state = new RadialSelectorState(radialList, true);

        _ui.SetUiState(cultist.Owner, RadialSelectorUiKey.Key, state);
        _ui.TryToggleUi(cultist.Owner, RadialSelectorUiKey.Key, cultist.Owner);
    }

    private void RemoveBloodSpells(Entity<BloodCultSpellsHolderComponent> cultist)
    {
        if (cultist.Comp.SelectedSpells.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("blood-cult-no-spells"), cultist, cultist, PopupType.Medium);
            return;
        }

        cultist.Comp.AddSpellsMode = false;

        var radialList = new List<RadialSelectorEntry>();
        foreach (var spell in cultist.Comp.SelectedSpells)
        {
            var entry = new RadialSelectorEntry
            {
                Prototype = spell.ToString(),
                Name = Name(spell),
                Icon = GetActionIcon(spell)
            };

            radialList.Add(entry);
        }

        var state = new RadialSelectorState(radialList, true);

        _ui.SetUiState(cultist.Owner, RadialSelectorUiKey.Key, state);
        _ui.TryToggleUi(cultist.Owner, RadialSelectorUiKey.Key, cultist.Owner);
    }

    private void EnsureCultUi(EntityUid uid)
    {
        _ui.SetUi(uid, RadialSelectorUiKey.Key, new InterfaceData("RadialSelectorMenuBUI"));
        _ui.SetUi(uid, ListViewSelectorUiKey.Key, new InterfaceData("ListViewSelectorBUI"));
    }

    private void CloseSpellSelector(Entity<BloodCultSpellsHolderComponent> cultist)
    {
        _ui.CloseUi(cultist.Owner, RadialSelectorUiKey.Key, cultist.Owner);
    }

    private SpriteSpecifier? GetActionIcon(EntityUid actionUid)
    {
        return TryComp<ActionComponent>(actionUid, out var action) ? action.Icon : null;
    }

    private SpriteSpecifier? GetActionPrototypeIcon(string protoId)
    {
        if (!_proto.TryIndex(protoId, out var prototype, false)
            || !prototype.TryGetComponent(out ActionComponent? action, EntityManager.ComponentFactory))
            return null;

        return action.Icon;
    }

    #endregion
}
