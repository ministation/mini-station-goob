using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Content.Goobstation.UIKit.UserInterface.Controls;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;

namespace Content.Goobstation.Client.MisandryBox.Thunderdome;

public sealed class ThunderdomeLoadoutWindow : ThunderdomeWindow
{
    public event Action<ThunderdomeLoadoutSelection>? OnLoadoutConfirmed;

    private int _weaponSelection = -1;
    private int _grenadeSelection = 0;
    private int _medicalSelection = 0;
    private int _headSelection = 0;
    private int _neckSelection = 0;
    private int _glassesSelection = 0;
    private int _backpackSelection = 0;
    private int _utilitySelection = 0;
    private ThunderdomeWeaponCard? _selectedWeaponCard;
    private ThunderdomeWeaponCard? _selectedGrenadeCard;
    private ThunderdomeWeaponCard? _selectedMedicalCard;
    private ThunderdomeWeaponCard? _selectedHeadCard;
    private ThunderdomeWeaponCard? _selectedNeckCard;
    private ThunderdomeWeaponCard? _selectedGlassesCard;
    private ThunderdomeWeaponCard? _selectedBackpackCard;
    private ThunderdomeWeaponCard? _selectedUtilityCard;

    private readonly Label _playerCountLabel;
    private readonly TabContainer _tabContainer;
    private readonly BoxContainer _weaponsContainer;
    private readonly BoxContainer _grenadesContainer;
    private readonly BoxContainer _medicalsContainer;
    private readonly BoxContainer _headsContainer;
    private readonly BoxContainer _necksContainer;
    private readonly BoxContainer _glassesContainer;
    private readonly BoxContainer _backpacksContainer;
    private readonly BoxContainer _utilitiesContainer;
    private readonly ThunderdomeButton _confirmButton;

    public ThunderdomeLoadoutWindow()
    {
        WindowTitle = Loc.GetString("thunderdome-loadout-title");
        SetSize = new System.Numerics.Vector2(650, 620);
        MinSize = new System.Numerics.Vector2(650, 620);

        _playerCountLabel = new Label
        {
            Text = Loc.GetString("thunderdome-loadout-players", ("count", 0)),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = ThunderdomeTheme.SubText,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4),
        };
        Contents.AddChild(_playerCountLabel);

