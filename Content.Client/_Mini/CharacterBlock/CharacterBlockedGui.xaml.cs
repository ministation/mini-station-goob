// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Mini.CharacterBlock;

public sealed partial class CharacterBlockedGui : DefaultWindow
{
    [ViewVariables]
    private RichTextLabel BlockedTextLabel { get; set; } = default!;

    [ViewVariables]
    private ContainerButton CharacterContainer { get; set; } = default!;

    [ViewVariables]
    private SpriteView View { get; set; } = default!;

    [ViewVariables]
    private Label DescriptionLabel { get; set; } = default!;

    [ViewVariables]
    private Button DeleteButton { get; set; } = default!;

    [ViewVariables]
    private Button ConfirmDeleteButton { get; set; } = default!;

    public CharacterBlockedGui()
    {
        RobustXamlLoader.Load(this);

        // Здесь можно добавить логику для кнопок и заполнения данных
        // DeleteButton.OnPressed += OnDeletePressed;
        // ConfirmDeleteButton.OnPressed += OnConfirmDeletePressed;
    }


}
