// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Client._Shitcode.Wizard.Systems;
using Content.Client.Actions;
using Content.Client.Construction;
using Content.Client.Gameplay;
using Content.Client.Hands;
using Content.Client.Interaction;
using Content.Client.Outline;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Client.UserInterface.Systems.Actions.Windows;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._Goobstation.Wizard.Components;
using Content.Shared._Goobstation.Wizard.SpellCards;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Ghost;
using Content.Shared.Heretic;
using Content.Shared.Input;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Configuration; // Goobstation
using Content.Goobstation.Common.CCVar; // Goobstation
using static Content.Client.Actions.ActionsSystem;
using static Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow;
using static Robust.Client.UserInterface.Control;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.LineEdit;
using static Robust.Client.UserInterface.Controls.MultiselectOptionButton<
    Content.Client.UserInterface.Systems.Actions.Windows.ActionsWindow.Filters>;
using static Robust.Client.UserInterface.Controls.TextureRect;
using static Robust.Shared.Input.Binding.PointerInputCmdHandler;

namespace Content.Client.UserInterface.Systems.Actions;

public sealed class ActionUIController : UIController, IOnStateChanged<GameplayState>, IOnSystemChanged<ActionsSystem>
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!; // Goobstation
    [Dependency] private readonly IConfigurationManager _cfg = default!; // Goobstation

    [UISystemDependency] private readonly ActionsSystem? _actionsSystem = default;
    [UISystemDependency] private readonly InteractionOutlineSystem? _interactionOutline = default;
    [UISystemDependency] private readonly TargetOutlineSystem? _targetOutline = default;
    [UISystemDependency] private readonly SpriteSystem _spriteSystem = default!;
    [UISystemDependency] private readonly TransformSystem _transform = default!; // Goobstation
    [UISystemDependency] private readonly SpellsSystem? _spells = default!; // Goobstation
    [UISystemDependency] private readonly ActionTargetMarkSystem? _mark = default!; // Goobstation
    [UISystemDependency] private readonly EntityLookupSystem _lookup = default!; // Goobstation

    private const int DefaultPageIndex = 0;
    private const int PagedSlotCount = 10;
    private const int MaxPageCount = 9;

    private ActionButtonContainer? _container;
    private List<EntityUid?> _actions = new(); // Flat mode
    private readonly List<ActionPage> _pages = new();
    private int _currentPageIndex = DefaultPageIndex;
    private readonly DragDropHelper<ActionButton> _menuDragHelper;
    private readonly TextureRect _dragShadow;
    private ActionsWindow? _window;

    // Goobstation: hotbar layout is stored on ActionBarLayoutComponent of the body entity.
    private EntityUid? _pendingLoadFrom;
    private TimeSpan? _pendingRestoreSince;
    private bool _layoutIsPaged;
    private ISawmill _sawmill = default!;

    private ActionsBar? ActionsBar => UIManager.GetActiveUIWidgetOrNull<ActionsBar>();
    private MenuButton? ActionButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.ActionButton;
    private ActionPage CurrentPage => _pages[_currentPageIndex];
    private bool IsPagedMode =>
        string.Equals(_cfg.GetCVar(GoobCVars.ActionBarMode), GoobCVars.ActionBarModePaged, StringComparison.OrdinalIgnoreCase);

    public bool IsDragging => _menuDragHelper.IsDragging;

    /// <summary>
    /// Action slot we are currently selecting a target for.
    /// </summary>
    public EntityUid? SelectingTargetFor { get; private set; }

    public ActionUIController()
    {
        _menuDragHelper = new DragDropHelper<ActionButton>(OnMenuBeginDrag, OnMenuContinueDrag, OnMenuEndDrag);
        _dragShadow = new TextureRect
        {
            MinSize = new Vector2(64, 64),
            Stretch = StretchMode.Scale,
            Visible = false,
            SetSize = new Vector2(64, 64),
            MouseFilter = MouseFilterMode.Ignore
        };

        for (var i = 0; i < MaxPageCount; i++)
            _pages.Add(new ActionPage(PagedSlotCount));
    }

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        _sawmill = Logger.GetSawmill("action_ui_controller");
        _cfg.OnValueChanged(GoobCVars.ActionBarMode, OnActionBarModeChanged, true);
    }

    private void OnScreenLoad()
    {
       LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnStateEntered(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded += OnActionAdded;
            _actionsSystem.OnActionRemoved += OnActionRemoved;
            _actionsSystem.ActionsUpdated += OnActionsUpdated;
            // Gooobstation start
            _actionsSystem.ActionsSaved += OnActionsSaved;
            _actionsSystem.ActionsLoaded += OnActionsLoaded;
            // Goobstation end
        }

        if (_spells != null) // Goobstation
            _spells.StopTargeting += StopTargeting;

        UpdateFilterLabel();
        QueueWindowUpdate();

        _dragShadow.Orphan();
        UIManager.PopupRoot.AddChild(_dragShadow);

        var builder = CommandBinds.Builder;
        var hotbarKeys = ContentKeyFunctions.GetHotbarBoundKeys();
        for (var i = 0; i < hotbarKeys.Length; i++)
        {
            var boundId = i;
            var boundKey = hotbarKeys[i];
            builder = builder.Bind(boundKey, new PointerInputCmdHandler((in PointerInputCmdArgs args) =>
            {
                if (args.State != BoundKeyState.Down)
                    return false;

                TriggerAction(boundId);
                return true;
            }, false, true));
        }

        builder
            .Bind(ContentKeyFunctions.OpenActionsMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Bind(ContentKeyFunctions.ActionBarPreviousPage,
                InputCmdHandler.FromDelegate(_ =>
                {
                    if (IsPagedMode)
                        ChangePage(_currentPageIndex - 1);
                }))
            .Bind(ContentKeyFunctions.ActionBarNextPage,
                InputCmdHandler.FromDelegate(_ =>
                {
                    if (IsPagedMode)
                        ChangePage(_currentPageIndex + 1);
                }))
            .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(TargetingOnUse, outsidePrediction: true),
                    typeof(ConstructionSystem), typeof(DragDropSystem))
                .BindBefore(ContentKeyFunctions.AltActivateItemInWorld, new PointerInputCmdHandler(AltTargeting, outsidePrediction: true)) // Goobstation
                .BindBefore(EngineKeyFunctions.UIRightClick, new PointerInputCmdHandler(TargetingCancel, outsidePrediction: true))
            .Register<ActionUIController>();
    }

    private bool TargetingCancel(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted)
            return false;

        // only do something for actual target-based actions
        if (SelectingTargetFor == null)
            return false;

        StopTargeting();
        return true;
    }

    /// <summary>
    ///     If the user clicked somewhere, and they are currently targeting an action, try and perform it.
    /// </summary>
    private bool TargetingOnUse(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted || _actionsSystem == null || SelectingTargetFor is not { } actionId)
            return false;

        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (!EntityManager.TryGetComponent<ActionsComponent>(user, out var comp))
            return false;

        if (_actionsSystem.GetAction(actionId) is not {} action ||
            !EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            return false;
        }

        // Is the action currently valid?
        if (!_actionsSystem.ValidAction(action))
        {
            // The user is targeting with this action, but it is not valid. Maybe mark this click as
            // handled and prevent further interactions.
            return !target.InteractOnMiss;
        }

        var ev = new ActionTargetAttemptEvent(args, (user, comp), action);
        EntityManager.EventBus.RaiseLocalEvent(action, ref ev);
        if (!ev.Handled)
        {
            Log.Error($"Action {EntityManager.ToPrettyString(actionId)} did not handle ActionTargetAttemptEvent!");
            return false;
        }

        // stop targeting when needed
        if (ev.FoundTarget ? !target.Repeat : target.DeselectOnMiss)
            StopTargeting();

        return true;
    }

    public void UnloadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (ActionButton != null)
            ActionButton.OnPressed += ActionButtonPressed;
    }

    private void OnWindowOpened()
    {
        ActionButton?.SetClickPressed(true);

        SearchAndDisplay();
    }

    private void OnWindowClosed()
    {
        ActionButton?.SetClickPressed(false);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_actionsSystem != null)
        {
            _actionsSystem.OnActionAdded -= OnActionAdded;
            _actionsSystem.OnActionRemoved -= OnActionRemoved;
            _actionsSystem.ActionsUpdated -= OnActionsUpdated;
            // Gooobstation start
            _actionsSystem.ActionsSaved -= OnActionsSaved;
            _actionsSystem.ActionsLoaded -= OnActionsLoaded;
            // Goobstation end
        }

        if (_spells != null) // Goobstation
            _spells.StopTargeting -= StopTargeting;

        CommandBinds.Unregister<ActionUIController>();
    }

    private void TriggerAction(int index)
    {
        EntityUid? actionId;
        if (IsPagedMode)
        {
            // Shift-row hotkeys (indices >= 10) are unused in paged mode.
            if (index < 0 || index >= PagedSlotCount)
                return;
            actionId = CurrentPage[index];
        }
        else if (!_actions.TryGetValue(index, out actionId))
        {
            return;
        }

        if (_actionsSystem?.GetAction(actionId) is not {} action)
            return;

        // Never fire another character's leftover action entity from a leaked hotbar slot.
        if (_playerManager.LocalEntity is not { } user || action.Comp.AttachedEntity != user)
            return;

        if (EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
            ToggleTargeting((action, action, target));
        else
            _actionsSystem?.TriggerAction(action);
    }

    private void ChangePage(int index)
    {
        if (_actionsSystem == null || _pages.Count == 0)
            return;

        var lastPage = _pages.Count - 1;
        if (index < 0)
            index = lastPage;
        else if (index > lastPage)
            index = 0;

        _currentPageIndex = index;
        RefreshActionContainer();
        UpdatePageButtons();
    }

    private void OnLeftArrowPressed(ButtonEventArgs args)
    {
        ChangePage(_currentPageIndex - 1);
    }

    private void OnRightArrowPressed(ButtonEventArgs args)
    {
        ChangePage(_currentPageIndex + 1);
    }

    private void UpdatePageButtons()
    {
        if (ActionsBar?.PageButtons is not { } pageButtons)
            return;

        pageButtons.Visible = IsPagedMode;
        pageButtons.Label.Text = $"{_currentPageIndex + 1}";
    }

    private void OnActionBarModeChanged(string _)
    {
        if (_layoutIsPaged != IsPagedMode)
            MigrateLayoutToCurrentMode();
        _layoutIsPaged = IsPagedMode;
        UpdatePageButtons();
        RefreshActionContainer();
        QueueWindowUpdate();
    }

    private void MigrateLayoutToCurrentMode()
    {
        if (IsPagedMode)
        {
            var flat = new List<EntityUid?>(_actions);
            if (flat.Count == 0)
            {
                foreach (var page in _pages)
                {
                    for (var i = 0; i < page.Size; i++)
                    {
                        if (page[i] != null)
                            flat.Add(page[i]);
                    }
                }
            }

            foreach (var page in _pages)
                page.Clear();

            EnsurePageCapacity((flat.Count + PagedSlotCount - 1) / PagedSlotCount);
            for (var i = 0; i < flat.Count; i++)
            {
                var pageIndex = i / PagedSlotCount;
                var slot = i % PagedSlotCount;
                _pages[pageIndex][slot] = flat[i];
            }

            _currentPageIndex = DefaultPageIndex;
            _actions.Clear();
        }
        else
        {
            _actions.Clear();
            foreach (var page in _pages)
            {
                for (var i = 0; i < page.Size; i++)
                {
                    if (page[i] != null)
                        _actions.Add(page[i]);
                }
            }

            foreach (var page in _pages)
                page.Clear();
            _currentPageIndex = DefaultPageIndex;
        }

        _layoutIsPaged = IsPagedMode;
    }

    private void EnsurePageCapacity(int pageCount)
    {
        pageCount = Math.Clamp(pageCount, 1, MaxPageCount);
        while (_pages.Count < pageCount)
            _pages.Add(new ActionPage(PagedSlotCount));
    }

    private void RefreshActionContainer()
    {
        if (_actionsSystem == null || _container == null)
            return;

        if (IsPagedMode)
        {
            _container.SetActionData(
                _actionsSystem,
                CurrentPage,
                fixedSize: true,
                keys: ContentKeyFunctions.GetPagedHotbarBoundKeys());
        }
        else
        {
            _container.SetActionData(_actionsSystem, _actions.ToArray());
        }
    }

    // Goobstation start
    private bool AltTargeting(in PointerInputCmdArgs args)
    {
        if (!_timing.IsFirstTimePredicted || _actionsSystem == null || SelectingTargetFor is not { } actionId)
            return false;

        if (_playerManager.LocalEntity is not { } user)
            return false;

        if (_actionsSystem.GetAction(actionId) is not { } action)
            return false;

        if (!EntityManager.TryGetComponent(actionId, out TargetActionComponent? targetComp))
            return false;

        // Is the action currently valid?
        if (!_actionsSystem.ValidAction(action))
        {
            // The user is targeting with this action, but it is not valid. Maybe mark this click as
            // handled and prevent further interactions.
            return !targetComp.InteractOnMiss;
        }

        if (!EntityManager.TryGetComponent(actionId, out EntityTargetActionComponent? entityTarget))
            return false;

        if (!EntityManager.TryGetComponent(actionId, out SwapSpellComponent? swap))
            return false;

        if (!swap.AllowSecondaryTarget)
            return false;

        if (_actionsSystem == null || _spells == null)
            return false;

        var entity = args.EntityUid;

        if (!_actionsSystem.ValidateEntityTarget(user, entity, (actionId, entityTarget)))
        {
            if (targetComp.DeselectOnMiss)
                StopTargeting();

            return false;
        }

        _spells.SetSwapSecondaryTarget(user, entity, actionId);

        return true;
    }

    private void OnActionsSaved(EntityUid entity)
    {
        if (entity == default)
            return;

        if (IsTransientActionBody(entity))
        {
            _sawmill.Info($"Skip saving action layout from transient {entity}");
            return;
        }

        PersistLayout(entity);
        var count = EntityManager.TryGetComponent(entity, out ActionBarLayoutComponent? layout)
            ? layout.NonNullCount
            : 0;
        _sawmill.Info($"Saved action layout on entity {entity} (slots={count})");
    }

    private void OnActionsLoaded(EntityUid entity)
    {
        _sawmill.Info($"Load action layout request for {entity}");

        if (_playerManager.LocalEntity is not { } local)
            return;

        // Still on jaunt/ghost — layout stays on the real body entity.
        if (IsTransientActionBody(local))
            return;

        if (!TryRestoreLayout())
        {
            _pendingLoadFrom = local;
            _pendingRestoreSince ??= _timing.CurTime;
        }
    }

    /// <summary>
    /// Writes the current hotbar into <see cref="ActionBarLayoutComponent"/> on that entity only.
    /// </summary>
    private void PersistLayout(EntityUid? forEntity = null, bool allowShrink = true)
    {
        var key = forEntity ?? _playerManager.LocalEntity;
        if (key is not { } uid)
            return;

        // Detach during entity delete / round flush — never add comps to a dying entity.
        if (!EntityManager.TryGetComponent(uid, out MetaDataComponent? meta) ||
            meta.EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        if (IsTransientActionBody(uid))
            return;

        var captured = CaptureLayout();
        if (captured.IsEmpty)
            return;

        if (!allowShrink &&
            EntityManager.TryGetComponent(uid, out ActionBarLayoutComponent? existing) &&
            (captured.NonNullCount < existing.NonNullCount ||
             captured.Pages.Count < existing.Pages.Count))
            return;

        if (!EntityManager.TryGetComponent(uid, out ActionBarLayoutComponent? layout))
        {
            if (meta.EntityLifeStage >= EntityLifeStage.Terminating)
                return;

            layout = EntityManager.AddComponent<ActionBarLayoutComponent>(uid);
        }

        layout.IsPaged = captured.IsPaged;
        layout.CurrentPage = captured.CurrentPage;
        layout.Pages.Clear();
        foreach (var page in captured.Pages)
        {
            var data = new List<ActionBarSlotData>(page.Count);
            foreach (var slot in page)
            {
                data.Add(new ActionBarSlotData
                {
                    ProtoId = slot.ProtoId,
                    ContainerProtoId = slot.ContainerProtoId,
                });
            }

            layout.Pages.Add(data);
        }
    }

    private bool TryRestoreLayout()
    {
        if (_playerManager.LocalEntity is not { } localEntity)
            return false;

        if (IsTransientActionBody(localEntity))
        {
            _pendingLoadFrom = null;
            _pendingRestoreSince = null;
            return true;
        }

        // Only this entity's component — never a session-wide layout from another body.
        if (!EntityManager.TryGetComponent(localEntity, out ActionBarLayoutComponent? layout) ||
            layout.Pages.Count == 0)
            return false;

        var saved = ToSavedLayout(layout);
        var current = CaptureAvailableSlots();
        if (current.Count == 0)
            return false;

        var savedCount = saved.NonNullCount;
        if (savedCount == 0)
            return false;

        var (remapped, matched) = RemapLayout(saved.Flatten(), current, localEntity);
        if (matched == 0)
            return false;

        // Component left over on a reused uid / stripped character: almost nothing overlaps.
        if (matched * 2 < savedCount && current.Count * 2 < savedCount)
        {
            _sawmill.Info($"Reject stale entity layout on {localEntity} (matched={matched}/{savedCount})");
            EntityManager.RemoveComponent<ActionBarLayoutComponent>(localEntity);
            LoadDefaultActions();
            RefreshActionContainer();
            _pendingLoadFrom = null;
            _pendingRestoreSince = null;
            return true;
        }

        ApplyFlatLayout(remapped);
        if (IsPagedMode)
            _currentPageIndex = Math.Clamp(saved.CurrentPage, 0, Math.Max(0, _pages.Count - 1));
        UpdatePageButtons();
        RefreshActionContainer();
        QueueWindowUpdate();

        var timedOut = _pendingRestoreSince is { } since &&
                       _timing.CurTime - since > TimeSpan.FromSeconds(2);
        var incomplete = matched < savedCount && current.Count < savedCount && !timedOut;
        if (incomplete)
        {
            _pendingLoadFrom = localEntity;
            _pendingRestoreSince ??= _timing.CurTime;
            _sawmill.Info($"Partial action restore {matched}/{savedCount} (have {current.Count}), waiting");
            return false;
        }

        _pendingLoadFrom = null;
        _pendingRestoreSince = null;
        PersistLayout(localEntity, allowShrink: false);
        _sawmill.Info($"Restored action layout from entity {localEntity} (matched={matched}/{savedCount})");
        return true;
    }

    private static SavedActionLayout ToSavedLayout(ActionBarLayoutComponent layout)
    {
        var saved = new SavedActionLayout
        {
            IsPaged = layout.IsPaged,
            CurrentPage = layout.CurrentPage,
        };

        foreach (var page in layout.Pages)
        {
            var slots = new List<SavedSlot>(page.Count);
            foreach (var slot in page)
                slots.Add(new SavedSlot(null, slot.ProtoId, slot.ContainerProtoId));
            saved.Pages.Add(slots);
        }

        return saved;
    }

    private bool IsTransientActionBody(EntityUid uid)
    {
        return EntityManager.HasComponent<GhostComponent>(uid)
               || EntityManager.HasComponent<SpectralComponent>(uid);
    }

    private SavedActionLayout CaptureLayout()
    {
        var layout = new SavedActionLayout { CurrentPage = _currentPageIndex };
        if (IsPagedMode)
        {
            layout.IsPaged = true;
            var lastUsed = 0;
            for (var i = 0; i < _pages.Count; i++)
            {
                for (var slot = 0; slot < _pages[i].Size; slot++)
                {
                    if (_pages[i][slot] != null)
                        lastUsed = i;
                }
            }

            // Include the page the user is viewing even if it is currently empty,
            // so moving the last action onto page 2 still persists that page.
            lastUsed = Math.Max(lastUsed, _currentPageIndex);

            for (var i = 0; i <= lastUsed && i < _pages.Count; i++)
                layout.Pages.Add(CapturePageSlots(_pages[i]));
        }
        else
        {
            layout.IsPaged = false;
            layout.Pages.Add(_actions.Select(ToSavedSlot).ToList());
        }

        return layout;
    }

    /// <summary>
    /// All actions currently granted to the local player (not UI slot order).
    /// Used as the remapping pool after body changes.
    /// </summary>
    private List<SavedSlot> CaptureAvailableSlots()
    {
        if (_actionsSystem == null)
            return [];

        var slots = new List<SavedSlot>();
        foreach (var action in _actionsSystem.GetClientActions())
            slots.Add(ToSavedSlot(action.Owner));
        return slots;
    }

    private List<SavedSlot> CapturePageSlots(ActionPage page)
    {
        var slots = new List<SavedSlot>(page.Size);
        for (var i = 0; i < page.Size; i++)
            slots.Add(ToSavedSlot(page[i]));
        return slots;
    }

    private SavedSlot ToSavedSlot(EntityUid? action)
    {
        if (action is not { } uid)
            return default;

        string? proto = null;
        string? containerProto = null;
        EntityUid? container = null;
        if (EntityManager.TryGetComponent(uid, out MetaDataComponent? meta))
            proto = meta.EntityPrototype?.ID;

        if (EntityManager.TryGetComponent(uid, out ActionComponent? actionComp) &&
            actionComp.Container is { } cont)
        {
            container = cont;
            if (EntityManager.TryGetComponent(cont, out MetaDataComponent? containerMeta))
                containerProto = containerMeta.EntityPrototype?.ID;
        }

        return new SavedSlot(uid, proto, containerProto, container);
    }

    private void ApplyFlatLayout(List<EntityUid?> flat)
    {
        if (IsPagedMode)
        {
            foreach (var page in _pages)
                page.Clear();

            EnsurePageCapacity(Math.Max(1, (flat.Count + PagedSlotCount - 1) / PagedSlotCount));
            var maxSlots = _pages.Count * PagedSlotCount;
            for (var i = 0; i < flat.Count && i < maxSlots; i++)
            {
                _pages[i / PagedSlotCount][i % PagedSlotCount] = flat[i];
            }

            _currentPageIndex = Math.Clamp(_currentPageIndex, 0, _pages.Count - 1);
            _actions.Clear();
        }
        else
        {
            _actions = new List<EntityUid?>(flat);
            foreach (var page in _pages)
                page.Clear();
            _currentPageIndex = DefaultPageIndex;
        }

        _layoutIsPaged = IsPagedMode;
    }

    private (List<EntityUid?> Result, int Matched) RemapLayout(
        List<SavedSlot> savedSlots,
        List<SavedSlot> currentSlots,
        EntityUid localEntity)
    {
        var metaQuery = EntityManager.GetEntityQuery<MetaDataComponent>();
        var used = new HashSet<EntityUid>();
        _ = localEntity;

        string? ProtoOf(EntityUid uid, string? known)
        {
            if (known != null)
                return known;
            return metaQuery.TryGetComponent(uid, out var meta) ? meta.EntityPrototype?.ID : null;
        }

        EntityUid? FindMatch(SavedSlot saved, bool allowReuse)
        {
            // 1) Exact entity — same action may sit on multiple pages; reuse is allowed.
            if (saved.Action is { } savedUid)
            {
                foreach (var current in currentSlots)
                {
                    if (current.Action is not { } cur)
                        continue;
                    if (cur != savedUid)
                        continue;
                    if (!allowReuse && used.Contains(cur))
                        continue;
                    return cur;
                }
            }

            if (saved.ProtoId == null)
                return null;

            // 2) Distinct entity: proto + same container entity (two PDAs etc.).
            if (saved.Container is { } savedContainer)
            {
                foreach (var current in currentSlots)
                {
                    if (current.Action is not { } cur || used.Contains(cur))
                        continue;
                    if (current.ProtoId != saved.ProtoId)
                        continue;
                    if (current.Container == savedContainer)
                        return cur;
                }
            }

            // 3) Distinct entity: proto + container prototype, first unused.
            if (saved.ContainerProtoId != null)
            {
                foreach (var current in currentSlots)
                {
                    if (current.Action is not { } cur || used.Contains(cur))
                        continue;
                    if (current.ProtoId != saved.ProtoId)
                        continue;
                    if (current.ContainerProtoId != saved.ContainerProtoId)
                        continue;
                    return cur;
                }
            }

            // 4) First unused matching prototype.
            foreach (var current in currentSlots)
            {
                if (current.Action is not { } cur || used.Contains(cur))
                    continue;
                if (ProtoOf(cur, current.ProtoId) != saved.ProtoId)
                    continue;
                return cur;
            }

            // 5) Same proto already placed — put that entity in this slot too (multi-page dupe).
            if (allowReuse)
            {
                foreach (var current in currentSlots)
                {
                    if (current.Action is not { } cur || !used.Contains(cur))
                        continue;
                    if (ProtoOf(cur, current.ProtoId) != saved.ProtoId)
                        continue;
                    return cur;
                }
            }

            return null;
        }

        var newActions = new List<EntityUid?>();
        var matched = 0;
        foreach (var saved in savedSlots)
        {
            if (saved.IsEmpty)
            {
                newActions.Add(null);
                continue;
            }

            // First pass prefers unused entities; if that fails, allow reusing one already placed.
            var matchedAction = FindMatch(saved, allowReuse: false) ?? FindMatch(saved, allowReuse: true);
            if (matchedAction is { } action)
            {
                used.Add(action);
                newActions.Add(action);
                matched++;
            }
            else
            {
                newActions.Add(null);
            }
        }

        // Do not append unmatched current actions — that puts back everything the player
        // cleared from the hotbar whenever layout is restored (equip / jaunt / aghost).
        return (newActions, matched);
    }
    // Goobstation end

    private void AppendAction(EntityUid action)
    {
        if (IsPagedMode)
        {
            foreach (var page in _pages)
            {
                for (var i = 0; i < page.Size; i++)
                {
                    if (page[i] != null)
                        continue;
                    page[i] = action;
                    return;
                }
            }

            // All pages full — leave action off the bar until a slot frees up.
            return;
        }

        if (!_actions.Contains(action))
            _actions.Add(action);
    }

    private bool ContainsAction(EntityUid actionId)
    {
        if (IsPagedMode)
        {
            foreach (var page in _pages)
            {
                for (var i = 0; i < page.Size; i++)
                {
                    if (page[i] == actionId)
                        return true;
                }
            }

            return false;
        }

        return _actions.Contains(actionId);
    }

    private void OnActionAdded(EntityUid actionId)
    {
        if (_actionsSystem?.GetAction(actionId) is not {} action)
            return;

        if (action.Comp.Toggled && EntityManager.TryGetComponent<TargetActionComponent>(actionId, out var target))
            StartTargeting((action, action, target));

        if (_pendingLoadFrom != null)
        {
            if (TryRestoreLayout())
                return;

            // Waiting for this body's component restore — do not fill intentional holes.
            if (_playerManager.LocalEntity is { } local &&
                EntityManager.HasComponent<ActionBarLayoutComponent>(local))
                return;

            _pendingLoadFrom = null;
        }

        if (!ContainsAction(action))
        {
            AppendAction(action);
            // Keep entity layout in sync when item actions appear (suit, tank, coat…).
            if (_playerManager.LocalEntity is { } local &&
                !IsTransientActionBody(local))
                PersistLayout(local);
        }
    }

    private void OnActionRemoved(EntityUid actionId)
    {
        if (_container == null)
            return;

        if (actionId == SelectingTargetFor)
            StopTargeting();

        if (IsPagedMode)
        {
            foreach (var page in _pages)
            {
                for (var i = 0; i < page.Size; i++)
                {
                    if (page[i] == actionId)
                        page[i] = null;
                }
            }
        }
        else
        {
            _actions.RemoveAll(x => x == actionId);
        }
    }

    private void OnActionsUpdated()
    {
        QueueWindowUpdate();

        if (_pendingLoadFrom != null)
            TryRestoreLayout();

        RefreshActionContainer();
    }

    private void ActionButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
            return;
        }

        _window.Open();
    }

    private void UpdateFilterLabel()
    {
        if (_window == null)
            return;

        if (_window.FilterButton.SelectedKeys.Count == 0)
        {
            _window.FilterLabel.Visible = false;
        }
        else
        {
            _window.FilterLabel.Visible = true;
            _window.FilterLabel.Text = Loc.GetString("ui-actionmenu-filter-label",
                ("selectedLabels", string.Join(", ", _window.FilterButton.SelectedLabels)));
        }
    }

    private bool MatchesFilter(Entity<ActionComponent> ent, Filters filter)
    {
        var (uid, comp) = ent;
        return filter switch
        {
            Filters.Enabled => comp.Enabled,
            Filters.Item => comp.Container != null && comp.Container != _playerManager.LocalEntity,
            Filters.Innate => comp.Container == null || comp.Container == _playerManager.LocalEntity,
            Filters.Instant => EntityManager.HasComponent<InstantActionComponent>(uid),
            Filters.Targeted => EntityManager.HasComponent<TargetActionComponent>(uid),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private void ClearList()
    {
        if (_window?.Disposed == false)
            _window.ResultsGrid.RemoveAllChildren();
    }

    private void PopulateActions(IEnumerable<Entity<ActionComponent>> actions)
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        _window.UpdateNeeded = false;

        List<ActionButton> existing = new(_window.ResultsGrid.ChildCount);
        foreach (var child in _window.ResultsGrid.Children)
        {
            if (child is ActionButton button)
                existing.Add(button);
        }

        int i = 0;
        foreach (var action in actions)
        {
            if (i < existing.Count)
            {
                existing[i++].UpdateData(action, _actionsSystem);
                continue;
            }

            var button = new ActionButton(EntityManager, _spriteSystem, this) {Locked = true};
            button.ActionPressed += OnWindowActionPressed;
            button.ActionUnpressed += OnWindowActionUnPressed;
            button.ActionFocusExited += OnWindowActionFocusExisted;
            button.UpdateData(action, _actionsSystem);
            _window.ResultsGrid.AddChild(button);
        }

        for (; i < existing.Count; i++)
        {
            existing[i].Dispose();
        }
    }

    public void QueueWindowUpdate()
    {
        if (_window != null)
            _window.UpdateNeeded = true;
    }

    private void SearchAndDisplay()
    {
        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_actionsSystem == null)
            return;

        if (_playerManager.LocalEntity is not { } player)
            return;

        var search = _window.SearchBar.Text;
        var filters = _window.FilterButton.SelectedKeys;
        var actions = _actionsSystem.GetClientActions();

        if (filters.Count == 0 && string.IsNullOrWhiteSpace(search))
        {
            PopulateActions(actions);
            return;
        }

        actions = actions.Where(action =>
        {
            if (filters.Count > 0 && filters.Any(filter => !MatchesFilter(action, filter)))
                return false;

            if (action.Comp.Keywords.Any(keyword => search.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;

            var name = EntityManager.GetComponent<MetaDataComponent>(action).EntityName;
            if (name.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            if (action.Comp.Container == null || action.Comp.Container == player)
                return false;

            var providerName = EntityManager.GetComponent<MetaDataComponent>(action.Comp.Container.Value).EntityName;
            return providerName.Contains(search, StringComparison.OrdinalIgnoreCase);
        });

        PopulateActions(actions);
    }

    private void SetAction(ActionButton button, EntityUid? actionId, bool updateSlots = true)
    {
        if (_actionsSystem == null)
            return;

        int position;

        if (IsPagedMode)
        {
            if (actionId == null)
            {
                button.ClearData();
                if (_container?.TryGetButtonIndex(button, out position) ?? false)
                    CurrentPage[position] = null;
            }
            else if (button.TryReplaceWith(actionId.Value, _actionsSystem) &&
                     _container != null &&
                     _container.TryGetButtonIndex(button, out position))
            {
                CurrentPage[position] = actionId;
            }

            if (updateSlots)
            {
                RefreshActionContainer();
                PersistLayout();
            }
            return;
        }

        if (actionId == null)
        {
            button.ClearData();
            if (_container?.TryGetButtonIndex(button, out position) ?? false)
            {
                if (_actions.Count > position && position >= 0)
                    _actions.RemoveAt(position);
            }
        }
        else if (button.TryReplaceWith(actionId.Value, _actionsSystem) &&
            _container != null &&
            _container.TryGetButtonIndex(button, out position))
        {
            if (position >= _actions.Count)
            {
                _actions.Add(actionId);
            }
            else
            {
                _actions[position] = actionId;
            }
        }

        if (updateSlots)
        {
            RefreshActionContainer();
            PersistLayout();
        }
    }

    private void DragAction()
    {
        if (_menuDragHelper.Dragged is not {Action: {} action} dragged)
        {
            _menuDragHelper.EndDrag();
            return;
        }

        EntityUid? swapAction = null;
        var currentlyHovered = UIManager.MouseGetControl(_input.MouseScreenPosition);
        if (currentlyHovered is ActionButton button)
        {
            swapAction = button.Action;
            SetAction(button, action, false);
        }

        if (dragged.Parent is ActionButtonContainer)
            SetAction(dragged, swapAction, false);

        RefreshActionContainer();
        PersistLayout();

        _menuDragHelper.EndDrag();
    }

    private void OnClearPressed(ButtonEventArgs args)
    {
        if (_window == null)
            return;

        _window.SearchBar.Clear();
        _window.FilterButton.DeselectAll();
        UpdateFilterLabel();
        QueueWindowUpdate();
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        QueueWindowUpdate();
    }

    private void OnFilterSelected(ItemPressedEventArgs args)
    {
        UpdateFilterLabel();
        QueueWindowUpdate();
    }

    private void OnWindowActionPressed(GUIBoundKeyEventArgs args, ActionButton action)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionPressed(args, action);
    }

    private void OnWindowActionUnPressed(GUIBoundKeyEventArgs args, ActionButton dragged)
    {
        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.Use)
            return;

        HandleActionUnpressed(args, dragged);
    }

    private void OnWindowActionFocusExisted(ActionButton button)
    {
        _menuDragHelper.EndDrag();
    }

    private void OnActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            SetAction(button, null);
            args.Handle();
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionPressed(args, button);
    }

    private void HandleActionPressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        args.Handle();
        if (button.Action != null)
        {
            // Goobstation - only allow drag if lock setting is off or actions menu is open
            if (!_cfg.GetCVar(GoobCVars.LockActionBarDrag) || _window is { IsOpen: true })
                _menuDragHelper.MouseDown(button);
            return;
        }

        // good job
    }

    private void OnActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        HandleActionUnpressed(args, button);
    }

    private void HandleActionUnpressed(GUIBoundKeyEventArgs args, ActionButton button)
    {
        if (_actionsSystem == null)
            return;

        args.Handle();

        if (_menuDragHelper.IsDragging)
        {
            DragAction();
            return;
        }

        _menuDragHelper.EndDrag();

        if (button.Action is not {} action)
            return;

        // TODO: make this an event
        if (!EntityManager.TryGetComponent<TargetActionComponent>(action, out var target))
        {
            _actionsSystem?.TriggerAction(action);
            return;
        }

        // for target actions, we go into "select target" mode, we don't
        // message the server until we actually pick our target.

        // if we're clicking the same thing we're already targeting for, then we simply cancel
        // targeting
        ToggleTargeting((action, action.Comp, target));
    }

    private bool OnMenuBeginDrag()
    {
        // TODO ACTIONS
        // The dragging icon shuld be based on the entity's icon style. I.e. if the action has a large icon texture,
        // and a small item/provider sprite, then the dragged icon should be the big texture, not the provider.
        if (_menuDragHelper.Dragged?.Action is {} action)
        {
            if (EntityManager.TryGetComponent(action.Comp.EntityIcon, out SpriteComponent? sprite)
                && sprite.Icon?.GetFrame(RsiDirection.South, 0) is {} frame)
            {
                _dragShadow.Texture = frame;
            }
            else if (action.Comp.Icon is {} icon)
            {
                _dragShadow.Texture = _spriteSystem.Frame0(icon);
            }
            else
            {
                _dragShadow.Texture = null;
            }
        }

        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        return true;
    }

    private bool OnMenuContinueDrag(float frameTime)
    {
        LayoutContainer.SetPosition(_dragShadow, UIManager.MousePositionScaled.Position - new Vector2(32, 32));
        _dragShadow.Visible = true;
        return true;
    }

    private void OnMenuEndDrag()
    {
        _dragShadow.Texture = null;
        _dragShadow.Visible = false;
    }

    private void UnloadGui()
    {
        _actionsSystem?.UnlinkAllActions();

        if (ActionsBar == null)
        {
            return;
        }

        ActionsBar.PageButtons.LeftArrow.OnPressed -= OnLeftArrowPressed;
        ActionsBar.PageButtons.RightArrow.OnPressed -= OnRightArrowPressed;

        if (_window != null)
        {
            _window.OnOpen -= OnWindowOpened;
            _window.OnClose -= OnWindowClosed;
            _window.ClearButton.OnPressed -= OnClearPressed;
            _window.SearchBar.OnTextChanged -= OnSearchChanged;
            _window.FilterButton.OnItemSelected -= OnFilterSelected;

            _window.Dispose();
            _window = null;
        }
    }

    private void LoadGui()
    {
        UnloadGui();
        _window = UIManager.CreateWindow<ActionsWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        _window.OnOpen += OnWindowOpened;
        _window.OnClose += OnWindowClosed;
        _window.ClearButton.OnPressed += OnClearPressed;
        _window.SearchBar.OnTextChanged += OnSearchChanged;
        _window.FilterButton.OnItemSelected += OnFilterSelected;

        if (ActionsBar == null)
        {
            return;
        }

        ActionsBar.PageButtons.LeftArrow.OnPressed += OnLeftArrowPressed;
        ActionsBar.PageButtons.RightArrow.OnPressed += OnRightArrowPressed;
        UpdatePageButtons();

        RegisterActionContainer(ActionsBar.ActionsContainer);

        _actionsSystem?.LinkAllActions();
    }

    public void RegisterActionContainer(ActionButtonContainer container)
    {
        if (_container != null)
        {
            _container.ActionPressed -= OnActionPressed;
            _container.ActionUnpressed -= OnActionUnpressed;
        }

        _container = container;
        _container.ActionPressed += OnActionPressed;
        _container.ActionUnpressed += OnActionUnpressed;
    }

    private void ClearActions()
    {
        _container?.ClearActionData();
    }

    private void AssignSlots(List<SlotAssignment> assignments)
    {
        if (_actionsSystem == null)
            return;

        if (IsPagedMode)
        {
            foreach (var page in _pages)
                page.Clear();

            foreach (var assign in assignments)
            {
                EnsurePageCapacity(assign.Hotbar + 1);
                if (assign.Slot < PagedSlotCount)
                    _pages[assign.Hotbar][assign.Slot] = assign.ActionId;
            }
        }
        else
        {
            _actions.Clear();
            foreach (var assign in assignments)
            {
                _actions.Add(assign.ActionId);
            }
        }

        RefreshActionContainer();
    }

    public void RemoveActionContainer()
    {
        _container = null;
    }

    public void OnSystemLoaded(ActionsSystem system)
    {
        system.LinkActions += OnComponentLinked;
        system.UnlinkActions += OnComponentUnlinked;
        system.ClearAssignments += ClearActions;
        system.AssignSlot += AssignSlots;
    }

    public void OnSystemUnloaded(ActionsSystem system)
    {
        system.LinkActions -= OnComponentLinked;
        system.UnlinkActions -= OnComponentUnlinked;
        system.ClearAssignments -= ClearActions;
        system.AssignSlot -= AssignSlots;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        _menuDragHelper.Update(args.DeltaSeconds);
        if (_window is {UpdateNeeded: true})
            SearchAndDisplay();

        // Goobstation start
        if (_mark == null)
            return;

        if (EntityManager.HasComponent<SwapSpellComponent>(SelectingTargetFor))
            return;

        if (!EntityManager.TryGetComponent(SelectingTargetFor, out LockOnMarkActionComponent? lockOnMark))
        {
            _mark.SetMark(null);
            return;
        }

        var coords = _eye.PixelToMap(_input.MouseScreenPosition);

        var targets =
            _lookup.GetEntitiesInRange<MobStateComponent>(coords, lockOnMark.LockOnRadius, LookupFlags.Dynamic);
        var xformQuery = EntityManager.GetEntityQuery<TransformComponent>();
        var damageableQuery = EntityManager.GetEntityQuery<DamageableComponent>();
        List<(float range, EntityUid target)> selectedTargets = new();
        foreach (var (target, _) in targets)
        {
            if (target == _playerManager.LocalEntity)
                continue;

            if (!damageableQuery.HasComp(target))
                continue;

            if (!xformQuery.TryGetComponent(target, out var targetXform))
                continue;

            var range = (_transform.GetMapCoordinates(target, targetXform).Position - coords.Position).Length();
            selectedTargets.Add((range, target));
        }

        if (selectedTargets.Count == 0)
        {
            _mark.SetMark(null);
            return;
        }

        _mark.SetMark(selectedTargets.MinBy(x => x.range).target);
        // Goobstation end
    }

    private void OnComponentLinked(ActionsComponent component)
    {
        if (_actionsSystem == null)
            return;

        LoadDefaultActions();

        if (_playerManager.LocalEntity is not { } local ||
            IsTransientActionBody(local) ||
            !EntityManager.HasComponent<ActionBarLayoutComponent>(local))
        {
            _pendingLoadFrom = null;
            _pendingRestoreSince = null;
            RefreshActionContainer();
            UpdatePageButtons();
            QueueWindowUpdate();
            return;
        }

        if (!TryRestoreLayout())
        {
            _pendingLoadFrom = local;
            _pendingRestoreSince ??= _timing.CurTime;
        }

        RefreshActionContainer();
        UpdatePageButtons();
        QueueWindowUpdate();
    }

    private void OnComponentUnlinked()
    {
        // Drop in-flight remaps so the next attach cannot punch holes into a new bar.
        _pendingLoadFrom = null;
        _container?.ClearActionData();
        QueueWindowUpdate();
        StopTargeting();
    }

    private void LoadDefaultActions()
    {
        if (_actionsSystem == null)
            return;

        var actions = _actionsSystem.GetClientActions().Where(action => action.Comp.AutoPopulate).ToList();
        actions.Sort(ActionComparer);

        if (IsPagedMode)
        {
            foreach (var page in _pages)
                page.Clear();

            EnsurePageCapacity(Math.Max(1, (actions.Count + PagedSlotCount - 1) / PagedSlotCount));

            var offset = 0;
            foreach (var page in _pages)
            {
                for (var slot = 0; slot < page.Size; slot++)
                {
                    var actionIndex = slot + offset;
                    page[slot] = actionIndex < actions.Count ? actions[actionIndex].Owner : null;
                }

                offset += page.Size;
                if (offset >= actions.Count)
                    break;
            }

            _currentPageIndex = DefaultPageIndex;
            _actions.Clear();
            _layoutIsPaged = true;
            return;
        }

        _actions.Clear();
        foreach (var (action, _) in actions)
        {
            if (!_actions.Contains(action))
                _actions.Add(action);
        }

        _layoutIsPaged = false;
    }

    /// <summary>
    /// If currently targeting with this slot, stops targeting.
    /// If currently targeting with no slot or a different slot, switches to
    /// targeting with the specified slot.
    /// </summary>
    private void ToggleTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        if (SelectingTargetFor == ent)
        {
            StopTargeting();
            return;
        }

        StartTargeting(ent);
    }

    /// <summary>
    /// Puts us in targeting mode, where we need to pick either a target point or entity
    /// </summary>
    private void StartTargeting(Entity<ActionComponent, TargetActionComponent> ent)
    {
        var (uid, action, target) = ent;

        // If we were targeting something else we should stop
        StopTargeting();

        // Goobstation
        if (EntityManager.TryGetComponent(ent, out WorldTargetActionComponent? worldTarget) &&
            worldTarget.Event is InstantWorldTargetActionEvent)
            _actionsSystem?.TriggerAction(ent, true); // We just perform it and hope for the best :godo:

        SelectingTargetFor = uid;
        // TODO inform the server
        _actionsSystem?.SetToggled(uid, true);

        // override "held-item" overlay
        var provider = action.Container;

        if (target.TargetingIndicator && _overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
        {
            if (action.ItemIconStyle == ItemActionIconStyle.BigItem && action.Container != null)
            {
                handOverlay.EntityOverride = provider;
            }
            else if (action.Toggled && action.IconOn != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.IconOn);
            else if (action.Icon != null)
                handOverlay.IconOverride = _spriteSystem.Frame0(action.Icon);
        }

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == uid)
                    button.UpdateIcons();
            }
        }

        // TODO: allow world-targets to check valid positions. E.g., maybe:
        // - Draw a red/green ghost entity
        // - Add a yes/no checkmark where the HandItemOverlay usually is

        // Highlight valid entity targets
        if (!EntityManager.TryGetComponent<EntityTargetActionComponent>(uid, out var entity))
            return;

        if (EntityManager.HasComponent<SwapSpellComponent>(uid) && _playerManager.LocalEntity != null) // Goobstation
            _spells?.SetSwapSecondaryTarget(_playerManager.LocalEntity.Value, null, uid);

        Func<EntityUid, bool>? predicate = null;
        var attachedEnt = action.AttachedEntity;

        if (!entity.CanTargetSelf)
            predicate = e => e != attachedEnt;

        var range = target.CheckCanAccess ? target.Range : -1;

        _interactionOutline?.SetEnabled(false);
        _targetOutline?.Enable(range, target.CheckCanAccess, predicate, entity.Whitelist, entity.Blacklist, null);
    }

    /// <summary>
    /// Switch out of targeting mode if currently selecting target for an action
    /// </summary>
    private void StopTargeting()
    {
        _mark?.SetMark(null); // Goobstation

        if (SelectingTargetFor == null)
            return;

        var oldAction = SelectingTargetFor;
        // TODO inform the server
        _actionsSystem?.SetToggled(oldAction, false);

        // Goobstation
        if (EntityManager.HasComponent<SwapSpellComponent>(oldAction.Value) && _playerManager.LocalEntity != null)
            _spells?.SetSwapSecondaryTarget(_playerManager.LocalEntity.Value, null, oldAction.Value);

        SelectingTargetFor = null;

        _targetOutline?.Disable();
        _interactionOutline?.SetEnabled(true);

        if (_container != null)
        {
            foreach (var button in _container.GetButtons())
            {
                if (button.Action?.Owner == oldAction)
                    button.UpdateIcons();
            }
        }

        if (!_overlays.TryGetOverlay<ShowHandItemOverlay>(out var handOverlay))
            return;

        handOverlay.IconOverride = null;
        handOverlay.EntityOverride = null;
    }

    private readonly record struct SavedSlot(
        EntityUid? Action,
        string? ProtoId,
        string? ContainerProtoId = null,
        EntityUid? Container = null)
    {
        public bool IsEmpty => Action == null && ProtoId == null;
    }

    private sealed class SavedActionLayout
    {
        public bool IsPaged;
        public int CurrentPage;
        public List<List<SavedSlot>> Pages { get; } = new();

        public bool IsEmpty => Pages.Count == 0 || Pages.All(p => p.Count == 0 || p.All(a => a.IsEmpty));

        public int NonNullCount
        {
            get
            {
                var count = 0;
                foreach (var page in Pages)
                {
                    foreach (var slot in page)
                    {
                        if (!slot.IsEmpty)
                            count++;
                    }
                }

                return count;
            }
        }

        public List<SavedSlot> Flatten()
        {
            var flat = new List<SavedSlot>();
            foreach (var page in Pages)
                flat.AddRange(page);
            return flat;
        }

        public SavedActionLayout Clone()
        {
            var clone = new SavedActionLayout
            {
                IsPaged = IsPaged,
                CurrentPage = CurrentPage,
            };
            foreach (var page in Pages)
                clone.Pages.Add(new List<SavedSlot>(page));
            return clone;
        }
    }

    private sealed class ActionPage
    {
        private readonly EntityUid?[] _data;

        public ActionPage(int size)
        {
            _data = new EntityUid?[size];
        }

        public EntityUid? this[int index]
        {
            get => _data[index];
            set => _data[index] = value;
        }

        public int Size => _data.Length;

        public void Clear()
        {
            Array.Fill(_data, null);
        }

        public List<EntityUid?> ToList()
        {
            return _data.ToList();
        }

        public static implicit operator EntityUid?[](ActionPage page)
        {
            return page._data.ToArray();
        }
    }
}
