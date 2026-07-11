using Content.Client.UserInterface.Controls;
using Content.Shared._Mini.TypanWar;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._Mini.TypanWar;

public sealed class TypanWarMinimapWindow : FancyWindow
{
    private readonly TypanWarMinimapControl _map;    private readonly Label _scoreLegend;
    private readonly Label _mapLegend;

    public TypanWarMinimapWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("typan-war-minimap-title");
        MinWidth = 420;
        MinHeight = 500;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };
        ContentsContainer.AddChild(root);

        _map = new TypanWarMinimapControl
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MinSize = new System.Numerics.Vector2(380, 380),
        };
        root.AddChild(_map);

        _scoreLegend = new Label
        {
            FontColorOverride = Color.FromHex("#E8E6F0"),
            Align = Label.AlignMode.Center,
        };
        root.AddChild(_scoreLegend);

        _mapLegend = new Label
        {
            FontColorOverride = Color.FromHex("#A8A4B8"),
            Align = Label.AlignMode.Center,
            MinHeight = 28,
        };
        root.AddChild(_mapLegend);
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
        _scoreLegend.Text = Loc.GetString("typan-war-minimap-legend",
            ("nt", (int) ntPoints),
            ("typan", (int) typanPoints),
            ("win", toWin));

        _mapLegend.Text = Loc.GetString("typan-war-minimap-map-legend");
    }
}
