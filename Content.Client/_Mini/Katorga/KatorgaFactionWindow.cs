// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Shared._Mini.Katorga;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Окно выбора фракции каторги: кнопка с названием и описанием на каждую фракцию,
///     сверху таймер до автоназначения.
/// </summary>
public sealed class KatorgaFactionWindow : DefaultWindow
{
    private readonly Label _countdown;
    private readonly BoxContainer _options;

    /// <summary>Игрок выбрал фракцию (идентификатор прототипа).</summary>
    public event Action<string>? OnSelect;

    public KatorgaFactionWindow()
    {
        Title = Loc.GetString("katorga-faction-ui-title");
        MinSize = new Vector2(420, 0);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        _countdown = new Label();
        box.AddChild(_countdown);

        _options = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        box.AddChild(_options);

        Contents.AddChild(box);
    }

    public void Update(KatorgaFactionEuiState state)
    {
        _countdown.Text = state.SecondsRemaining > 0
            ? Loc.GetString("katorga-faction-ui-countdown", ("seconds", state.SecondsRemaining))
            : Loc.GetString("katorga-faction-ui-assigning");

        _options.RemoveAllChildren();
        foreach (var option in state.Options)
        {
            var select = option;
            var button = new Button
            {
                Text = option.Name,
                MinHeight = 34,
            };
            button.OnPressed += _ => OnSelect?.Invoke(select.Id);
            _options.AddChild(button);

            var feature = new RichTextLabel
            {
                MinHeight = 30,
            };
            feature.SetMessage(FormattedMessage.FromMarkupPermissive(option.Feature));
            _options.AddChild(feature);
        }
    }
}
