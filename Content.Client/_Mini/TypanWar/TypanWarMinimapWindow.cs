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
        MinWidth = 420;
        MinHeight = 440;

        _map = new TypanWarMinimapControl
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new System.Numerics.Vector2(400, 400),
            Margin = new Thickness(4),
        };
        ContentsContainer.AddChild(_map);
    }

    public void Update(
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
