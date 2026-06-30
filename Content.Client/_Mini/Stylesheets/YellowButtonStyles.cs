// SPDX-FileCopyrightText: 2024 Your Name <you@example.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Maths;

namespace Content.Client.Stylesheets
{
    public static class YellowButtonStyles
    {
        // Константы цветов
        public static readonly Color ButtonColorDefaultYellow = Color.FromHex("#6a0dad");
        public static readonly Color ButtonColorHoveredYellow = Color.FromHex("#7b1fa2");
        public static readonly Color ButtonColorPressedYellow = Color.FromHex("#4a0072");
        public static readonly Color ButtonColorDisabledYellow = Color.FromHex("#3d1b5b");
        public static readonly Color ButtonColorTextYellow = Color.FromHex("#e453caff"); // Черный текст для контраста

        // Классы стилей
        public const string StyleClassButtonColorYellow = "ButtonColorYellow";
        public const string StyleClassButtonColorYellowBright = "ButtonColorYellowBright";
        public const string StyleClassButtonColorYellowDark = "ButtonColorYellowDark";
        public const string StyleClassButtonColorYellowCaution = "ButtonColorYellowCaution";

        // Яркие варианты
        public static readonly Color ButtonColorDefaultYellowBright = Color.FromHex("#9c27b0");
        public static readonly Color ButtonColorHoveredYellowBright = Color.FromHex("#ba68c8");

        // Темные варианты
        public static readonly Color ButtonColorDefaultYellowDark = Color.FromHex("#4a148c");
        public static readonly Color ButtonColorHoveredYellowDark = Color.FromHex("#6a1b9a");

        // Предупреждающие варианты
        public static readonly Color ButtonColorDefaultYellowCaution = Color.FromHex("#673ab7");
        public static readonly Color ButtonColorHoveredYellowCaution = Color.FromHex("#9575cd");
    }
}
