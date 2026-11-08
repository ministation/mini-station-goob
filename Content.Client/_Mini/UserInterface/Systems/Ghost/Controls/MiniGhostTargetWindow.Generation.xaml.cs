// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using Content.Client._Mini.UserInterface.Controls;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;
using GhostWarpPlayer = Content.Shared.Ghost.SharedGhostSystem.GhostWarpPlayer;
using GhostWarpPlace = Content.Shared.Ghost.SharedGhostSystem.GhostWarpPlace;
using GhostWarpGlobalAntagonist = Content.Shared.Ghost.SharedGhostSystem.GhostWarpGlobalAntagonist;

namespace Content.Client._Mini.UserInterface.Systems.Ghost.Controls;

public sealed partial class MiniGhostTargetWindow
{
    private static readonly Color AntagonistButtonColor = Color.FromHex("#c85a5a");
    private static readonly Color PlaceButtonColor = Color.FromHex("#969696");

    private const int DefaultButtonWidth = 180;
    private const int DefaultButtonHeight = 30;
    private const float DefaultTooltipDelay = 0.1f;
    private const int MaxLenght = 15;
    private const int MaxLenghtWithoutIcons = 18;

    // Коэффициенты усиления цвета
    private const float SaturationBoost = 2f;   // Насыщенность +30%
    private const float BrightnessBoost = 0.6f;  // Яркость +15%

    /// <summary>
    ///     Усиливает цвет: увеличивает насыщенность и яркость.
    ///     Делает кнопки отделов более выразительными.
    /// </summary>
    private static Color BoostColor(Color color)
    {
        var r = color.R;
        var g = color.G;
        var b = color.B;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        // Hue
        float h = 0;
        if (delta > 0.001f)
        {
            if (Math.Abs(max - r) < 0.001f)
                h = ((g - b) / delta) % 6;
            else if (Math.Abs(max - g) < 0.001f)
                h = (b - r) / delta + 2;
            else
                h = (r - g) / delta + 4;

            h /= 6;
            if (h < 0) h += 1;
        }

        // Saturation
        var s = max > 0.001f ? delta / max : 0;

        // Value
        var v = max;

        // Усиливаем насыщенность
        s = Math.Clamp(s * SaturationBoost, 0f, 1f);

        // Усиливаем яркость
        v = Math.Clamp(v + (1f - v) * BrightnessBoost, 0f, 1f);

        // Конвертируем обратно в RGB
        if (s < 0.001f)
            return new Color(v, v, v, color.A);

        var hue = h * 6;
        var sector = (int)Math.Floor(hue);
        var frac = hue - sector;
        var p = v * (1 - s);
        var q = v * (1 - s * frac);
        var t = v * (1 - s * (1 - frac));

        return sector switch
        {
            0 => new Color(v, t, p, color.A),
            1 => new Color(q, v, p, color.A),
            2 => new Color(p, v, t, color.A),
            3 => new Color(p, q, v, color.A),
            4 => new Color(t, p, v, color.A),
            _ => new Color(v, p, q, color.A),
        };
    }

    // ============================================================
    // Методы добавления кнопок
    // ============================================================

    private void AddPlayerButtons(List<GhostWarpPlayer> warps, string text)
    {
        if (warps.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };

        bigGrid.AddChild(bigLabel);

        var sortedWarps = GroupPlayersByDepartment(warps)
            .OrderByDescending(kvp => kvp.Key.Weight)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var (department, players) in sortedWarps)
        {
            var departmentGrid = new GridContainer
            {
                Columns = 2,
            };

            var departmentLabel = new Label
            {
                Text = Loc.GetString(department.Name) + ": " + players.Count,
                StyleClasses = { "LabelSecondaryColor" },
            };

            // Усиливаем цвет отдела: ярче и насыщеннее
            var boostedColor = BoostColor(department.Color);

            foreach (var player in players)
            {
                var playerButton = new RichTextButton
                {
                    ModulateSelfOverride = boostedColor,
                    Text = GeneratePlayerLabel(player),
                    TextAlign = Label.AlignMode.Right,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    SizeFlagsStretchRatio = 1,
                    ToolTip = GeneratePlayerTooltip(player),
                    TooltipDelay = DefaultTooltipDelay,
                    SetWidth = DefaultButtonWidth,
                    SetHeight = DefaultButtonHeight,
                };

                playerButton.OnPressed += _ => WarpClicked?.Invoke(player.Entity);

                departmentGrid.AddChild(playerButton);
            }

            bigGrid.AddChild(departmentLabel);
            bigGrid.AddChild(departmentGrid);
        }

