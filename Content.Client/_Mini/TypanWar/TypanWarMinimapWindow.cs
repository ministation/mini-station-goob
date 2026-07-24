// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapWindow : FancyWindow
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly TypanWarMinimapControl _map;
    private readonly CheckBox _warMusicCheckBox;

    public TypanWarMinimapWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("typan-war-minimap-title");
        MinWidth = 680;
        MinHeight = 720;
        SetSize = new Vector2(720, 760);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        ContentsContainer.AddChild(root);

        _map = new TypanWarMinimapControl
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new Vector2(660, 640),
            Margin = new Thickness(2),
        };
        root.AddChild(_map);

        _warMusicCheckBox = new CheckBox
        {
            Text = Loc.GetString("typan-war-minimap-music"),
            Pressed = _cfg.GetCVar(MiniCCVars.WarMusicEnabled),
            HorizontalAlignment = HAlignment.Left,
            Margin = new Thickness(4, 0, 4, 2),
        };
        _warMusicCheckBox.OnToggled += args =>
        {
            _cfg.SetCVar(MiniCCVars.WarMusicEnabled, args.Pressed);
            _cfg.SaveToFile();
        };
        root.AddChild(_warMusicCheckBox);
    }

    public void OpenPrepared()
    {
        _warMusicCheckBox.Pressed = _cfg.GetCVar(MiniCCVars.WarMusicEnabled);
        _map.PrepareForDisplay();

        if (!IsOpen)
            OpenCentered();
    }

    public void Refresh(
        TypanWarMinimapGrid[] grids,
        TypanWarCaptureZoneStatus[] zones,
        TypanWarAllyBlip[] allies,
        int ntAlive,
        int typanAlive)
    {
        _map.Update(grids, zones, allies, ntAlive, typanAlive);
    }
}
