// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarRespawnWindow : FancyWindow
{
    private readonly TypanWarRespawnBoundUserInterface _bui;
    private readonly Label _timerLabel;
    private readonly BoxContainer _optionsContainer;

    public TypanWarRespawnWindow(TypanWarRespawnBoundUserInterface bui)
    {
        _bui = bui;
        Title = Loc.GetString("typan-war-respawn-title");
        MinWidth = 360;
        MinHeight = 200;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 8,
        };
        ContentsContainer.AddChild(root);

        _timerLabel = new Label
        {
            FontColorOverride = Color.FromHex("#F0D890"),
            Align = Label.AlignMode.Center,
        };
        root.AddChild(_timerLabel);

        _optionsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        root.AddChild(_optionsContainer);
    }

    public void UpdateState(TypanWarRespawnBoundUserInterfaceState state)
    {
        if (!state.CanRespawn)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, state.SecondsRemaining));
            _timerLabel.Text = Loc.GetString("typan-war-respawn-timer",
                ("seconds", (int) Math.Ceiling(span.TotalSeconds)));
        }
        else
        {
            _timerLabel.Text = Loc.GetString("typan-war-respawn-ready");
        }

        _optionsContainer.RemoveAllChildren();

        foreach (var option in state.Options)
        {
            var button = new Button
            {
                Text = option.Label,
                ToolTip = option.Description,
                Disabled = !state.CanRespawn,
            };
            var index = option.Index;
            button.OnPressed += _ => _bui.SendRespawnRequest(index);
            _optionsContainer.AddChild(button);
        }

        if (state.CanRespawn && state.Options.Length == 0)
        {
            _optionsContainer.AddChild(new Label
            {
                Text = Loc.GetString("typan-war-respawn-no-options"),
                FontColorOverride = Color.FromHex("#C0B8A8"),
                Align = Label.AlignMode.Center,
            });
        }
    }
}
