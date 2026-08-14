using Content.Client._Mini.DiscordAuth;
using System.Numerics;
using Content.Client._Donate.Mini;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Configuration;
using Robust.Client.Player;

namespace Content.Client._Donate.UI;

public sealed class DonateShopWindow : MiniDefaultWindow
{
    [Dependency] private readonly IUriOpener _uriOpener = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private BoxContainer _profilePanel = default!;

    public DonateShopWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "ДОНАТ МАГАЗИН";
        MinSize = SetSize = new Vector2(700, 500);

        BuildUI();
    }

    private void BuildUI()
    {
        var mainContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 20,
            Margin = new Thickness(20)
        };

        // Инициализируем панель профиля
        _profilePanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true
        };

        // Добавляем панель в главный контейнер
        mainContainer.AddChild(_profilePanel);

        // Используем метод AddContent или AddChild
        AddContent(mainContainer);

        // Показываем загрузку
        ShowLoading();
    }

    private void ShowLoading()
    {
        _profilePanel.RemoveAllChildren();

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 20
        };

        // Создаем контейнер для кнопок
        var buttonsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 10,
            Margin = new Thickness(0, 40, 0, 0)
        };

        // Кнопка авторизации Discord
        var discordButton = new Button
        {
            Text = "Авторизация",
            HorizontalExpand = true,
            MinSize = new Vector2(250, 40),
            StyleClasses = {"ActionButton"}
        };
        discordButton.OnPressed += _ => DiscordAuthLink.TryOpen(_cfg, _playerManager, _uriOpener);
        buttonsContainer.AddChild(discordButton);

        // Кнопка донатов на сайте
        var donateButton = new Button
        {
            Text = "Купить подписку",
            HorizontalExpand = true,
            MinSize = new Vector2(250, 40),
            StyleClasses = {"ActionButton"}
        };
        donateButton.OnPressed += _ =>
        {
            _uriOpener.OpenUri(new Uri("https://ministation.ru/donate"));
        };
        buttonsContainer.AddChild(donateButton);

        container.AddChild(buttonsContainer);
        _profilePanel.AddChild(container);
    }
}
