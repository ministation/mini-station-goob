// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Lobby.UI;

public static class MiniCoinUi
{
    public const string CoinIconPath = "/Textures/_Mini/Interface/Coin.png";

    private const int PriceFontSize = 11;

    public static Label CreatePriceLabel(IResourceCache cache, int cost)
    {
        return new Label
        {
            Text = cost.ToString(),
            Modulate = Color.White,
            FontOverride = cache.GetStack("Regular", PriceFontSize),
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static TextureRect CreateCoinIcon(IResourceCache cache, float scale = 0.3f)
    {
        return new TextureRect
        {
            Texture = cache.GetTexture(CoinIconPath),
            MinSize = new Vector2(16, 16),
            MaxSize = new Vector2(16, 16),
            TextureScale = new Vector2(scale, scale),
            Stretch = TextureRect.StretchMode.KeepCentered,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static BoxContainer CreateUnlockPriceRow(IResourceCache cache, int cost, string prefixLocId)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
        };

        row.AddChild(new Label
        {
            Text = Loc.GetString(prefixLocId),
            StyleClasses = { StyleNano.StyleClassLabelSubText },
            VerticalAlignment = Control.VAlignment.Center,
        });
        row.AddChild(CreatePriceLabel(cache, cost));
        row.AddChild(CreateCoinIcon(cache));

        return row;
    }
}
