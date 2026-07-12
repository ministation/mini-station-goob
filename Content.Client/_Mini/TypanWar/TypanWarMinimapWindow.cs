// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapWindow : FancyWindow
{
    private readonly TypanWarMinimapControl _map;

    public TypanWarMinimapWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("typan-war-minimap-title");
        MinWidth = 680;
        MinHeight = 680;
        SetSize = new Vector2(720, 720);

        _map = new TypanWarMinimapControl
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new Vector2(660, 660),
            Margin = new Thickness(2),
        };
        ContentsContainer.AddChild(_map);
    }

    public void OpenPrepared()
    {
        _map.PrepareForDisplay();

        if (!IsOpen)
            OpenCentered();
    }

    public void Refresh(
        TypanWarMinimapGrid[] grids,
        TypanWarCaptureZoneStatus[] zones,
        TypanWarAllyBlip[] allies,
        float ntPoints,
        float typanPoints,
        int toWin)
    {
        _map.Update(grids, zones, allies);
    }
}
