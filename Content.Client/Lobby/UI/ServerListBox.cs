using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Robust.Client.Console;
using Robust.Client.Graphics;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client.Lobby.UI;

public sealed class ServerListBox : BoxContainer
{
    private const float ActionButtonHeight = 40f;
    private const float ActionIconScale = 0.36f;

    private static readonly SpriteSpecifier CoinAnimatedIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Mini/Interface/antag_tokens.rsi"), "coin_animated");

    private static readonly SpriteSpecifier ClockAnimatedIcon = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Mini/Interface/daily_rewards.rsi"), "clock_animated");

    [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
    private IGameController _gameController;
    private List<Button> _connectButtons = new();
    private IUriOpener _uriOpener;

    private void OpenDailyRewards()
    {
        _consoleHost.ExecuteCommand("dailyrewardmenu");
    }

    private void OpenAntagTokens()
    {
        _consoleHost.ExecuteCommand("antagtokenmenu");
    }

    private void OpenGhostShop()
    {
        _consoleHost.ExecuteCommand("ghostshop");
    }

    public ServerListBox()
    {
        IoCManager.InjectDependencies(this);

        _gameController = IoCManager.Resolve<IGameController>();
        _uriOpener = IoCManager.Resolve<IUriOpener>();
        Orientation = LayoutOrientation.Vertical;

        var actionButtonsContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 2,
            Margin = new Thickness(0, 0, 0, 4)
        };

        actionButtonsContainer.AddChild(CreateActionButton(
            "Терминал",
            CoinAnimatedIcon,
            OpenAntagTokens));

        actionButtonsContainer.AddChild(CreateActionButton(
            "Награды",
            ClockAnimatedIcon,
            OpenDailyRewards));

        actionButtonsContainer.AddChild(CreateActionButton(
            "Призраки",
            new SpriteSpecifier.Texture(new ResPath("/Textures/_Mini/Interface/Ghost.png")),
            OpenGhostShop));

        AddChild(actionButtonsContainer);

        var scrollContainer = new ScrollContainer
        {
            HScrollEnabled = false,
            VScrollEnabled = true,
            MinHeight = 80,
            MaxHeight = 330,
            HorizontalExpand = false,
            VerticalExpand = true
        };

        var serverContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        scrollContainer.AddChild(serverContainer);
        AddChild(scrollContainer);
    }

    private Button CreateActionButton(string text, SpriteSpecifier? icon, Action onPressed)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = ActionButtonHeight,
            MaxHeight = ActionButtonHeight,
            SetHeight = ActionButtonHeight,
        };

        var contentContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 6,
        };

        // Fixed-size icon slot so labels line up even if an icon is missing.
        var iconSlot = new Control
        {
            MinSize = new Vector2(20, 20),
            MaxSize = new Vector2(20, 20),
        };

        if (icon != null)
        {
            try
            {
                var animatedIcon = new AnimatedTextureRect
                {
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                };
                animatedIcon.DisplayRect.TextureScale = new Vector2(ActionIconScale, ActionIconScale);
                animatedIcon.SetFromSpriteSpecifier(icon);
                iconSlot.AddChild(animatedIcon);
            }
            catch
            {
                // Keep empty slot for alignment.
            }
        }

        contentContainer.AddChild(iconSlot);
        contentContainer.AddChild(new Label
        {
            Text = text,
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 90,
        });

        button.AddChild(contentContainer);
        button.OnPressed += _ => onPressed();

        return button;
    }

    private void AddServerInfo(BoxContainer container, string serverName, string serverUrl, string description, string? discord)
    {
        var serverBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            MinHeight = 20,
            Margin = new Thickness(0, 0, 0, 5)
        };

        var nameAndDescriptionBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
        };

        var serverNameLabel = new Label
        {
            Text = serverName,
            MinWidth = 150
        };

        var descriptionLabel = new RichTextLabel
        {
            MaxWidth = 500
        };
        descriptionLabel.SetMessage(FormattedMessage.FromMarkup(description));

        var buttonBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Right
        };

        var connectButton = new Button
        {
            Text = "Зайти"
        };

        if (discord != null)
        {
            var discordButton = new Button
            {
                Text = "Discord"
            };

            discordButton.OnPressed += _ =>
            {
                _uriOpener.OpenUri(discord);
            };

            buttonBox.AddChild(discordButton);
        }

        _connectButtons.Add(connectButton);

        connectButton.OnPressed += _ =>
        {
            _gameController.Redial(serverUrl, "Connecting to another server...");

            foreach (var button in _connectButtons)
            {
                button.Disabled = true;
            }
        };

        buttonBox.AddChild(connectButton);

        nameAndDescriptionBox.AddChild(serverNameLabel);
        nameAndDescriptionBox.AddChild(descriptionLabel);

        serverBox.AddChild(nameAndDescriptionBox);
        serverBox.AddChild(buttonBox);

        container.AddChild(serverBox);
    }
}
