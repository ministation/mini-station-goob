// SPDX-FileCopyrightText: 2022 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr.@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <jmaster9999@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 wrexbe <wrexbe@protonmail.com>
// SPDX-FileCopyrightText: 2023 Justin Trotter <trotter.justin@gmail.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Kevin Zheng <kevinz5000@gmail.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Errant <35878406+Errant-4@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 SpaceManiac <tad@platymuus.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Mini.DailyQuests;
using Content.Client._Mini.Objectives;
using Content.Client.CharacterInfo;
using Content.Shared._Mini.DailyQuests;
using Content.Client.Gameplay;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Character.Controls;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Shared.Input;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Client.CharacterInfo.CharacterInfoSystem;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.Character;

[UsedImplicitly]
public sealed class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>, IOnSystemChanged<DailyQuestUiSystem>
{
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly DailyQuestUiSystem _dailyQuests = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MindRoleTypeChangedEvent>(OnRoleTypeChanged);
    }

    private CharacterWindow? _window;
    private MenuButton? CharacterButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.CharacterButton;
    private readonly List<PanelContainer> _dailyQuestSlots = new();
    private readonly List<DailyQuestCardControl> _dailyQuestCards = new();
    private readonly List<DailyQuestEntry> _dailyQuestLayoutCache = new();
    private readonly List<PanelContainer> _objectiveSlots = new();
    private readonly List<AntagObjectiveCardControl> _objectiveCards = new();
    private readonly List<ObjectiveInfo> _objectiveLayoutCache = new();
    private AntagObjectiveCardControl? _rewardCard;
    private AntagObjectiveCardControl? _briefingCard;
    private string? _briefingCache;
    private bool _rewardAllCompleteCache;
    private bool _rewardGrantedCache;
    private readonly List<Control> _characterInfoControls = new();
    private EntityUid _characterInfoEntity;
    private int _characterInfoControlCount = -1;
    private CharacterData? _cachedCharacterData;
    private float _objectiveRefreshAccumulator;
    private const float ObjectiveRefreshInterval = 1f;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<CharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenCharacterMenu,
                InputCmdHandler.FromDelegate(_ => ToggleWindow()))
            .Register<CharacterUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }

        _dailyQuestSlots.Clear();
        _dailyQuestCards.Clear();
        _dailyQuestLayoutCache.Clear();

        CommandBinds.Unregister<CharacterUIController>();
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        _player.LocalPlayerDetached -= CharacterDetached;
    }

    public void OnSystemLoaded(DailyQuestUiSystem system)
    {
        system.QuestsUpdated += OnDailyQuestsUpdated;
        RefreshDailyQuests(system.Quests, system.TimeInterpSeconds);
    }

    public void OnSystemUnloaded(DailyQuestUiSystem system)
    {
        system.QuestsUpdated -= OnDailyQuestsUpdated;
    }

    public void UnloadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed -= CharacterButtonPressed;
    }

    public void LoadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed += CharacterButtonPressed;
    }

    private void DeactivateButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.Pressed = false;
    }

    private void ActivateButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.Pressed = true;
    }

    private void CharacterUpdated(CharacterData data)
    {
        if (_window == null)
            return;

        var preserveScroll = _window.IsOpen;
        var scrollPos = preserveScroll ? _window.ContentScroll.VScroll : 0f;

        if (_cachedCharacterData is { } cached && IsProgressOnlyUpdate(cached, data))
        {
            ApplyObjectiveProgressUpdate(data);
            _cachedCharacterData = data;
            if (preserveScroll)
                RestoreScroll(scrollPos);
            return;
        }

        var (entity, job, objectives, briefing, entityName, antagAllComplete, antagCoinGranted) = data;

        _window.SpriteView.SetEntity(entity);
        UpdateRoleType();

        if (_window.NameLabel.Text != entityName)
            _window.NameLabel.Text = entityName;

        if (_window.SubText.Text != job)
            _window.SubText.Text = job;

        _window.ObjectivesLabel.Visible = objectives.Any();

        RefreshAntagObjectives(objectives, antagAllComplete, antagCoinGranted, briefing);
        RefreshCharacterInfoControls(entity);

        _window.RolePlaceholder.Visible = briefing == null
            && _characterInfoControls.Count == 0
            && !objectives.Any()
            && !_dailyQuests.Quests.Any();

        _cachedCharacterData = data;
        if (preserveScroll)
            RestoreScroll(scrollPos);
    }

    private void ApplyObjectiveProgressUpdate(CharacterData data)
    {
        if (_window == null)
            return;

        var flatObjectives = data.Objectives.SelectMany(pair => pair.Value).ToList();

        for (var i = 0; i < flatObjectives.Count && i < _objectiveCards.Count; i++)
            _objectiveCards[i].UpdateObjectiveProgress(flatObjectives[i]);

        if (_rewardCard != null
            && (_rewardAllCompleteCache != data.AntagAllObjectivesComplete
                || _rewardGrantedCache != data.AntagObjectiveCoinRewardGranted))
        {
            _rewardCard.UpdateRewardFooter(data.AntagAllObjectivesComplete, data.AntagObjectiveCoinRewardGranted);
            _rewardAllCompleteCache = data.AntagAllObjectivesComplete;
            _rewardGrantedCache = data.AntagObjectiveCoinRewardGranted;
        }

        for (var i = 0; i < flatObjectives.Count; i++)
        {
            if (i < _objectiveLayoutCache.Count)
                _objectiveLayoutCache[i] = flatObjectives[i];
        }
    }

    private static bool IsProgressOnlyUpdate(CharacterData prev, CharacterData next)
    {
        if (prev.Entity != next.Entity)
            return false;

        if (prev.Job != next.Job || prev.EntityName != next.EntityName)
            return false;

        if (prev.Briefing != next.Briefing)
            return false;

        var prevFlat = prev.Objectives.SelectMany(pair => pair.Value).ToList();
        var nextFlat = next.Objectives.SelectMany(pair => pair.Value).ToList();

        if (prevFlat.Count != nextFlat.Count)
            return false;

        for (var i = 0; i < prevFlat.Count; i++)
        {
            var a = prevFlat[i];
            var b = nextFlat[i];
            if (a.Title != b.Title
                || a.Description != b.Description)
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreScroll(float scrollPos)
    {
        if (_window == null || !_window.IsOpen)
            return;

        if (MathF.Abs(_window.ContentScroll.VScroll - scrollPos) > 0.01f)
            _window.ContentScroll.VScroll = scrollPos;
    }

    private void RefreshCharacterInfoControls(EntityUid entity)
    {
        if (_window == null)
            return;

        var controls = _characterInfo.GetCharacterInfoControls(entity);
        if (_characterInfoEntity == entity
            && _characterInfoControlCount == controls.Count
            && _characterInfoControls.Count == controls.Count)
        {
            return;
        }

        foreach (var control in _characterInfoControls)
            control.Orphan();

        _characterInfoControls.Clear();
        _window.CharacterExtras.RemoveAllChildren();

        foreach (var control in controls)
        {
            _window.CharacterExtras.AddChild(control);
            _characterInfoControls.Add(control);
        }

        _characterInfoEntity = entity;
        _characterInfoControlCount = controls.Count;
    }

    private void RefreshAntagObjectives(
        Dictionary<string, List<ObjectiveInfo>> objectives,
        bool antagAllComplete,
        bool antagCoinGranted,
        string? briefing)
    {
        if (_window == null)
            return;

        var flatObjectives = objectives.SelectMany(pair => pair.Value).ToList();
        var rebuildLayout = NeedsObjectiveLayoutRebuild(flatObjectives, briefing);

        if (rebuildLayout)
        {
            _window.Objectives.RemoveAllChildren();
            _objectiveSlots.Clear();
            _objectiveCards.Clear();
            _objectiveLayoutCache.Clear();
            _characterInfoControls.Clear();
            _rewardCard = null;
            _briefingCard = null;
            _briefingCache = null;

            var groupIndex = 0;
            foreach (var (groupId, conditions) in objectives)
            {
                if (objectives.Count > 1)
                {
                    var groupMessage = new FormattedMessage();
                    groupMessage.TryAddMarkup(groupId, out _);

                    var groupLabel = new RichTextLabel
                    {
                        StyleClasses = { StyleNano.StyleClassTooltipActionTitle },
                        HorizontalAlignment = Control.HAlignment.Center,
                        Margin = new Thickness(0, groupIndex == 0 ? 0 : 4, 0, 2),
                    };
                    groupLabel.SetMessage(groupMessage);
                    _window.Objectives.AddChild(groupLabel);
                }

                foreach (var condition in conditions)
                {
                    var slot = CreateObjectiveSlot();
                    var card = new AntagObjectiveCardControl
                    {
                        HorizontalExpand = true,
                        VerticalExpand = false,
                    };
                    card.SetObjective(condition, _sprite.Frame0(condition.Icon));
                    slot.AddChild(card);
                    _window.Objectives.AddChild(slot);
                    _objectiveSlots.Add(slot);
                    _objectiveCards.Add(card);
                }

                groupIndex++;
            }

            if (flatObjectives.Count > 0)
            {
                var rewardSlot = CreateObjectiveSlot();
                _rewardCard = new AntagObjectiveCardControl
                {
                    HorizontalExpand = true,
                    VerticalExpand = false,
                };
                _rewardCard.SetRewardFooter(antagAllComplete, antagCoinGranted);
                rewardSlot.AddChild(_rewardCard);
                _window.Objectives.AddChild(rewardSlot);
            }

            if (briefing != null)
            {
                var briefingSlot = CreateObjectiveSlot();
                _briefingCard = new AntagObjectiveCardControl
                {
                    HorizontalExpand = true,
                    VerticalExpand = false,
                };
                _briefingCard.SetBriefing(briefing);
                briefingSlot.AddChild(_briefingCard);
                _window.Objectives.AddChild(briefingSlot);
            }

            _objectiveLayoutCache.Clear();
            _objectiveLayoutCache.AddRange(flatObjectives);
            _briefingCache = briefing;
            _rewardAllCompleteCache = antagAllComplete;
            _rewardGrantedCache = antagCoinGranted;
            return;
        }

        var cardIndex = 0;
        foreach (var condition in flatObjectives)
        {
            _objectiveCards[cardIndex].UpdateObjectiveProgress(condition);
            cardIndex++;
        }

        if (_rewardCard != null
            && (_rewardAllCompleteCache != antagAllComplete || _rewardGrantedCache != antagCoinGranted))
        {
            _rewardCard.UpdateRewardFooter(antagAllComplete, antagCoinGranted);
            _rewardAllCompleteCache = antagAllComplete;
            _rewardGrantedCache = antagCoinGranted;
        }

        for (var i = 0; i < flatObjectives.Count; i++)
            _objectiveLayoutCache[i] = flatObjectives[i];
    }

    private static PanelContainer CreateObjectiveSlot()
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = false,
            RectClipContent = false,
        };
    }

    private bool NeedsObjectiveLayoutRebuild(IReadOnlyList<ObjectiveInfo> objectives, string? briefing)
    {
        if (_objectiveLayoutCache.Count != objectives.Count || _briefingCache != briefing)
            return true;

        for (var i = 0; i < objectives.Count; i++)
        {
            var cached = _objectiveLayoutCache[i];
            var current = objectives[i];
            if (cached.Title != current.Title
                || cached.Description != current.Description)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDailyQuestsUpdated(IReadOnlyList<DailyQuestEntry> quests, float smoothTimeExtra)
    {
        RefreshDailyQuests(quests, smoothTimeExtra);
    }

    private void RefreshDailyQuests(IReadOnlyList<DailyQuestEntry> quests, float smoothTimeExtra = 0f)
    {
        if (_window == null)
            return;

        _window.DailyQuestsLabel.Visible = quests.Count > 0;

        if (quests.Count == 0)
        {
            _window.DailyQuests.RemoveAllChildren();
            _dailyQuestSlots.Clear();
            _dailyQuestCards.Clear();
            _dailyQuestLayoutCache.Clear();
            _window.RolePlaceholder.Visible = _window.Objectives.ChildCount == 0
                && string.IsNullOrWhiteSpace(_window.SubText.Text);
            return;
        }

        var rebuildLayout = NeedsDailyQuestLayoutRebuild(quests);
        EnsureDailyQuestSlots(quests.Count);

        if (rebuildLayout)
        {
            _window.DailyQuests.RemoveAllChildren();
            for (var i = 0; i < quests.Count; i++)
            {
                _dailyQuestSlots[i].HorizontalExpand = true;
                _dailyQuestSlots[i].VerticalExpand = false;
                if (_dailyQuestSlots[i].Parent != null)
                    _dailyQuestSlots[i].Orphan();
                _window.DailyQuests.AddChild(_dailyQuestSlots[i]);
            }

            _dailyQuestLayoutCache.Clear();
            _dailyQuestLayoutCache.AddRange(quests);
        }

        for (var i = 0; i < quests.Count; i++)
            _dailyQuestCards[i].SetQuest(quests[i], smoothTimeExtra);

        while (_dailyQuestSlots.Count > quests.Count)
        {
            _dailyQuestSlots.RemoveAt(_dailyQuestSlots.Count - 1);
            _dailyQuestCards.RemoveAt(_dailyQuestCards.Count - 1);
        }

        _window.RolePlaceholder.Visible = false;
    }

    private bool NeedsDailyQuestLayoutRebuild(IReadOnlyList<DailyQuestEntry> quests)
    {
        if (_dailyQuestLayoutCache.Count != quests.Count)
            return true;

        for (var i = 0; i < quests.Count; i++)
        {
            var a = _dailyQuestLayoutCache[i];
            var b = quests[i];
            if (a.QuestId != b.QuestId
                || a.IsClaimed != b.IsClaimed
                || a.IsCompleted != b.IsCompleted)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureDailyQuestSlots(int count)
    {
        while (_dailyQuestSlots.Count < count)
        {
            var slot = new PanelContainer
            {
                HorizontalExpand = true,
                VerticalExpand = false,
                RectClipContent = true,
                MinSize = new Vector2(0, DailyQuestCardControl.CompactQuestCardHeight),
            };

            var card = new DailyQuestCardControl(compact: true)
            {
                HorizontalExpand = true,
                VerticalExpand = false,
            };
            slot.AddChild(card);
            _dailyQuestSlots.Add(slot);
            _dailyQuestCards.Add(card);
        }
    }

    private void OnRoleTypeChanged(MindRoleTypeChangedEvent ev, EntitySessionEventArgs _)
    {
        UpdateRoleType();
    }

    private void UpdateRoleType()
    {
        if (_window == null || !_window.IsOpen)
            return;

        if (!_ent.TryGetComponent<MindContainerComponent>(_player.LocalEntity, out var container)
            || container.Mind is null)
            return;

        if (!_ent.TryGetComponent<MindComponent>(container.Mind.Value, out var mind))
            return;

        if (!_prototypeManager.TryIndex(mind.RoleType, out var proto))
            Log.Error($"Player '{_player.LocalSession}' has invalid Role Type '{mind.RoleType}'. Displaying default instead");

        _window.RoleType.Text = Loc.GetString(proto?.Name ?? "role-type-crew-aligned-name");
        _window.RoleType.FontColorOverride = proto?.Color ?? Color.White;
    }

    private void CharacterDetached(EntityUid uid)
    {
        CloseWindow();
    }

    private void CharacterButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void CloseWindow()
    {
        _window?.Close();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_window?.IsOpen != true)
            return;

        _objectiveRefreshAccumulator += args.DeltaSeconds;
        if (_objectiveRefreshAccumulator < ObjectiveRefreshInterval)
            return;

        _objectiveRefreshAccumulator = 0f;
        _characterInfo.RequestCharacterInfo();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        CharacterButton?.SetClickPressed(!_window.IsOpen);

        if (_window.IsOpen)
        {
            CloseWindow();
        }
        else
        {
            _objectiveRefreshAccumulator = 0f;
            _characterInfo.RequestCharacterInfo();
            _window.Open();
        }
    }
}