        var subtitle = new Label
        {
            Text = Loc.GetString("thunderdome-loadout-subtitle"),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = ThunderdomeTheme.SubText,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6),
        };
        Contents.AddChild(subtitle);

        var tabWrapper = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
        };

        _tabContainer = new TabContainer
        {
            VerticalExpand = true,
            MinWidth = 570,
        };
        tabWrapper.AddChild(_tabContainer);
        Contents.AddChild(tabWrapper);

        // Weapons tab
        var weaponsScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _weaponsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        weaponsScroll.AddChild(_weaponsContainer);
        _tabContainer.AddChild(weaponsScroll);
        _tabContainer.SetTabTitle(0, Loc.GetString("thunderdome-tab-weapons"));

        // Grenades tab
        var grenadesScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _grenadesContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        grenadesScroll.AddChild(_grenadesContainer);
        _tabContainer.AddChild(grenadesScroll);
        _tabContainer.SetTabTitle(1, Loc.GetString("thunderdome-tab-grenades"));

        // Medical tab
        var medicalsScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _medicalsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        medicalsScroll.AddChild(_medicalsContainer);
        _tabContainer.AddChild(medicalsScroll);
        _tabContainer.SetTabTitle(2, Loc.GetString("thunderdome-tab-medical"));

        // Head tab
        var headsScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _headsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        headsScroll.AddChild(_headsContainer);
        _tabContainer.AddChild(headsScroll);
        _tabContainer.SetTabTitle(3, Loc.GetString("thunderdome-tab-head"));

        // Neck tab
        var necksScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _necksContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        necksScroll.AddChild(_necksContainer);
        _tabContainer.AddChild(necksScroll);
        _tabContainer.SetTabTitle(4, Loc.GetString("thunderdome-tab-neck"));

        // Glasses tab
        var glassesScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _glassesContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        glassesScroll.AddChild(_glassesContainer);
        _tabContainer.AddChild(glassesScroll);
        _tabContainer.SetTabTitle(5, Loc.GetString("thunderdome-tab-glasses"));

        // Backpack tab
        var backpacksScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _backpacksContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        backpacksScroll.AddChild(_backpacksContainer);
        _tabContainer.AddChild(backpacksScroll);
        _tabContainer.SetTabTitle(6, Loc.GetString("thunderdome-tab-backpack"));

        // Utility tab
        var utilitiesScroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        _utilitiesContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        utilitiesScroll.AddChild(_utilitiesContainer);
        _tabContainer.AddChild(utilitiesScroll);
        _tabContainer.SetTabTitle(7, Loc.GetString("thunderdome-tab-utility"));

        _confirmButton = new ThunderdomeButton
        {
            Text = Loc.GetString("thunderdome-loadout-confirm"),
            Disabled = true,
            Margin = new Thickness(8, 6),
        };
        _confirmButton.OnPressed += () =>
        {
            if (_weaponSelection >= 0)
            {
                var selection = new ThunderdomeLoadoutSelection(
                    _weaponSelection,
                    _grenadeSelection,
                    _medicalSelection,
                    _headSelection,
                    _neckSelection,
                    _glassesSelection,
                    _backpackSelection,
                    _utilitySelection);
                OnLoadoutConfirmed?.Invoke(selection);
            }
        };
        Contents.AddChild(_confirmButton);
    }

    public void UpdateState(ThunderdomeLoadoutEuiState state)
    {
        _playerCountLabel.Text = Loc.GetString("thunderdome-loadout-players", ("count", state.PlayerCount));

        _weaponsContainer.RemoveAllChildren();
        _grenadesContainer.RemoveAllChildren();
        _medicalsContainer.RemoveAllChildren();
        _headsContainer.RemoveAllChildren();
        _necksContainer.RemoveAllChildren();
        _glassesContainer.RemoveAllChildren();
        _backpacksContainer.RemoveAllChildren();
        _utilitiesContainer.RemoveAllChildren();

        _selectedWeaponCard = null;
        _selectedGrenadeCard = null;
        _selectedMedicalCard = null;
        _selectedHeadCard = null;
        _selectedNeckCard = null;
        _selectedGlassesCard = null;
        _selectedBackpackCard = null;
        _selectedUtilityCard = null;
        _weaponSelection = state.LastWeaponSelection;
        _grenadeSelection = state.LastGrenadeSelection;
        _medicalSelection = state.LastMedicalSelection;
        _headSelection = state.LastHeadSelection;
        _neckSelection = state.LastNeckSelection;
        _glassesSelection = state.LastGlassesSelection;
        _backpackSelection = state.LastBackpackSelection;
        _utilitySelection = state.LastUtilitySelection;
        _confirmButton.Disabled = _weaponSelection < 0;

        // Weapons tab - grouped by category
        var categories = new List<(string Category, List<ThunderdomeLoadoutOption> Options)>();
        var categoryMap = new Dictionary<string, List<ThunderdomeLoadoutOption>>();

        foreach (var option in state.Weapons)
        {
            if (!categoryMap.TryGetValue(option.Category, out var list))
            {
                list = new List<ThunderdomeLoadoutOption>();
                categoryMap[option.Category] = list;
                categories.Add((option.Category, list));
            }
            list.Add(option);
        }

        foreach (var (category, options) in categories)
        {
            var header = new Label
            {
                Text = LocString(category),
                StyleClasses = { "LabelKeyText" },
                FontColorOverride = ThunderdomeTheme.HeaderText,
                Margin = new Thickness(4, 6, 0, 2),
            };
            _weaponsContainer.AddChild(header);

            foreach (var option in options)
            {
                var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
                card.OnSelected += OnWeaponCardSelected;
                _weaponsContainer.AddChild(card);

                if (option.Index == state.LastWeaponSelection)
                {
                    card.SetSelected(true);
                    _selectedWeaponCard = card;
                }
            }
        }

        // Grenades tab
        foreach (var option in state.Grenades)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnGrenadeCardSelected;
            _grenadesContainer.AddChild(card);

            if (option.Index == state.LastGrenadeSelection)
            {
                card.SetSelected(true);
                _selectedGrenadeCard = card;
            }
        }

        // Medical tab
        foreach (var option in state.Medicals)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnMedicalCardSelected;
            _medicalsContainer.AddChild(card);

            if (option.Index == state.LastMedicalSelection)
            {
                card.SetSelected(true);
                _selectedMedicalCard = card;
            }
        }

        // Head tab
        foreach (var option in state.Heads)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnHeadCardSelected;
            _headsContainer.AddChild(card);

            if (option.Index == state.LastHeadSelection)
            {
                card.SetSelected(true);
                _selectedHeadCard = card;
            }
        }

        // Neck tab
        foreach (var option in state.Necks)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnNeckCardSelected;
            _necksContainer.AddChild(card);

            if (option.Index == state.LastNeckSelection)
            {
                card.SetSelected(true);
                _selectedNeckCard = card;
            }
        }

        // Glasses tab
        foreach (var option in state.Glasses)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnGlassesCardSelected;
            _glassesContainer.AddChild(card);

            if (option.Index == state.LastGlassesSelection)
            {
                card.SetSelected(true);
                _selectedGlassesCard = card;
            }
        }

        // Backpack tab
        foreach (var option in state.Backpacks)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnBackpackCardSelected;
            _backpacksContainer.AddChild(card);

            if (option.Index == state.LastBackpackSelection)
            {
                card.SetSelected(true);
                _selectedBackpackCard = card;
            }
        }

        // Utilities tab
        foreach (var option in state.Utilities)
        {
            var card = new ThunderdomeWeaponCard(option.Index, LocString(option.Name), option.SpritePrototype, LocString(option.Description));
            card.OnSelected += OnUtilityCardSelected;
            _utilitiesContainer.AddChild(card);

            if (option.Index == state.LastUtilitySelection)
            {
                card.SetSelected(true);
                _selectedUtilityCard = card;
            }
        }
    }

    private void OnWeaponCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedWeaponCard?.SetSelected(false);

        _selectedWeaponCard = card;
        _weaponSelection = card.WeaponIndex;
        card.SetSelected(true);

        _confirmButton.Disabled = false;
    }

    private void OnGrenadeCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedGrenadeCard?.SetSelected(false);

        _selectedGrenadeCard = card;
        _grenadeSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnMedicalCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedMedicalCard?.SetSelected(false);

        _selectedMedicalCard = card;
        _medicalSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnHeadCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedHeadCard?.SetSelected(false);

        _selectedHeadCard = card;
        _headSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnNeckCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedNeckCard?.SetSelected(false);

        _selectedNeckCard = card;
        _neckSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnGlassesCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedGlassesCard?.SetSelected(false);

        _selectedGlassesCard = card;
        _glassesSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnBackpackCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedBackpackCard?.SetSelected(false);

        _selectedBackpackCard = card;
        _backpackSelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private void OnUtilityCardSelected(ThunderdomeWeaponCard card)
    {
        _selectedUtilityCard?.SetSelected(false);

        _selectedUtilityCard = card;
        _utilitySelection = card.WeaponIndex;
        card.SetSelected(true);
    }

    private static string LocString(string? key)
    {
        return string.IsNullOrEmpty(key) ? string.Empty : Loc.GetString(key);
    }
}