        GhostTeleportContainer.AddChild(bigGrid);
    }

    private void AddPlaceButtons(List<GhostWarpPlace> places, string text)
    {
        if (places.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };
        bigGrid.AddChild(bigLabel);

        var placesGrid = new GridContainer
        {
            Columns = 2,
        };

        var countLabel = new Label
        {
            Text = Loc.GetString("ghost-teleport-menu-count-label") + ": " + places.Count,
            StyleClasses = { "LabelSecondaryColor" },
        };

        foreach (var place in places)
        {
            var placeButton = new RichTextButton
            {
                ModulateSelfOverride = PlaceButtonColor,
                Text = TruncateWithEllipsis(place.Name, MaxLenghtWithoutIcons),
                TextAlign = Label.AlignMode.Right,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                SizeFlagsStretchRatio = 1,
                ToolTip = GenerateGenericTooltip(place.Name, place.Description),
                TooltipDelay = DefaultTooltipDelay,
                SetWidth = DefaultButtonWidth,
                SetHeight = DefaultButtonHeight,
            };

            placeButton.OnPressed += _ => WarpClicked?.Invoke(place.Entity);

            placesGrid.AddChild(placeButton);
        }

        bigGrid.AddChild(countLabel);
        bigGrid.AddChild(placesGrid);

        GhostTeleportContainer.AddChild(bigGrid);
    }

    private void AddAntagButtons(List<GhostWarpGlobalAntagonist> antags, string text)
    {
        if (antags.Count == 0)
            return;

        var bigGrid = new GridContainer();

        var bigLabel = new Label
        {
            Text = Loc.GetString(text),
            StyleClasses = { "LabelBig" },
        };
        bigGrid.AddChild(bigLabel);

        var sortedAntags = SortAntagsByPriority(antags);

        foreach (var antagSet in sortedAntags)
        {
            var departmentGrid = new GridContainer
            {
                Columns = 2,
            };

            var labelText = string.Empty;

            foreach (var antag in antagSet)
            {
                var playerButton = new RichTextButton
                {
                    ModulateSelfOverride = AntagonistButtonColor,
                    Text = TruncateWithEllipsis(antag.Name, MaxLenghtWithoutIcons),
                    TextAlign = Label.AlignMode.Right,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    SizeFlagsStretchRatio = 1,
                    ToolTip = GenerateGenericTooltip(antag.Name, LocalizeOrRaw(antag.AntagonistDescription)),
                    TooltipDelay = DefaultTooltipDelay,
                    SetWidth = DefaultButtonWidth,
                    SetHeight = DefaultButtonHeight,
                };

                playerButton.OnPressed += _ => WarpClicked?.Invoke(antag.Entity);

                departmentGrid.AddChild(playerButton);

                labelText = antag.AntagonistName;
            }

            var departmentLabel = new Label
            {
                Text = Loc.GetString(labelText) + ": " + antagSet.Count,
                StyleClasses = { "LabelSecondaryColor" },
            };

            bigGrid.AddChild(departmentLabel);
            bigGrid.AddChild(departmentGrid);
        }

        GhostTeleportContainer.AddChild(bigGrid);
    }

    private static string LocalizeOrRaw(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return Loc.TryGetString(value, out var localized) ? localized : value;
    }
}
